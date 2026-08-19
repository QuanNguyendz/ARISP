using System;
using ARI.Application.Playbooks;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
namespace ARI.Application.Services
{
    public class CvJdAnalysisService : ICvJdAnalysisService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGeminiProvider _geminiProvider;
        private readonly IDocumentParserService _documentParserService;

        public CvJdAnalysisService(IUnitOfWork unitOfWork, IGeminiProvider geminiProvider, IDocumentParserService documentParserService)
        {
            _unitOfWork = unitOfWork;
            _geminiProvider = geminiProvider;
            _documentParserService = documentParserService;
        }

        public async Task<Result<CvJdAnalysis>> AnalyzeAndCacheAsync(Guid jobPostingId, System.IO.Stream cvFileStream, string cvFileName, CancellationToken ct = default)
        {
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (jobPosting == null)
            {
                return Result.Failure<CvJdAnalysis>("Không tìm thấy tin tuyển dụng.");
            }

            string cvHash = ComputeFileHash(cvFileStream);

            var existingAnalyses = await _unitOfWork.Repository<CvJdAnalysis>()
                .FindAsync(x => x.JobPostingId == jobPostingId && x.CvHash == cvHash, ct);
            var existingAnalysis = System.Linq.Enumerable.FirstOrDefault(existingAnalyses);

            if (existingAnalysis != null && existingAnalysis.Status == "completed")
            {
                if (!string.IsNullOrEmpty(existingAnalysis.RawResponse) && existingAnalysis.RawResponse != "{}")
                {
                    try {
                        using var doc = JsonDocument.Parse(existingAnalysis.RawResponse);
                        var root = doc.RootElement;
                        var candidates = root.GetProperty("candidates")[0];
                        var rawText = candidates.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                        
                        if (rawText != null)
                        {
                            if (rawText.StartsWith("```json")) rawText = rawText.Substring(7, rawText.Length - 10);
                            using var innerDoc = JsonDocument.Parse(rawText);
                            if (innerDoc.RootElement.TryGetProperty("analysis_reasoning", out var p1)) existingAnalysis.AnalysisReasoning = p1.GetString();
                            if (innerDoc.RootElement.TryGetProperty("seniority_alignment", out var p2)) existingAnalysis.SeniorityAlignment = p2.GetString();
                            if (innerDoc.RootElement.TryGetProperty("tech_depth_analysis", out var p3)) existingAnalysis.TechDepthAnalysis = p3.GetString();
                        }
                    } catch {}
                }
                return Result.Success(existingAnalysis);
            }

            var skills = jobPosting.Skills != null ? string.Join(", ", jobPosting.Skills) : "None";
            string jdContext = $@"
Title: {jobPosting.Title}
Department: {jobPosting.Department}
Experience Level: {jobPosting.ExperienceLevel}
Required Skills: {skills}
Language Requirement: {jobPosting.LanguageRequirement}
Work Mode: {jobPosting.WorkMode}
Location: {jobPosting.Location}
Scoring Rubric: {jobPosting.ScoringRubric ?? "Sử dụng trọng số chuẩn: Kinh nghiệm (40%), Kỹ năng chuyên môn (40%), Học vấn và Kỹ năng mềm (20%)"}

--- Detailed Description ---
{jobPosting.JobDescription}";
            
            byte[] cvBytes;
            using (var ms = new System.IO.MemoryStream())
            {
                await cvFileStream.CopyToAsync(ms, ct);
                cvBytes = ms.ToArray();
            }
            string mimeType = cvFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : 
                              cvFileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : 
                              "text/plain";

            cvFileStream.Position = 0;
            string fallbackCvText = await _documentParserService.ParseDocumentAsync(cvFileStream, System.IO.Path.GetExtension(cvFileName));

            // Bộ tiêu chí chấm CV do doanh nghiệp khai (playbook cv_rubric) + ngữ cảnh playbook
            // liên quan. Đây là phần "chấm theo cấu hình công ty" thay cho việc AI tự nghĩ ra
            // trọng số (ADR-060) — không khai thì giữ nguyên hành vi cũ.
            var criteria = await PlaybookScope.ResolveRubricAsync(
                _unitOfWork, jobPosting.Id, 1, ScoringRubric.TypeCvRubric, ct);
            var rubricInstruction = criteria.Count == 0
                ? null
                : await BuildRubricInstructionAsync(jobPosting.Id, criteria, ct);

            var geminiResult = await _geminiProvider.AnalyzeCvJdMatchAsync(
                jdContext, 
                cvBytes, 
                mimeType, 
                fallbackCvText, 
                rubricInstruction,
                ct);

            if (geminiResult.IsFailure)
                return Result.Failure<CvJdAnalysis>($"Lỗi AI: {geminiResult.Error}");

            var resultDto = geminiResult.Value;

            if (!resultDto.IsValidCv)
            {
                var invalidAnalysis = new CvJdAnalysis
                {
                    JobPostingId = jobPostingId,
                    CvHash = cvHash,
                    MatchScore = 0,
                    Summary = resultDto.Summary,
                    Status = "failed",
                    ErrorMessage = "File tải lên không hợp lệ hoặc không phải là CV.",
                    AiModel = resultDto.Provider,
                    PromptTokens = resultDto.PromptTokens,
                    CompletionTokens = resultDto.CompletionTokens
                };
                await _unitOfWork.Repository<CvJdAnalysis>().AddAsync(invalidAnalysis, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<CvJdAnalysis>("Tài liệu không phải là một CV hợp lệ.");
            }

            // ĐIỂM CUỐI DO BACKEND CỘNG khi có bộ tiêu chí: AI chỉ chấm từng tiêu chí. Trước đây
            // match_score là con số AI tự đưa ra, không suy ra từ tiêu chí nào cả (ADR-060).
            var aiCriterionScores = resultDto.CriterionScores ?? new Dictionary<string, decimal>();
            var finalScore = resultDto.MatchScore;
            var criterionSnapshot = "{}";
            if (criteria.Count > 0 && resultDto.IsValidCv)
            {
                var computed = ScoringRubric.ComputeOverall(criteria, aiCriterionScores);
                if (computed.HasValue)
                {
                    finalScore = (int)Math.Round(computed.Value, MidpointRounding.AwayFromZero);
                    criterionSnapshot = ScoringRubric.SerializeScoreSnapshot(criteria, aiCriterionScores);
                }
            }

            var analysis = new CvJdAnalysis
            {
                JobPostingId = jobPostingId,
                CvHash = cvHash,
                MatchScore = finalScore,
                CriterionScores = criterionSnapshot,
                Summary = resultDto.Summary,
                SkillsMatched = JsonSerializer.Serialize(resultDto.SkillsMatched),
                SkillsGaps = JsonSerializer.Serialize(resultDto.SkillsGaps),
                RedFlags = JsonSerializer.Serialize(resultDto.RedFlags),
                ExperienceRelevance = resultDto.ExperienceRelevance,
                OverallRecommendation = resultDto.OverallRecommendation,
                AiModel = resultDto.Provider,
                Status = "completed",
                PromptTokens = resultDto.PromptTokens,
                CompletionTokens = resultDto.CompletionTokens,
                RawResponse = resultDto.RawResponse,
                AnalysisReasoning = resultDto.AnalysisReasoning,
                SeniorityAlignment = resultDto.SeniorityAlignment,
                TechDepthAnalysis = resultDto.TechDepthAnalysis
            };

            await _unitOfWork.Repository<CvJdAnalysis>().AddAsync(analysis, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(analysis);
        }

        public async Task<Result<CvJdAnalysis>> GetAnalysisByIdAsync(Guid id, CancellationToken ct = default)
        {
            var analysis = await _unitOfWork.Repository<CvJdAnalysis>().GetByIdAsync(id, ct);
            if (analysis == null)
                return Result.Failure<CvJdAnalysis>("Không tìm thấy bản đánh giá.");
            return Result.Success(analysis);
        }

        public async Task<Result<CvJdAnalysis>> GetAnalysisByApplicationIdAsync(Guid applicationId, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null || application.CvJdAnalysisId == null)
                return Result.Failure<CvJdAnalysis>("Không tìm thấy bản đánh giá cho đơn ứng tuyển này.");

            return await GetAnalysisByIdAsync(application.CvJdAnalysisId.Value, ct);
        }

        public async Task<bool> CheckCandidateOwnershipAsync(Guid cvAnalysisId, Guid candidateAccountId, CancellationToken ct = default)
        {
            var applications = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .FindAsync(a => a.CvJdAnalysisId == cvAnalysisId && a.CandidateAccountId == candidateAccountId, ct);
            return System.Linq.Enumerable.Any(applications);
        }

        public async Task ClearAllCacheAsync(CancellationToken ct = default)
        {
            var all = await _unitOfWork.Repository<CvJdAnalysis>().GetAllAsync(ct);
            foreach (var item in all)
            {
                _unitOfWork.Repository<CvJdAnalysis>().Delete(item);
            }
            await _unitOfWork.SaveChangesAsync(ct);
        }

        private string ComputeFileHash(System.IO.Stream stream)
        {
            using var md5 = MD5.Create();
            long originalPosition = stream.Position;
            stream.Position = 0;
            var hash = md5.ComputeHash(stream);
            stream.Position = originalPosition;
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Phần rubric nhồi vào prompt: bảng tiêu chí + trọng số + chuẩn chấm, kèm ngữ cảnh playbook
        /// của tin (khung năng lực, chuẩn đánh giá…) để AI chấm theo tài liệu của doanh nghiệp chứ
        /// không theo cảm nhận chung chung. Playbook loại <c>compliance</c> đi vào phần CẤM: những
        /// thuộc tính đó không được ảnh hưởng tới điểm.
        /// </summary>
        private async Task<string> BuildRubricInstructionAsync(
            Guid jobPostingId, List<RubricCriterion> criteria, CancellationToken ct)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(ScoringRubric.ToPromptText(criteria));

            var eligibleIds = await PlaybookScope.EligibleDocumentIdsAsync(_unitOfWork, jobPostingId, 1, ct);
            if (eligibleIds.Count == 0) return sb.ToString();

            var complianceIds = (await _unitOfWork.Repository<PlaybookDocument>().QueryAsync(
                    q => q.Where(p => eligibleIds.Contains(p.Id) && p.DocumentType == PlaybookScope.TypeCompliance)
                          .Select(p => p.Id), ct))
                .ToHashSet();

            var chunks = (await _unitOfWork.Repository<DocumentChunk>().QueryAsync(
                    q => q.Where(c => c.SourceType == "playbook" && eligibleIds.Contains(c.SourceId))
                          .Select(c => new { c.SourceId, c.ChunkText }), ct))
                .ToList();

            var context = new List<string>();
            var prohibited = new List<string>();
            foreach (var chunk in chunks)
            {
                if (complianceIds.Contains(chunk.SourceId)) prohibited.Add(chunk.ChunkText);
                else context.Add(chunk.ChunkText);
            }

            // Chặn trên độ dài: playbook dài không được đẩy CV/JD ra khỏi cửa sổ ngữ cảnh.
            const int MaxContextChars = 6000;
            if (context.Count > 0)
            {
                var joined = string.Join("\n- ", context);
                if (joined.Length > MaxContextChars) joined = joined.Substring(0, MaxContextChars);
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

    }
}
