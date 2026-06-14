using System;
using System.Collections.Generic;

namespace ARISP.Domain.Entities
{
    public class JobPosting : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CreatedByUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string? JdFileUrl { get; set; }      // URL file JD gốc (PDF/DOCX) – Gemini ưu tiên file này
        public string? JdFileName { get; set; }     // tên file gốc
        public string? JdFileFormat { get; set; }   // pdf | docx
        public string InterviewMode { get; set; } = "remote"; // remote | onsite | both
        public string Status { get; set; } = "draft"; // draft | pending | active | rejected | closed | archived
        public string? RejectionReason { get; set; }
        public bool IsPublicListing { get; set; } = false;
        public string? DetectedLanguage { get; set; }
        public string? LanguageRequirement { get; set; }
        public bool LanguageConfirmed { get; set; } = false;
        public int? RescheduleDeadlineHours { get; set; } = 24;
        public int InviteTokenTtlHours { get; set; } = 48;
        public string? ScoringRubric { get; set; } // JSON array of criteria
        public string? PersonaName { get; set; }
        public string? PersonaVoiceId { get; set; }
        public string? PersonaStyle { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; }
        public bool? SalaryIsNegotiable { get; set; } = false;
        public string? EmploymentType { get; set; }
        public string? ExperienceLevel { get; set; }
        public List<string>? Skills { get; set; } = new List<string>();
        public string? JobCategory { get; set; }
        public DateTimeOffset? ApplicationDeadline { get; set; }
        public bool? IsUrgent { get; set; } = false;
    }
}
