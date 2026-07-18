using System;

namespace ARISP.Domain.Entities
{
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
}
