using System;
using System.Collections.Generic;
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

    // Job Posting DTOs
    public class CreateJobPostingRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string InterviewMode { get; set; } = "remote"; // remote | onsite | both
        public bool IsPublicListing { get; set; } = false;
        public List<RoundConfigDto> RoundConfigs { get; set; } = new();
        public string? ScoringRubric { get; set; } // JSON
        public string? PersonaName { get; set; }
        public string? PersonaVoiceId { get; set; }
        public string? PersonaStyle { get; set; }
    }

    public class RoundConfigDto
    {
        public int RoundNumber { get; set; }
        public string RoundType { get; set; } = "screening";
        public string? InterviewLanguage { get; set; }
        public int InterviewCodeTtlHours { get; set; } = 2;
        public int MaxDurationMinutes { get; set; } = 45;
    }

    public class JobPostingResponse
    {
        public Guid Id { get; set; }
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
    }

    // Candidate Application DTOs
    public class SubmitApplicationRequest
    {
        public Guid JobPostingId { get; set; }
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
        public string CandidateEmail { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
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
}
