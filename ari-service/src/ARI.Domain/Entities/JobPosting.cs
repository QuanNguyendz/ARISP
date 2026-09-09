using System;
using System.Collections.Generic;

namespace ARI.Domain.Entities
{
    public class JobPosting : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CreatedByUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string? JdFileUrl { get; set; }      // URL file JD gốc (PDF/DOCX) – Gemini ưu tiên file này
        public string? JdFileName { get; set; }     // tên file gốc
        public string? JdFileFormat { get; set; }   // pdf | docx
        public string InterviewMode { get; set; } = "remote"; // remote | onsite | both
        public string Status { get; set; } = "draft"; // draft | pending | active | rejected | closed | archived
        public string? RejectionReason { get; set; }
        public bool IsPublicListing { get; set; } = false;
        public string? DetectedLanguage { get; set; }
        public string? LanguageRequirement { get; set; }
        public bool LanguageConfirmed { get; set; } = false;
        public int? RescheduleDeadlineHours { get; set; } = 24;
        public int InviteTokenTtlHours { get; set; } = 48;
        public string? ScoringRubric { get; set; } // JSON array of criteria
        /// <summary>Điểm sàn (%) để ĐẠT bài thi trắc nghiệm online (Online Test) của job này. Mặc định 70.</summary>
        public int OnlineTestPassScore { get; set; } = 70;
        /// <summary>Số câu bốc ngẫu nhiên từ ngân hàng cho mỗi lượt thi. Mặc định 20.</summary>
        public int OnlineTestQuestionsPerTest { get; set; } = 20;
        /// <summary>Thời lượng làm bài (phút). Mặc định 30.</summary>
        public int OnlineTestDurationMinutes { get; set; } = 30;

        /// <summary>
        /// Điểm sàn (%) để AI kết luận ĐẠT một vòng phỏng vấn. Mặc định 70 — cùng mô hình với
        /// <see cref="OnlineTestPassScore"/>. Verdict suy từ điểm (ADR-060) chứ không để LLM tự phán;
        /// HR vẫn Override được (ADR-053).
        /// </summary>
        public int InterviewPassScore { get; set; } = 70;
        public string? PersonaName { get; set; }
        public string? PersonaVoiceId { get; set; }
        public string? PersonaStyle { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; }
        public bool? SalaryIsNegotiable { get; set; } = false;
        public string? EmploymentType { get; set; }
        public string? ExperienceLevel { get; set; }
        public List<string>? Skills { get; set; } = new List<string>();
        public string? JobCategory { get; set; }
        public DateTimeOffset? ApplicationDeadline { get; set; }
        public bool? IsUrgent { get; set; } = false;
        /// <summary>Số lượng cần tuyển (chỉ tiêu/headcount). Null = không giới hạn. Tuyển đủ → có thể đóng tin.</summary>
        public int? Vacancies { get; set; }

        // ===== Phê duyệt của HR Leader (approval) =====
        /// <summary>HR Leader/SuperAdmin đã duyệt tin (pending → active). Null = chưa duyệt.</summary>
        public Guid? ApprovedByUserId { get; set; }
        /// <summary>Thời điểm duyệt.</summary>
        public DateTimeOffset? ApprovedAt { get; set; }
        /// <summary>Tên người duyệt (snapshot tại thời điểm duyệt, để hiển thị & đóng dấu).</summary>
        public string? ApproverName { get; set; }
        /// <summary>storageKey của file JD đã đóng dấu duyệt (visual stamp). Chỉ tạo khi JD là PDF.</summary>
        public string? SignedJdFileUrl { get; set; }

        // ===== Chữ ký duyệt JD của Hiring Manager (ADR-061 — duyệt tin hai bước) =====
        //
        // Khai bằng CỘT chứ không thêm trạng thái vào vòng đời tin. Hai lý do: (1) ký duyệt và
        // vòng đời là hai trục vuông góc — tin có thể đã ký mà vẫn ở `draft`, `pending` hoặc
        // `rejected`; (2) `UpdateJobStatusCommand` là chuỗi if 459 dòng, `GetAdminJobsQuery` còn
        // viết lại status khi hiển thị, và FE mirror cả tập giá trị — mọi trạng thái mới phải
        // luồn qua từng chỗ đó. Đúng khuôn khối `ApprovedByUserId` ngay phía trên.

        /// <summary>
        /// pending | approved | rejected — xem <see cref="Constants.HmSignOffStatus"/>.
        /// <b>Null = tin không có Hiring Manager</b> → không có cổng nào, hành vi y hệt trước ADR-061.
        /// </summary>
        public string? HmSignOffStatus { get; set; }

        public Guid? HmSignOffByUserId { get; set; }
        public DateTimeOffset? HmSignOffAt { get; set; }

        /// <summary>Lý do từ chối JD (bắt buộc khi từ chối) — Recruiter sửa rồi gửi duyệt lại.</summary>
        public string? HmSignOffReason { get; set; }

        // ===== Phiếu yêu cầu tuyển dụng sinh ra tin này (ADR-063) =====

        /// <summary>
        /// Phiếu <see cref="RecruitmentRequest"/> đã được HR Leader duyệt và là nguồn gốc của tin.
        ///
        /// <b>Nullable trong lược đồ nhưng BẮT BUỘC với tin mới.</b> Cột để null được là để những
        /// tin có trước ADR-063 không bị migration làm hỏng — gán một phiếu giả cho dữ liệu cũ thì
        /// bịa ra một phiếu chưa ai từng duyệt, tệ hơn hẳn việc thừa nhận nó không có. Ràng buộc
        /// "mọi tin phải từ phiếu đã duyệt" cưỡng chế ở <c>CreateJobCommand</c>, nơi phân biệt được
        /// tin mới với dữ liệu lịch sử.
        /// </summary>
        public Guid? RecruitmentRequestId { get; set; }
    }
}
