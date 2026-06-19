using System;

namespace ARISP.Domain.Entities
{
    public class Application : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public Guid? CandidateAccountId { get; set; }
        public string CandidateEmail { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidatePhone { get; set; }
        public string? CvFileUrl { get; set; }
        public string? CvText { get; set; }
        public string Source { get; set; } = "invited"; // job_board | invited
        public string Status { get; set; } = "invited"; // invited | cv_submitted | screening | interview | pass | not_pass | withdrawn
        public string? InviteTokenHash { get; set; }
        public DateTimeOffset? InviteExpiresAt { get; set; }
        public bool PracticeSessionUsed { get; set; } = false;
        public string? DemographicData { get; set; } // JSON format
        public bool DemographicConsent { get; set; } = false;
        public Guid? CvJdAnalysisId { get; set; }
        public virtual CvJdAnalysis? CvJdAnalysis { get; set; } 
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
