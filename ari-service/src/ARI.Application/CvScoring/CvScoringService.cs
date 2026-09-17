using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ARI.Application.CvScoring
{
    public static class CvScoringErrors
    {
        /// <summary>Tin chưa có bộ tiêu chí chấm CV — không chấm (ADR-070).</summary>
        public const string RubricRequired = "cv_rubric_required";

        // Lý do một lượt chấm hỏng — giao diện nói bằng lời của mình, không in nguyên thông điệp của nhà cung cấp AI.
        /// <summary>Không đọc được nội dung / file CV.</summary>
        public const string CvUnreadable = "cv_unreadable";
        /// <summary>Gemini và dự phòng GPT-4o-mini đều lỗi hoặc quá tải.</summary>
        public const string AiUnavailable = "ai_unavailable";
        /// <summary>AI trả lời nhưng không chấm được tiêu chí nào của bộ tiêu chí.</summary>
        public const string NoCriterionScored = "no_criterion_scored";
    }

    /// <summary>
    /// Chấm một file CV cho một tin theo bộ tiêu chí ĐANG SỐNG của tin đó (ADR-070).
    /// </summary>
    public interface ICvScoringService
    {
        /// <summary>
        /// Trả bản chấm có sẵn cho (tin, file, bộ tiêu chí), hoặc chấm mới. Kết quả thành công có thể mang
        /// trạng thái <c>invalid_cv</c> — đó là kết cục chắc chắn của file, không phải lỗi.
        /// Lỗi mang mã <see cref="CvScoringErrors.RubricRequired"/> khi tin chưa có bộ tiêu chí; khi đó
        /// KHÔNG có lời gọi AI nào.
        /// </summary>
        Task<Result<CvJdAnalysis>> ScoreAsync(Guid jobPostingId, byte[] cvBytes, string cvFileName, CancellationToken ct = default);

        /// <summary>Tra (không chấm): bộ tiêu chí sống, khoá, và bản chấm có sẵn nếu có.</summary>
        Task<CvScoreLookup> LookupAsync(Guid jobPostingId, byte[] cvBytes, CancellationToken ct = default);
    }

    public sealed record CvScoreLookup(PlaybookDocument? Rubric, string CvHash, string? Key, CvJdAnalysis? Existing);

    public class CvScoringService : ICvScoringService
    {
        /// <summary>Loại playbook được đưa vào ngữ cảnh chấm CV. Các loại khác thuộc về buổi phỏng vấn.</summary>
        private static readonly string[] ContextTypes =
        {
            "competency_framework", PlaybookScope.TypeRedFlag, PlaybookScope.TypeCompliance,
        };

        /// <summary>Chặn trên độ dài: playbook dài không được đẩy CV/JD ra khỏi cửa sổ ngữ cảnh.</summary>
        private const int MaxContextChars = 6000;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IGeminiProvider _gemini;
        private readonly IDocumentParserService _parser;
        private readonly IFileStorageService _fileStorage;
        private readonly CvScoringInFlight _inFlight;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CvScoringService> _logger;

        public CvScoringService(
            IUnitOfWork unitOfWork,
            IGeminiProvider gemini,
            IDocumentParserService parser,
            IFileStorageService fileStorage,
            CvScoringInFlight inFlight,
            IMemoryCache cache,
            ILogger<CvScoringService> logger)
        {
            _unitOfWork = unitOfWork;
            _gemini = gemini;
            _parser = parser;
            _fileStorage = fileStorage;
            _inFlight = inFlight;
            _cache = cache;
            _logger = logger;
        }

        public static string ComputeHash(byte[] bytes)
            => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

        public async Task<CvScoreLookup> LookupAsync(Guid jobPostingId, byte[] cvBytes, CancellationToken ct = default)
        {
            var hash = ComputeHash(cvBytes);
            var rubric = await CvRubricStore.LiveAsync(_unitOfWork, jobPostingId, ct);
            if (rubric == null) return new CvScoreLookup(null, hash, null, null);

            var existing = await FindAsync(jobPostingId, hash, rubric.Id, ct);
            return new CvScoreLookup(rubric, hash, CvScoringInFlight.Key(jobPostingId, hash, rubric.Id), existing);
        }

        public async Task<Result<CvJdAnalysis>> ScoreAsync(Guid jobPostingId, byte[] cvBytes, string cvFileName, CancellationToken ct = default)
        {
            if (cvBytes == null || cvBytes.Length == 0)
                return Result.Failure<CvJdAnalysis>("File CV trống.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null)
                return Result.Failure<CvJdAnalysis>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            var rubric = await CvRubricStore.LiveAsync(_unitOfWork, jobPostingId, ct);
            var criteria = CvRubricStore.Criteria(rubric);
            if (rubric == null || criteria.Count == 0)
                return Result.Failure<CvJdAnalysis>(
                    "Tin này chưa có bộ tiêu chí chấm CV — Hiring Manager cần khai bộ tiêu chí trước khi hệ thống chấm.",
                    CvScoringErrors.RubricRequired);

            var hash = ComputeHash(cvBytes);
            var cached = await FindAsync(jobPostingId, hash, rubric.Id, ct);
            if (cached != null) return Result.Success(cached);

            var key = CvScoringInFlight.Key(jobPostingId, hash, rubric.Id);
            using (await _inFlight.AcquireAsync(key, ct))
            {
                // Người giữ khoá trước có thể vừa chấm xong đúng file này.
                cached = await FindAsync(jobPostingId, hash, rubric.Id, ct);
                if (cached != null) return Result.Success(cached);

                var result = await ScoreCoreAsync(job, rubric, criteria, hash, cvBytes, cvFileName, ct);
                if (result.IsFailure) _inFlight.RecordFailure(key, result.Error!, result.ErrorCode);
                else _inFlight.ClearFailure(key);
                return result;
            }
        }

        private async Task<Result<CvJdAnalysis>> ScoreCoreAsync(
            JobPosting job, PlaybookDocument rubric, List<RubricCriterion> criteria,
            string hash, byte[] cvBytes, string cvFileName, CancellationToken ct)
        {
            var ext = Path.GetExtension(cvFileName ?? string.Empty).ToLowerInvariant();
            var isPdf = ext == ".pdf";

            string? cvText = null;
            try
            {
                using var stream = new MemoryStream(cvBytes);
                cvText = (await _parser.ParseDocumentAsync(stream, ext))?.Replace("\0", string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không trích được text từ CV ({Ext}) — {Fallback}", ext, isPdf ? "gửi file PDF gốc" : "không chấm được");
            }

            if (!isPdf && string.IsNullOrWhiteSpace(cvText))
                return Result.Failure<CvJdAnalysis>("Không đọc được nội dung file CV.", CvScoringErrors.CvUnreadable);

            var (jdText, jdPdf) = await BuildJdAsync(job, ct);
            var rubricInstruction = await BuildRubricInstructionAsync(job.Id, criteria, ct);

            var ai = await _gemini.AnalyzeCvJdMatchAsync(new CvScoringAiRequest(
                jdText,
                jdPdf,
                isPdf ? new AiAttachment("cv.pdf", "application/pdf", cvBytes) : null,
                cvText,
                rubricInstruction,
                criteria.Select(c => c.Key).ToList()), ct);

            if (ai.IsFailure)
                return Result.Failure<CvJdAnalysis>($"Lỗi AI: {ai.Error}", CvScoringErrors.AiUnavailable);

            var dto = ai.Value!;
            CvJdAnalysis analysis;

            if (!dto.IsValidCv)
            {
                analysis = new CvJdAnalysis
                {
                    JobPostingId = job.Id,
                    CvHash = hash,
                    RubricDocumentId = rubric.Id,
                    MatchScore = 0,
                    Summary = dto.Summary,
                    Status = CvAnalysisStatuses.InvalidCv,
                    ErrorMessage = "File tải lên không phải là CV.",
                    AiModel = dto.Provider,
                    PromptTokens = dto.PromptTokens,
                    CompletionTokens = dto.CompletionTokens,
                    ProcessingTimeMs = dto.ProcessingTimeMs,
                    RawResponse = SafeJson(dto.RawResponse),
                };
            }
            else
            {
                // ĐIỂM CUỐI DO BACKEND CỘNG. Không có nhánh nào lấy điểm tổng từ AI (ADR-070).
                var indexed = CvScoreSnapshot.IndexAiResults(criteria, dto.Criteria);
                var overall = ScoringRubric.ComputeOverall(criteria, CvScoreSnapshot.Scores(criteria, indexed));
                if (overall == null)
                    return Result.Failure<CvJdAnalysis>(
                        "AI chưa chấm được tiêu chí nào của bộ tiêu chí — sẽ thử lại sau.", CvScoringErrors.NoCriterionScored);

                var score = (int)Math.Round(overall.Value, MidpointRounding.AwayFromZero);
                analysis = new CvJdAnalysis
                {
                    JobPostingId = job.Id,
                    CvHash = hash,
                    RubricDocumentId = rubric.Id,
                    MatchScore = score,
                    CriterionScores = CvScoreSnapshot.Serialize(criteria, indexed),
                    Summary = dto.Summary,
                    SkillsMatched = JsonSerializer.Serialize(dto.SkillsMatched ?? new List<string>()),
                    SkillsGaps = JsonSerializer.Serialize(dto.SkillsGaps ?? new List<string>()),
                    RedFlags = JsonSerializer.Serialize(dto.RedFlags ?? new List<string>()),
                    ExperienceRelevance = dto.ExperienceRelevance,
                    SeniorityAlignment = string.IsNullOrWhiteSpace(dto.SeniorityAlignment) ? null : dto.SeniorityAlignment,
                    OverallRecommendation = CvScoreSnapshot.Recommendation(score),
                    AiModel = dto.Provider,
                    Status = CvAnalysisStatuses.Completed,
                    PromptTokens = dto.PromptTokens,
                    CompletionTokens = dto.CompletionTokens,
                    ProcessingTimeMs = dto.ProcessingTimeMs,
                    RawResponse = SafeJson(dto.RawResponse),
                    AnalysisReasoning = dto.AnalysisReasoning,
                    TechDepthAnalysis = dto.TechDepthAnalysis,
                };
            }

            var repo = _unitOfWork.Repository<CvJdAnalysis>();
            await repo.AddAsync(analysis, ct);
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // UNIQUE (tin, CV, bộ tiêu chí): tiến trình khác vừa ghi đúng khoá này → dùng bản đó.
                repo.Delete(analysis); // gỡ bản đang chờ ghi khỏi ngữ cảnh
                var winner = await FindAsync(job.Id, hash, rubric.Id, ct);
                if (winner != null) return Result.Success(winner);
                _logger.LogError(ex, "Lưu kết quả chấm CV thất bại cho tin {JobId}", job.Id);
                return Result.Failure<CvJdAnalysis>("Không lưu được kết quả chấm CV.", CommonErrorCodes.ServerError);
            }

            return Result.Success(analysis);
        }

        private async Task<CvJdAnalysis?> FindAsync(Guid jobPostingId, string hash, Guid rubricId, CancellationToken ct)
            => (await _unitOfWork.Repository<CvJdAnalysis>().FindAsync(
                    a => a.JobPostingId == jobPostingId && a.CvHash == hash && a.RubricDocumentId == rubricId, ct))
               .FirstOrDefault();

        /// <summary>
        /// Phần JD gửi cho AI: các trường có cấu trúc + mô tả công việc, và FILE JD GỐC (ADR-070 — trước đây
        /// file JD không bao giờ tới tay model, trái quy tắc 17). PDF gửi nguyên file; DOCX trích text.
        /// File gốc hỏng/mất thì vẫn chấm được bằng phần có cấu trúc.
        /// </summary>
        private async Task<(string Text, AiAttachment? Pdf)> BuildJdAsync(JobPosting job, CancellationToken ct)
        {
            var skills = job.Skills is { Count: > 0 } ? string.Join(", ", job.Skills) : "(không nêu)";
            var text =
                $"Title: {job.Title}\n"
                + $"Department: {job.Department}\n"
                + $"Experience Level: {job.ExperienceLevel}\n"
                + $"Employment Type: {job.EmploymentType}\n"
                + $"Required Skills: {skills}\n"
                + $"Language Requirement: {job.LanguageRequirement}\n"
                + $"Work Mode: {job.WorkMode}\n"
                + $"Location: {job.Location}\n\n"
                + $"--- Detailed Description ---\n{HtmlToText(job.JobDescription)}";

            AiAttachment? pdf = null;
            var fmt = (job.JdFileFormat ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(job.JdFileUrl) && fmt is "pdf" or "docx")
            {
                try
                {
                    var bytes = await _cache.GetOrCreateAsync($"cv-scoring:jd-file:{job.JdFileUrl}", async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                        return await _fileStorage.ReadAllBytesAsync(job.JdFileUrl!, ct);
                    });

                    if (bytes is { Length: > 0 })
                    {
                        if (fmt == "pdf")
                        {
                            pdf = new AiAttachment(job.JdFileName ?? "jd.pdf", "application/pdf", bytes);
                        }
                        else
                        {
                            using var ms = new MemoryStream(bytes);
                            var docxText = (await _parser.ParseDocumentAsync(ms, ".docx"))?.Replace("\0", string.Empty);
                            if (!string.IsNullOrWhiteSpace(docxText))
                                text += $"\n\n--- Nội dung file JD gốc ---\n{docxText}";
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Không đọc được file JD gốc của tin {JobId} — chấm bằng phần có cấu trúc.", job.Id);
                }
            }

            return (text, pdf);
        }

        /// <summary>
        /// Phần rubric nhồi vào prompt: bảng tiêu chí + mức neo, kèm tài liệu nội bộ liên quan tới việc đọc CV
        /// (khung năng lực, dấu hiệu cần lưu ý) của công ty và của tin. Playbook <c>compliance</c> đi vào phần
        /// CẤM: những thuộc tính đó không được ảnh hưởng tới điểm.
        /// </summary>
        private async Task<string> BuildRubricInstructionAsync(Guid jobPostingId, List<RubricCriterion> criteria, CancellationToken ct)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(ScoringRubric.ToPromptText(criteria));

            var docs = await _unitOfWork.Repository<PlaybookDocument>().QueryAsync(
                q => q.Where(p => p.DeletedAt == null
                                  && ContextTypes.Contains(p.DocumentType)
                                  && (p.Scope == PlaybookScope.ScopeOrg
                                      || (p.Scope == PlaybookScope.ScopeJobPosting && p.ScopeRefId == jobPostingId)))
                      .Select(p => new { p.Id, p.DocumentType }), ct);
            if (docs.Count == 0) return sb.ToString();

            var ids = docs.Select(d => d.Id).ToList();
            var complianceIds = docs.Where(d => d.DocumentType == PlaybookScope.TypeCompliance).Select(d => d.Id).ToHashSet();

            var chunks = await _unitOfWork.Repository<DocumentChunk>().QueryAsync(
                q => q.Where(c => c.SourceType == "playbook" && ids.Contains(c.SourceId))
                      .Select(c => new { c.SourceId, c.ChunkText }), ct);

            var context = chunks.Where(c => !complianceIds.Contains(c.SourceId)).Select(c => c.ChunkText).ToList();
            var prohibited = chunks.Where(c => complianceIds.Contains(c.SourceId)).Select(c => c.ChunkText).ToList();

            if (context.Count > 0)
            {
                var joined = string.Join("\n- ", context);
                if (joined.Length > MaxContextChars) joined = joined[..MaxContextChars];
                sb.AppendLine();
                sb.AppendLine("--- TÀI LIỆU NỘI BỘ ĐỂ ĐỐI CHIẾU ---");
                sb.AppendLine("- " + joined);
            }
            if (prohibited.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- KHÔNG ĐƯỢC DÙNG ĐỂ CHẤM ĐIỂM (pháp lý) ---");
                foreach (var p in prohibited) sb.AppendLine("- " + p);
            }

            return sb.ToString();
        }

        private static readonly Regex BlockTags = new(@"</?(p|div|br|li|ul|ol|h[1-6]|tr)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AnyTag = new(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex BlankLines = new(@"\n{3,}", RegexOptions.Compiled);

        /// <summary>Mô tả công việc lưu dạng HTML (ADR-064) — gửi văn bản thuần cho model, bỏ nhiễu thẻ.</summary>
        public static string HtmlToText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            var text = BlockTags.Replace(html, "\n");
            text = AnyTag.Replace(text, string.Empty);
            text = WebUtility.HtmlDecode(text);
            return BlankLines.Replace(text.Replace("\r", string.Empty), "\n\n").Trim();
        }

        /// <summary>Cột <c>raw_response</c> là jsonb — chuỗi không phải JSON sẽ làm hỏng cả lượt lưu.</summary>
        private static string SafeJson(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "{}";
            try
            {
                using var _ = JsonDocument.Parse(raw);
                return raw;
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(new { raw });
            }
        }
    }
}
