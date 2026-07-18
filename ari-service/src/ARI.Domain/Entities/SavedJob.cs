using System;

namespace ARISP.Domain.Entities
{
    /// <summary>
    /// Việc làm được ứng viên lưu (bookmark) trên Job Board. Quan hệ N-N giữa
    /// <see cref="CandidateAccount"/> và <see cref="JobPosting"/> — mỗi cặp là duy nhất.
    /// </summary>
    public class SavedJob : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CandidateAccountId { get; set; }
        public Guid JobPostingId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
