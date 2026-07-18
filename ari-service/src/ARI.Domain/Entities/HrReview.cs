using System;

namespace ARISP.Domain.Entities
{
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
}
