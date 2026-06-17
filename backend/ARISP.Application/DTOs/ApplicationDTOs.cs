using System;
using System.ComponentModel.DataAnnotations;

namespace ARISP.Application.DTOs
{
    public class SubmitApplicationRequest
    {
        public Guid JobPostingId { get; set; }
        public Guid? CandidateAccountId { get; set; }
        public string CandidateEmail { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidatePhone { get; set; }
        public string? CvFileUrl { get; set; }
        public string? CvText { get; set; }
        public string? CvFileHash { get; set; }
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
        public Guid? CvJdAnalysisId { get; set; }
    }

    public class UpdateApplicationStatusRequest
    {
        [Required(ErrorMessage = "Trạng thái (status) là bắt buộc.")]
        public string Status { get; set; } = string.Empty;
    }
}
