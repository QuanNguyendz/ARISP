using System;

namespace ARISP.Domain.Entities
{
    /// <summary>
    /// Thông báo cho ứng viên HOẶC nhân sự nội bộ (HR Admin / Recruiter / Super Admin).
    /// Được đồng bộ (sync) từ các sự kiện thực tế của người nhận qua <c>DedupKey</c> để không
    /// trùng lặp; trạng thái đã đọc được lưu bền. Đúng một trong hai khóa người nhận được set:
    /// <c>CandidateAccountId</c> (ứng viên) hoặc <c>RecipientUserId</c> (nhân sự nội bộ).
    /// </summary>
    public class Notification : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Người nhận là ứng viên (null nếu thông báo dành cho nhân sự nội bộ).</summary>
        public Guid? CandidateAccountId { get; set; }

        /// <summary>Người nhận là nhân sự nội bộ — <c>User.Id</c> (null nếu thông báo dành cho ứng viên).</summary>
        public Guid? RecipientUserId { get; set; }

        /// <summary>Khóa chống trùng theo sự kiện, ví dụ "result:{evaluationId}". Duy nhất theo người nhận.</summary>
        public string DedupKey { get; set; } = string.Empty;

        /// <summary>invite | result | pending | schedule | applied | approval | system</summary>
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
