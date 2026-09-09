using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARI.Application.Playbooks;
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

        // ===== ADR-061 =====

        /// <summary>
        /// Lý do quản trị viên chốt THAY Hiring Manager (tin có HM mà người chốt không phải HM đó).
        /// Bắt buộc tối thiểu 10 ký tự trong trường hợp đó; bỏ qua ở mọi trường hợp khác.
        /// </summary>
        public string? FallbackReason { get; set; }

        /// <summary>Đề xuất cấp bậc + dải lương của người chốt — dùng để điền sẵn thư mời nhận việc.</summary>
        public string? SuggestedLevel { get; set; }
        public decimal? SuggestedSalaryMin { get; set; }
        public decimal? SuggestedSalaryMax { get; set; }
        public string? SuggestedSalaryCurrency { get; set; }

        /// <summary>Điểm mạnh / điểm cần lưu ý về ứng viên.</summary>
        public string? Strengths { get; set; }
        public string? Concerns { get; set; }
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

        // ===== ADR-061 =====

        /// <summary>
        /// Ảnh chụp vai trò người chốt tại thời điểm chốt (<c>hiring_manager|hr_admin|super_admin</c>).
        /// Ảnh chụp chứ không join ngược <c>users.role</c>: vai trò của một người đổi được về sau,
        /// còn câu hỏi "ai đã chốt tuyển người này, với tư cách gì" thì phải trả lời được mãi mãi.
        /// </summary>
        public string? ReviewerRole { get; set; }

        /// <summary>Quản trị viên đã chốt THAY Hiring Manager của tin.</summary>
        public bool IsHrFallback { get; set; }
        public string? FallbackReason { get; set; }

        /// <summary>Đề xuất cấp bậc + dải lương — nguồn điền sẵn cho thư mời nhận việc.</summary>
        public string? SuggestedLevel { get; set; }
        public decimal? SuggestedSalaryMin { get; set; }
        public decimal? SuggestedSalaryMax { get; set; }
        public string? SuggestedSalaryCurrency { get; set; }

        public string? Strengths { get; set; }
        public string? Concerns { get; set; }

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
                UpdatedAt = review.UpdatedAt,
                ReviewerRole = review.ReviewerRole,
                IsHrFallback = review.IsHrFallback,
                FallbackReason = review.FallbackReason,
                SuggestedLevel = review.SuggestedLevel,
                SuggestedSalaryMin = review.SuggestedSalaryMin,
                SuggestedSalaryMax = review.SuggestedSalaryMax,
                SuggestedSalaryCurrency = review.SuggestedSalaryCurrency,
                Strengths = review.Strengths,
                Concerns = review.Concerns
            };
    }

    /// <summary>Điểm một tiêu chí kèm nhãn + trọng số tại thời điểm chấm (ADR-060).</summary>
    public class CriterionScoreDto
    {
        public string Key { get; set; } = string.Empty;
        public decimal Score { get; set; }
        /// <summary>Tên hiển thị do doanh nghiệp đặt. Null với bản đánh giá cũ (dạng JSON phẳng).</summary>
        public string? Label { get; set; }
        /// <summary>Trọng số (%) của tiêu chí. Null với bản đánh giá cũ.</summary>
        public decimal? Weight { get; set; }
    }

    public class EvaluationDetailResponse
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public Guid ApplicationId { get; set; }
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public string AiVerdict { get; set; } = string.Empty;
        public decimal? OverallScore { get; set; }
        public Dictionary<string, decimal>? CriterionScores { get; set; }

        /// <summary>
        /// Điểm từng tiêu chí kèm NHÃN và TRỌNG SỐ tại thời điểm chấm (ADR-060) — có giá trị khi tin
        /// đã khai bộ tiêu chí. Giữ song song <see cref="CriterionScores"/> (chỉ điểm) để giao diện cũ
        /// và các bản đánh giá trước ADR-060 không vỡ.
        /// </summary>
        public List<CriterionScoreDto>? CriterionDetails { get; set; }

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

        /// <summary>
        /// Điểm khớp CV-JD do Gemini chấm (ADR-030), lấy từ `cv_jd_analyses` gắn với hồ sơ.
        /// Null = hồ sơ nộp trước khi có phân tích (không chạy lại — kết quả dùng 1 lần per CV+JD).
        /// Trước đây màn đánh giá vẽ cứng số 87 nên nhân sự đọc phải một con số bịa.
        /// </summary>
        public int? CvMatchScore { get; set; }
        /// <summary>Tóm tắt của cùng bản phân tích CV-JD — hiện kèm điểm để biết điểm đó từ đâu ra.</summary>
        public string? CvMatchSummary { get; set; }

        // ===== Ai là người được chốt kết quả này (ADR-061) =====
        // Handler điền, không phải FromEntity: cần đọc bảng đội tuyển dụng. Giao diện dùng để
        // quyết định hiện nút "Chốt kết quả" hay banner "đang chờ Hiring Manager chốt".

        /// <summary>Tin có Hiring Manager chính không. False = mọi thứ như trước ADR-061.</summary>
        public bool RequiresHmApproval { get; set; }
        public Guid? HiringManagerUserId { get; set; }
        public string? HiringManagerName { get; set; }

        public static EvaluationDetailResponse FromEntity(
            Evaluation eval, 
            ARI.Domain.Entities.Application app, 
            JobPosting job, 
            HrReview? hrReview)
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            // Đọc CẢ HAI dạng JSON điểm tiêu chí (ADR-060): dạng phẳng cũ {"technical":88} và dạng có
            // ảnh chụp {"technical":{"score":88,"label":…,"weight":…}}. Bản cũ deserialize thẳng sang
            // Dictionary<string,decimal> nên gặp dạng mới là ném lỗi rồi bỏ trống — HR mất bảng điểm.
            var criterionViews = ScoringRubricSupport.ParseForDisplay(eval.CriterionScores);
            Dictionary<string, decimal>? parsedCriteria = criterionViews.Count == 0
                ? null
                : criterionViews.ToDictionary(c => c.Key, c => c.Score);
            List<CriterionScoreDto>? criterionDetails = criterionViews.Count == 0
                ? null
                : criterionViews
                    .Select(c => new CriterionScoreDto
                    {
                        Key = c.Key,
                        Score = c.Score,
                        Label = c.Label,
                        Weight = c.Weight,
                    })
                    .ToList();

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
                CriterionDetails = criterionDetails,
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
                JobPostingId = job.Id,
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
