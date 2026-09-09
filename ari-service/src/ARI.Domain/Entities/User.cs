using System;

namespace ARI.Domain.Entities
{
    public class User : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string Role { get; set; } = "recruiter"; // super_admin | hr_admin | recruiter
        public string? FullName { get; set; }

        /// <summary>
        /// Đội/bộ phận của nhân viên (ADR-065). Thay cho cột <c>department</c> chuỗi tự do trước đây.
        ///
        /// <b>Chỉ Super Admin đặt được</b> — cố ý bỏ khỏi trang Cài đặt cá nhân: khi nhân viên tự sửa
        /// được, ô "đội" khoá cứng trên phiếu yêu cầu tuyển dụng chỉ là hình thức.
        ///
        /// Cố ý KHÔNG giữ song song một cột tên đội "cho tiện": hai nguồn cho cùng một sự thật là
        /// đúng kiểu trôi lệch mà ADR-058 đã phải đi chữa. Tên đội tra bằng join lúc đọc.
        /// </summary>
        public Guid? DepartmentId { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Lý do tài khoản bị khóa (set khi Super Admin khóa). Null nếu đang hoạt động.</summary>
        public string? LockReason { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
        public string? SettingsJson { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
