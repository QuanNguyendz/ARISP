using System;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Bản mô tả công việc do Recruiter soạn theo mẫu công ty, cho MỘT phiếu yêu cầu tuyển dụng
    /// (ADR-064).
    ///
    /// <b>Vì sao lưu NỘI DUNG chứ không chỉ xuất file rồi quên:</b> Hiring Manager từ chối ký duyệt
    /// kèm lý do là luồng đã có (<c>JobPosting.HmSignOffReason</c>, ADR-061). Nếu chỉ giữ file đã
    /// xuất thì mỗi lần bị trả về, Recruiter phải soạn lại từ đầu — đúng thứ khiến vòng sửa–duyệt
    /// trở nên đắt đỏ tới mức người ta né tránh nó.
    ///
    /// Nối một chiều tới phiếu; tin tuyển dụng KHÔNG trỏ ngược về đây mà chỉ giữ
    /// <c>JobPosting.JdFileUrl</c> — cùng lý lẽ một-chiều của ADR-063.
    /// </summary>
    public class JdDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Phiếu sinh ra bản JD này. Mỗi phiếu chỉ một bản (unique index có filter).</summary>
        public Guid RecruitmentRequestId { get; set; }

        // ===== Phần thông tin nhanh, in thành bảng ở đầu JD =====
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? EmploymentType { get; set; }
        public string? WorkMode { get; set; }
        public string? Location { get; set; }
        public string? ExperienceLevel { get; set; }
        public int? Vacancies { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; } = "VND";
        public DateTimeOffset? ApplicationDeadline { get; set; }

        /// <summary>
        /// Nội dung từng mục — JSON đối tượng <c>{"description":"…","requirements":"…"}</c>, khoá là
        /// <c>key</c> của mục trong <see cref="JdTemplate.SectionsJson"/>. Mục bị tắt trong mẫu vẫn
        /// giữ nội dung ở đây: bật lại là có ngay, không mất công gõ lại.
        /// </summary>
        public string SectionsJson { get; set; } = "{}";

        // ===== File đã xuất gần nhất =====
        /// <summary>storageKey của file JD đã sinh — chính file được gắn vào tin và HM mở ra để duyệt.</summary>
        public string? GeneratedFileStorageKey { get; set; }
        public string? GeneratedFileName { get; set; }

        /// <summary>pdf | docx</summary>
        public string? GeneratedFormat { get; set; }
        public DateTimeOffset? GeneratedAt { get; set; }

        public Guid CreatedByUserId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
