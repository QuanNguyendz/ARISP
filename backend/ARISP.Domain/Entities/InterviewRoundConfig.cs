using System;

namespace ARISP.Domain.Entities
{
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
}
