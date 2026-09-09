using System;
using ARI.Domain.Constants;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Một người thuộc ĐỘI TUYỂN DỤNG của một tin (ADR-061).
    ///
    /// Đây là trục phân quyền thứ hai của hệ thống, bên cạnh <c>JobPosting.CreatedByUserId</c>
    /// (chủ tin). Cố ý gán <b>theo từng tin</b> chứ không theo phòng ban: <c>users.department</c>
    /// và <c>job_postings.department</c> là chuỗi text tự do — trên tin thì do Gemini trích từ JD
    /// (ADR-042) — nên lấy nó làm ranh giới truy cập là dựng cổng bảo mật trên dữ liệu không ai
    /// kiểm soát. Department chỉ dùng để GỢI Ý ai nên được gán, lúc nhân sự bấm nút.
    ///
    /// Cách các ATS thật làm cũng vậy: Greenhouse gán Hiring Manager ở tab Job Setup của từng tin.
    /// </summary>
    public class JobHiringTeamMember : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid JobPostingId { get; set; }

        /// <summary>Tài khoản nhân sự nội bộ được gán vào tin.</summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Vai trò TRONG TIN NÀY — xem <see cref="JobTeamRoles"/>. Là chuỗi chứ không phải enum,
        /// đồng bộ với mọi cột trạng thái khác trong hệ (Application.Status, JobPosting.Status,
        /// AccountRequest.Status): thêm vai trò mới về sau không tốn migration.
        /// </summary>
        public string RoleOnJob { get; set; } = JobTeamRoles.HiringManager;

        /// <summary>
        /// Hiring Manager CHÍNH của tin — người mà chữ ký duyệt chặn phễu và là người chốt kết quả
        /// phỏng vấn. Ràng buộc UNIQUE có lọc ở DB bảo đảm mỗi tin nhiều nhất một người.
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>Ai đã gán (chủ tin hoặc quản trị viên) — giữ dấu vết cho kiểm toán.</summary>
        public Guid AddedByUserId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
