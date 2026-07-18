using System;

namespace ARISP.Domain.Entities
{
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
}
