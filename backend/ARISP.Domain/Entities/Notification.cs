using System;

namespace ARISP.Domain.Entities
{
    /// <summary>
    /// Thông báo cho ứng viên. Được đồng bộ (sync) từ các sự kiện thực tế của ứng viên
    /// (lời mời phỏng vấn, kết quả vòng đã chia sẻ, lịch sắp tới, nộp hồ sơ…) qua
    /// <c>DedupKey</c> để không trùng lặp; trạng thái đã đọc được lưu bền.
    /// </summary>
    public class Notification : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CandidateAccountId { get; set; }

        /// <summary>Khóa chống trùng theo sự kiện, ví dụ "result:{evaluationId}". Duy nhất theo ứng viên.</summary>
        public string DedupKey { get; set; } = string.Empty;

        /// <summary>invite | result | pending | schedule | applied | system</summary>
        public string Type { get; set; } = "system";
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public string? Link { get; set; }
        public bool IsRead { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
