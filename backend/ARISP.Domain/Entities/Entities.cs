using System;
using System.Collections.Generic;

namespace ARISP.Domain.Entities
{
    public class User : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string Role { get; set; } = "recruiter"; // super_admin | hr_admin | recruiter
        public string? FullName { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset? LastLoginAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }

    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class MagicLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string TokenHash { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? UsedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class CandidateAccount : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Headline { get; set; }
        public string? ProfileCvUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public bool EmailVerified { get; set; } = false;
        public DateTimeOffset? LastLoginAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }

    public class CandidateRefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CandidateAccountId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class JobPosting : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CreatedByUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string InterviewMode { get; set; } = "remote"; // remote | onsite | both
        public string Status { get; set; } = "draft"; // draft | active | closed | archived
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
    }

    public class InterviewRoundConfig
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; }
        public string RoundType { get; set; } = "screening"; // screening | technical | hr | culture_fit
        public string? InterviewLanguage { get; set; }
        public int InterviewCodeTtlHours { get; set; } = 2;
        public int MaxDurationMinutes { get; set; } = 45;
    }

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
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }

    public class AvailabilitySlot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public int Capacity { get; set; } = 1;
        public int BookedCount { get; set; } = 0;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class InterviewBooking
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public Guid AvailabilitySlotId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string? InterviewLink { get; set; }
        public string Status { get; set; } = "scheduled"; // scheduled | completed | cancelled | rescheduled
        public bool Reminder24hSent { get; set; } = false;
        public bool Reminder1hSent { get; set; } = false;
        public Guid? RescheduledFromId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class InterviewCode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string Code { get; set; } = string.Empty; // ARX-7K2P
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? UsedAt { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class InterviewSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string RoundType { get; set; } = string.Empty;
        public string SessionType { get; set; } = "real"; // practice | real
        public string InterviewLanguage { get; set; } = "vi";
        public string Status { get; set; } = "pending"; // pending | active | completed | aborted | error
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int? DurationSeconds { get; set; }
        public string? RecordingUrl { get; set; }
        public bool RecordingVisibleToCandidate { get; set; } = false;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class Question
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public int SequenceNumber { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? QuestionType { get; set; } // behavioral | technical | language_probe | scenario
        public int DifficultyLevel { get; set; } = 3;
        public string Source { get; set; } = "ai_generated"; // ai_generated | playbook_must_ask | playbook_suggested
        public Guid? PlaybookChunkId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class Answer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid QuestionId { get; set; }
        public Guid SessionId { get; set; }
        public string? Transcript { get; set; }
        public string? AudioUrl { get; set; }
        public int? ResponseTimeMs { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class DocumentChunk
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SourceType { get; set; } = string.Empty; // jd | cv | playbook
        public Guid SourceId { get; set; }
        public int ChunkIndex { get; set; }
        public string ChunkText { get; set; } = string.Empty;
        public float[]? Embedding { get; set; } // pgvector representation (size 1536)
        public string? Metadata { get; set; } // JSON format
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class PlaybookDocument : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Scope { get; set; } = "org"; // org | job_posting | round
        public Guid? ScopeRefId { get; set; }
        public int? RoundNumber { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileFormat { get; set; } = "txt"; // pdf | docx | txt | md | json
        public string? ParsedText { get; set; }
        public string Status { get; set; } = "processing"; // processing | ready | error
        public string? ErrorMessage { get; set; }
        public Guid UploadedByUserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }

    public class MustAskTracking
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public Guid PlaybookDocumentId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public DateTimeOffset? AskedAt { get; set; }
        public Guid? QuestionId { get; set; }
    }

    public class Evaluation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; }
        public string SessionType { get; set; } = "real"; // practice | real
        public string AiVerdict { get; set; } = "not_pass"; // pass | not_pass
        public decimal? OverallScore { get; set; }
        public string? CriterionScores { get; set; } // JSON
        public string? Reasoning { get; set; }
        public string? RecommendedNextStep { get; set; }
        public string? QuestionAnalyses { get; set; } // JSON array
        public decimal? CheatScore { get; set; }
        public string? CheatSignals { get; set; } // JSON array
        public string? LanguageAssessment { get; set; } // JSON
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class HrReview
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid EvaluationId { get; set; }
        public Guid ReviewedByUserId { get; set; }
        public string FinalVerdict { get; set; } = "not_pass"; // pass | not_pass
        public bool IsOverride { get; set; } = false;
        public string? OverrideReason { get; set; }
        public bool ShareRecording { get; set; } = false;
        public bool ShareTranscript { get; set; } = false;
        public bool ShareEvaluation { get; set; } = false;
        public bool ShareFeedback { get; set; } = false;
        public string? CandidateFeedback { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class CheatDetectionSignal
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public string SignalType { get; set; } = string.Empty; // eye_tracking | response_timing | speech_pattern | tab_switch | focus_loss
        public string Payload { get; set; } = "{}"; // JSON payload
        public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? ActorUserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public Guid? EntityId { get; set; }
        public string? Metadata { get; set; } // JSON format
        public string? IpAddress { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class WebhookDelivery
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = "{}"; // JSON payload
        public int? ResponseStatus { get; set; }
        public string? ResponseBody { get; set; }
        public int AttemptCount { get; set; } = 1;
        public DateTimeOffset? DeliveredAt { get; set; }
        public DateTimeOffset? NextRetryAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
