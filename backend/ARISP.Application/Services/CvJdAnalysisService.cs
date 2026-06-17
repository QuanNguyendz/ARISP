using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Common;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;
namespace ARISP.Application.Services
{
    public class CvJdAnalysisService
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

            var geminiResult = await _geminiProvider.AnalyzeCvJdMatchAsync(
                jdContext, 
                cvBytes, 
                mimeType, 
                fallbackCvText, 
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
                    AiModel = "gemini-2.5-flash",
                    PromptTokens = resultDto.PromptTokens,
                    CompletionTokens = resultDto.CompletionTokens
                };
                await _unitOfWork.Repository<CvJdAnalysis>().AddAsync(invalidAnalysis, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<CvJdAnalysis>("Tài liệu không phải là một CV hợp lệ.");
            }

            var analysis = new CvJdAnalysis
            {
                JobPostingId = jobPostingId,
                CvHash = cvHash,
                MatchScore = resultDto.MatchScore,
                Summary = resultDto.Summary,
                SkillsMatched = JsonSerializer.Serialize(resultDto.SkillsMatched),
                SkillsGaps = JsonSerializer.Serialize(resultDto.SkillsGaps),
                RedFlags = JsonSerializer.Serialize(resultDto.RedFlags),
                ExperienceRelevance = resultDto.ExperienceRelevance,
                OverallRecommendation = resultDto.OverallRecommendation,
                AiModel = "gemini-2.5-flash",
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
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null || application.CvJdAnalysisId == null)
                return Result.Failure<CvJdAnalysis>("Không tìm thấy bản đánh giá cho đơn ứng tuyển này.");

            return await GetAnalysisByIdAsync(application.CvJdAnalysisId.Value, ct);
        }

        public async Task<bool> CheckCandidateOwnershipAsync(Guid cvAnalysisId, Guid candidateAccountId, CancellationToken ct = default)
        {
            var applications = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
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
    }
}
