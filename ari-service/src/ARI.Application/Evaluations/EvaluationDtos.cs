using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARI.Domain.Entities;

namespace ARI.Application.Evaluations
{
    public class ConfirmReviewRequest
    {
        public Guid EvaluationId { get; set; }
        public string FinalVerdict { get; set; } = "pass"; // pass | not_pass
        public string? OverrideReason { get; set; }
        public bool ShareRecording { get; set; } = false;
        public bool ShareTranscript { get; set; } = false;
        public bool ShareEvaluation { get; set; } = false;
        public bool ShareFeedback { get; set; } = false;
        public string? CandidateFeedback { get; set; }
    }

    public class CriterionScoreDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; } = 100; // default to 100
        public string Reasoning { get; set; } = string.Empty;
    }

    public class QuestionAnalysisDto
    {
        /// <summary>Số thứ tự câu hỏi trong phiên — dùng để ghép phân tích vào đúng lượt hỏi–đáp thật.</summary>
        [JsonPropertyName("SequenceNumber")]
        public int SequenceNumber { get; set; }

        [JsonPropertyName("Question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("Answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("Score")]
        public decimal Score { get; set; }

        [JsonPropertyName("Analysis")]
        public string Analysis { get; set; } = string.Empty;

        [JsonPropertyName("Feedback")]
        public string? Feedback { get; set; }
    }

    public class CheatSignalDto
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
    }

    public class LanguageAssessmentDto
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = "en"; // default English

        [JsonPropertyName("fluency")]
        public decimal Fluency { get; set; }

        [JsonPropertyName("grammar")]
        public decimal Grammar { get; set; }

        [JsonPropertyName("vocabulary")]
        public decimal Vocabulary { get; set; }

        [JsonPropertyName("comprehension")]
        public decimal Comprehension { get; set; }

        [JsonPropertyName("overall_score")]
        public decimal OverallScore { get; set; }

        /// <summary>Bậc CEFR do AI kết luận (A1..C2) — hiển thị trực tiếp, không suy từ điểm ở FE.</summary>
        [JsonPropertyName("cefr_level")]
        public string? CefrLevel { get; set; }

        /// <summary>Ứng viên có trả lời đúng ngôn ngữ phỏng vấn yêu cầu không.</summary>
        [JsonPropertyName("language_adherence")]
        public string? LanguageAdherence { get; set; }

        /// <summary>Dẫn chứng trích từ câu trả lời của ứng viên.</summary>
        [JsonPropertyName("evidence")]
        public string? Evidence { get; set; }
    }

    public class HrReviewDto
    {
        public Guid Id { get; set; }
        public Guid EvaluationId { get; set; }
        public Guid ReviewedByUserId { get; set; }
        public string FinalVerdict { get; set; } = string.Empty;
        public bool IsOverride { get; set; }
        public string? OverrideReason { get; set; }
        public bool ShareRecording { get; set; }
        public bool ShareTranscript { get; set; }
        public bool ShareEvaluation { get; set; }
        public bool ShareFeedback { get; set; }
        public string? CandidateFeedback { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public static HrReviewDto FromEntity(HrReview review) =>
            new()
            {
                Id = review.Id,
                EvaluationId = review.EvaluationId,
                ReviewedByUserId = review.ReviewedByUserId,
                FinalVerdict = review.FinalVerdict,
                IsOverride = review.IsOverride,
                OverrideReason = review.OverrideReason,
                ShareRecording = review.ShareRecording,
                ShareTranscript = review.ShareTranscript,
                ShareEvaluation = review.ShareEvaluation,
                ShareFeedback = review.ShareFeedback,
                CandidateFeedback = review.CandidateFeedback,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            };
    }

    public class EvaluationDetailResponse
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public string AiVerdict { get; set; } = string.Empty;
        public decimal? OverallScore { get; set; }
        public Dictionary<string, decimal>? CriterionScores { get; set; }
        public string? Reasoning { get; set; }
        public string? RecommendedNextStep { get; set; }
        public List<QuestionAnalysisDto>? QuestionAnalyses { get; set; }
        public decimal? CheatScore { get; set; }
        public List<CheatSignalDto>? CheatSignals { get; set; }
        public LanguageAssessmentDto? LanguageAssessment { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        // Associated details
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;

        // HR Review if exists
        public HrReviewDto? HrReview { get; set; }

        /// <summary>Video buổi phỏng vấn thật (ADR-052) — null nếu chưa quay hoặc đã quá hạn lưu.</summary>
        public string? RecordingUrl { get; set; }
        /// <summary>Hạn lưu video — HR biết còn bao lâu để xem/tải trước khi bị xoá tự động.</summary>
        public DateTimeOffset? RecordingExpiresAt { get; set; }
        /// <summary>Thời điểm video đã bị xoá theo hạn lưu (để HR không tưởng là mất dữ liệu).</summary>
        public DateTimeOffset? RecordingDeletedAt { get; set; }

        public static EvaluationDetailResponse FromEntity(
            Evaluation eval, 
            ARI.Domain.Entities.Application app, 
            JobPosting job, 
            HrReview? hrReview)
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            Dictionary<string, decimal>? parsedCriteria = null;
            if (!string.IsNullOrEmpty(eval.CriterionScores))
            {
                try
                {
                    parsedCriteria = JsonSerializer.Deserialize<Dictionary<string, decimal>>(eval.CriterionScores, options);
                }
                catch
                {
                    // Fallback to case-insensitive if camelCase fails
                    try
                    {
                        parsedCriteria = JsonSerializer.Deserialize<Dictionary<string, decimal>>(eval.CriterionScores);
                    }
                    catch { /* ignore */ }
                }
            }

            List<QuestionAnalysisDto>? parsedQuestions = null;
            if (!string.IsNullOrEmpty(eval.QuestionAnalyses))
            {
                try
                {
                    parsedQuestions = JsonSerializer.Deserialize<List<QuestionAnalysisDto>>(eval.QuestionAnalyses, options);
                }
                catch { /* ignore */ }
            }

            List<CheatSignalDto>? parsedCheatSignals = null;
            if (!string.IsNullOrEmpty(eval.CheatSignals))
            {
                try
                {
                    parsedCheatSignals = JsonSerializer.Deserialize<List<CheatSignalDto>>(eval.CheatSignals, options);
                }
                catch { /* ignore */ }
            }

            LanguageAssessmentDto? parsedLang = null;
            if (!string.IsNullOrEmpty(eval.LanguageAssessment))
            {
                try
                {
                    parsedLang = JsonSerializer.Deserialize<LanguageAssessmentDto>(eval.LanguageAssessment, options);
                }
                catch { /* ignore */ }
            }

            return new EvaluationDetailResponse
            {
                Id = eval.Id,
                SessionId = eval.SessionId,
                ApplicationId = eval.ApplicationId,
                RoundNumber = eval.RoundNumber,
                SessionType = eval.SessionType,
                AiVerdict = eval.AiVerdict,
                OverallScore = eval.OverallScore,
                CriterionScores = parsedCriteria,
                Reasoning = eval.Reasoning,
                RecommendedNextStep = eval.RecommendedNextStep,
                QuestionAnalyses = parsedQuestions,
                CheatScore = eval.CheatScore,
                CheatSignals = parsedCheatSignals,
                LanguageAssessment = parsedLang,
                CreatedAt = eval.CreatedAt,
                UpdatedAt = eval.UpdatedAt,
                CandidateName = app.CandidateName,
                CandidateEmail = app.CandidateEmail,
                JobTitle = job.Title,
                HrReview = hrReview != null ? HrReviewDto.FromEntity(hrReview) : null
            };
        }
    }

    public class EvaluationListItemResponse
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public string AiVerdict { get; set; } = string.Empty;
        public decimal? OverallScore { get; set; }
        public decimal? CheatScore { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        
        // Joined details
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;

        // Review info
        public string Status { get; set; } = "pending"; // pending | completed
        public string FinalVerdict { get; set; } = string.Empty;

        public static EvaluationListItemResponse FromEntity(
            Evaluation eval,
            ARI.Domain.Entities.Application app,
            JobPosting job,
            HrReview? hrReview)
        {
            return new EvaluationListItemResponse
            {
                Id = eval.Id,
                SessionId = eval.SessionId,
                ApplicationId = eval.ApplicationId,
                RoundNumber = eval.RoundNumber,
                SessionType = eval.SessionType,
                AiVerdict = eval.AiVerdict,
                OverallScore = eval.OverallScore,
                CheatScore = eval.CheatScore,
                CreatedAt = eval.CreatedAt,
                CandidateName = app.CandidateName,
                CandidateEmail = app.CandidateEmail,
                JobTitle = job.Title,
                Status = hrReview != null ? "completed" : "pending",
                FinalVerdict = hrReview != null ? hrReview.FinalVerdict : eval.AiVerdict
            };
        }
    }
}
