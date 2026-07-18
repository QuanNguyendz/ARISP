using System;

namespace ARISP.Domain.Entities
{
    public class OnlineTestSubmission
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string SelectedAnswers { get; set; } = "{}"; // JSONB – e.g. {"q_uuid_1": 2, "q_uuid_2": 0}
        public decimal Score { get; set; }
        public bool IsPassed { get; set; } = false;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
