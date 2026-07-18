using System;

namespace ARISP.Domain.Entities
{
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
}
