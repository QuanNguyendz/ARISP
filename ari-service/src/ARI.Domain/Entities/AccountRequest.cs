using System;

namespace ARISP.Domain.Entities
{
    /// <summary>
    /// Yêu cầu tạo tài khoản staff do HR Leader gửi lên Super Admin phê duyệt.
    /// Mỗi dòng = 1 tài khoản đề xuất; tạo hàng loạt (CSV) sẽ tạo nhiều dòng cùng BatchId.
    /// Khi Super Admin duyệt → tạo User active tương ứng và liên kết qua CreatedUserId.
    /// </summary>
    public class AccountRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Gom nhóm các tài khoản cùng một lần gửi (bulk). Null nếu gửi lẻ.</summary>
        public Guid? BatchId { get; set; }

        /// <summary>HR Leader đã tạo yêu cầu.</summary>
        public Guid RequestedByUserId { get; set; }

        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        /// <summary>hr_admin | recruiter</summary>
        public string Role { get; set; } = "recruiter";
        public string? Department { get; set; }

        /// <summary>pending | approved | rejected</summary>
        public string Status { get; set; } = "pending";

        /// <summary>Lý do từ chối (bắt buộc khi reject) hoặc ghi chú khi duyệt.</summary>
        public string? ReviewReason { get; set; }

        public Guid? ReviewedByUserId { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>User được tạo khi yêu cầu được duyệt.</summary>
        public Guid? CreatedUserId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
