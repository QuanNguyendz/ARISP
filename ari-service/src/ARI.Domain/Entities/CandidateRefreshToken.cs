using System;

namespace ARISP.Domain.Entities
{
    public class CandidateRefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CandidateAccountId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
