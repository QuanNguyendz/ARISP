using System;

namespace ARISP.Domain.Entities
{
    public class MustAskTracking
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public Guid PlaybookDocumentId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public DateTimeOffset? AskedAt { get; set; }
        public Guid? QuestionId { get; set; }
    }
}
