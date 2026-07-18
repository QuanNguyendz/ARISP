using System;

namespace ARISP.Domain.Entities
{
    /// <summary>
    /// Lời mời phỏng vấn theo từng vòng (magic link phạm vi hẹp): cho phép ứng viên mở trang
    /// chọn lịch + phỏng vấn thử trên thiết bị cá nhân mà không cần đăng nhập mật khẩu.
    /// Token thật gửi qua email; DB chỉ lưu hash (SHA256). Một lời mời / (application, round).
    /// </summary>
    public class InterviewInvite
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string TokenHash { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        /// <summary>Thời điểm ứng viên đã chọn lịch (null = chưa đặt lịch).</summary>
        public DateTimeOffset? ScheduledAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
