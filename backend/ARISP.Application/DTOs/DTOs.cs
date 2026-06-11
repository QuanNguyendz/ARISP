using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARISP.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace ARISP.Application.DTOs
{
    // Auth DTOs
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class FirebaseAuthResponse : AuthResponse
    {
        public string FirebaseUid { get; set; } = string.Empty;
    }

    public class CandidateRegisterRequest
    {
        //[Required(ErrorMessage = "Email là bắt buộc.")]
        //[EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ.")]
        //public string Email { get; set; } = null!;

        //[Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        //[MinLength(8, ErrorMessage = "Mật khẩu phải có tối thiểu 8 ký tự.")]
        //[MaxLength(30, ErrorMessage = "Mật khẩu không được vượt quá 30 ký tự.")]
        //// Yêu cầu: Ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt
        //[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,30}$",
        //    ErrorMessage = "Mật khẩu phải bao gồm ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt.")]
        //public string Password { get; set; } = null!;

        //[Required(ErrorMessage = "Họ và tên là bắt buộc.")]
        //public string FullName { get; set; } = null!;

        //public string? Phone { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    // ==========================================
    // Job Posting DTOs
    // ==========================================

    /// <summary>
    /// DTO đại diện cho yêu cầu tạo một Job Posting (tin tuyển dụng) mới, kèm theo cấu hình các vòng phỏng vấn.
    /// </summary>
    public class CreateJobPostingRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string InterviewMode { get; set; } = "remote"; // remote | onsite | both
        public bool IsPublicListing { get; set; } = false;
        public string? LanguageRequirement { get; set; }
        public int? RescheduleDeadlineHours { get; set; } = 24;
        public int InviteTokenTtlHours { get; set; } = 48;
        public List<RoundConfigDto> RoundConfigs { get; set; } = new();
        public JsonElement? ScoringRubric { get; set; } // JSON
        public string? PersonaName { get; set; }
        public string? PersonaVoiceId { get; set; }
        public string? PersonaStyle { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; }
        public bool SalaryIsNegotiable { get; set; } = false;
        public string? EmploymentType { get; set; }
        public string? ExperienceLevel { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? JobCategory { get; set; }
        public DateTimeOffset? ApplicationDeadline { get; set; }
        public bool IsUrgent { get; set; } = false;
    }

    /// <summary>
    /// DTO cấu hình thông tin chi tiết cho từng vòng phỏng vấn (Interview Round) thuộc Job Posting.
    /// </summary>
    public class RoundConfigDto
    {
        public int RoundNumber { get; set; }
        public string RoundType { get; set; } = "screening";
        public string? InterviewLanguage { get; set; }
        public int InterviewCodeTtlHours { get; set; } = 2;
        public int MaxDurationMinutes { get; set; } = 45;

        public static RoundConfigDto FromEntity(InterviewRoundConfig config) =>
            new()
            {
                RoundNumber = config.RoundNumber,
                RoundType = config.RoundType,
                InterviewLanguage = config.InterviewLanguage,
                InterviewCodeTtlHours = config.InterviewCodeTtlHours,
                MaxDurationMinutes = config.MaxDurationMinutes
            };
    }

    /// <summary>
    /// DTO chi tiết thông tin Job Posting trả về cho client sau khi tạo hoặc lấy chi tiết.
    /// </summary>
    public class JobPostingResponse
    {
        public Guid Id { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string InterviewMode { get; set; } = "remote";
        public string Status { get; set; } = "draft";
        public bool IsPublicListing { get; set; }
        public string? DetectedLanguage { get; set; }
        public string? LanguageRequirement { get; set; }
        public bool LanguageConfirmed { get; set; }
        public List<RoundConfigDto> RoundConfigs { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; }
        public bool SalaryIsNegotiable { get; set; }
        public string? EmploymentType { get; set; }
        public string? ExperienceLevel { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? JobCategory { get; set; }
        public DateTimeOffset? ApplicationDeadline { get; set; }
        public bool IsUrgent { get; set; }
        public JsonElement? ScoringRubric { get; set; }

        public static JobPostingResponse FromEntity(JobPosting job, List<RoundConfigDto> roundConfigs) =>
            new()
            {
                Id = job.Id,
                CreatedByUserId = job.CreatedByUserId,
                Title = job.Title,
                Department = job.Department,
                JobDescription = job.JobDescription,
                InterviewMode = job.InterviewMode,
                Status = job.Status,
                IsPublicListing = job.IsPublicListing,
                DetectedLanguage = job.DetectedLanguage,
                LanguageRequirement = job.LanguageRequirement,
                LanguageConfirmed = job.LanguageConfirmed,
                RoundConfigs = roundConfigs,
                CreatedAt = job.CreatedAt,
                Location = job.Location,
                WorkMode = job.WorkMode,
                SalaryMin = job.SalaryMin,
                SalaryMax = job.SalaryMax,
                SalaryCurrency = job.SalaryCurrency,
                SalaryIsNegotiable = job.SalaryIsNegotiable ?? false,
                EmploymentType = job.EmploymentType,
                ExperienceLevel = job.ExperienceLevel,
                Skills = job.Skills ?? new List<string>(),
                JobCategory = job.JobCategory,
                ApplicationDeadline = job.ApplicationDeadline,
                IsUrgent = job.IsUrgent ?? false,
                ScoringRubric = !string.IsNullOrEmpty(job.ScoringRubric) ? JsonSerializer.Deserialize<JsonElement>(job.ScoringRubric, (JsonSerializerOptions?)null) : null
            };
    }

    /// <summary>
    /// DTO thông tin rút gọn của Job Posting để hiển thị danh sách trên Job Board công khai.
    /// </summary>
    public class JobPostingListItemResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string InterviewMode { get; set; } = "remote";
        public string Status { get; set; } = "draft";
        public string? DetectedLanguage { get; set; }
        public string? LanguageRequirement { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public string? EmploymentType { get; set; }
        public string? ExperienceLevel { get; set; }
        public string? JobCategory { get; set; }
        public bool IsUrgent { get; set; }

        public static JobPostingListItemResponse FromEntity(JobPosting job) =>
            new()
            {
                Id = job.Id,
                Title = job.Title,
                Department = job.Department,
                InterviewMode = job.InterviewMode,
                Status = job.Status,
                DetectedLanguage = job.DetectedLanguage,
                LanguageRequirement = job.LanguageRequirement,
                CreatedAt = job.CreatedAt,
                PublishedAt = job.PublishedAt,
                Location = job.Location,
                WorkMode = job.WorkMode,
                EmploymentType = job.EmploymentType,
                ExperienceLevel = job.ExperienceLevel,
                JobCategory = job.JobCategory,
                IsUrgent = job.IsUrgent ?? false
            };
    }

    /// <summary>
    /// DTO yêu cầu thiết lập khung giờ rảnh (Availability Slots) cho vòng phỏng vấn của Job Posting.
    /// </summary>
    public class CreateAvailabilitySlotRequest
    {
        public int RoundNumber { get; set; } = 1;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public int Capacity { get; set; } = 1;
    }

    // Candidate Application DTOs
    public class SubmitApplicationRequest
    {
        public Guid JobPostingId { get; set; }
        public Guid? CandidateAccountId { get; set; }
        public string CandidateEmail { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidatePhone { get; set; }
        public string? CvFileUrl { get; set; }
        public string? CvText { get; set; }
    }

    public class ApplicationResponse
    {
        public Guid Id { get; set; }
        public Guid JobPostingId { get; set; }
        public string? JobTitle { get; set; }
        public string CandidateEmail { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidatePhone { get; set; }
        public string? CvFileUrl { get; set; }
        public string? CvText { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool PracticeSessionUsed { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    // Interview Session DTOs
    public class StartSessionRequest
    {
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string SessionType { get; set; } = "real"; // practice | real
    }

    public class StartSessionResponse
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = "active";
        public string Language { get; set; } = "vi";
        public string? HeyGenSdpOffer { get; set; }
        public string? HeyGenSessionId { get; set; }
    }

    public class SubmitAnswerRequest
    {
        public Guid QuestionId { get; set; }
        public string Transcript { get; set; } = string.Empty;
        public int? ResponseTimeMs { get; set; }
    }

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
    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class GenerateCodeRequest
    {
        public Guid ApplicationId { get; set; }
        public int? RoundNumber { get; set; }
    }

    public class GenerateBatchRequest
    {
        public List<Guid> ApplicationIds { get; set; } = new();
        public int? RoundNumber { get; set; }
    }

    public class ValidateCodeRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public class InterviewCodeSummaryDto
    {
        public string Code { get; set; } = string.Empty;
        public int RoundNumber { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? UsedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
    }

    // ==========================================
    // Evaluation DTOs
    // ==========================================

    public class CriterionScoreDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; } = 100; // default to 100
        public string Reasoning { get; set; } = string.Empty;
    }

    public class QuestionAnalysisDto
    {
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

        public static EvaluationDetailResponse FromEntity(
            Evaluation eval, 
            ARISP.Domain.Entities.Application app, 
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
            ARISP.Domain.Entities.Application app,
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

    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // Refresh Token DTOs
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response cho GET /api/auth/me – match FE User type { id, email, name, role }
    /// </summary>
    public class UserMeResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        public string? RefreshToken { get; set; }
    }

    // Magic Link DTOs
    public class SendMagicLinkRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        public Guid? ApplicationId { get; set; }
    }

    public class UpdateApplicationStatusRequest
    {
        [Required(ErrorMessage = "Trạng thái (status) là bắt buộc.")]
        public string Status { get; set; } = string.Empty;
    }
}
