using System;

namespace ARISP.Domain.Entities
{
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
}
