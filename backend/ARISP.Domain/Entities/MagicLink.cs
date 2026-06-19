using System;
using ARISP.Domain.Constants;

namespace ARISP.Domain.Entities
{
    public class MagicLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// Phân biệt loại tài khoản sở hữu token: "candidate" (mặc định) hoặc "staff" (HR/Recruiter/Super Admin).
        /// Tránh token reset của hai cổng đăng nhập tách biệt dùng nhầm lẫn nhau.
        /// </summary>
        public string Audience { get; set; } = MagicLinkAudience.Candidate;

        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? UsedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
