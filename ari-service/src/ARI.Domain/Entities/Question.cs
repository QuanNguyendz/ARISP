using System;

namespace ARISP.Domain.Entities
{
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
}
