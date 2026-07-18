using System;

namespace ARISP.Domain.Entities
{
    public class OnlineTestQuestion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string Options { get; set; } = "[]"; // JSONB – e.g. ["A", "B", "C", "D"]
        public int CorrectOption { get; set; }       // 0-based index
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
