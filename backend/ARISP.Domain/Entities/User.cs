using System;

namespace ARISP.Domain.Entities
{
    public class User : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string Role { get; set; } = "recruiter"; // super_admin | hr_admin | recruiter
        public string? FullName { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Lý do tài khoản bị khóa (set khi Super Admin khóa). Null nếu đang hoạt động.</summary>
        public string? LockReason { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
