using System;

namespace ARI.Domain.Entities
{
    public class Application : ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public Guid? CandidateAccountId { get; set; }
        public string CandidateEmail { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidatePhone { get; set; }
        public string? CvFileUrl { get; set; }
        public string? CvText { get; set; }
        // Thông tin ứng viên nhập khi nộp hồ sơ qua Job Board (màn ứng tuyển).
        public string? DesiredLocation { get; set; } // Nơi làm việc mong muốn
        public string? CoverLetter { get; set; } // Thư giới thiệu / câu trả lời 1 (kinh nghiệm, kỹ năng, vì sao phù hợp)
        public string? NoticePeriod { get; set; } // Thời gian báo trước khi nghỉ việc (notice period)
        public string Source { get; set; } = "invited"; // job_board | invited
        /// <summary>Xem <see cref="Constants.ApplicationStatuses"/> — nơi khai đầy đủ vòng đời.</summary>
        public string Status { get; set; } = "invited";

        // ===== Cổng duyệt shortlist của Hiring Manager (ADR-061) =====
        //
        // Tách khỏi Status có chủ đích: Status nói hồ sơ đang Ở ĐÂU trong phễu, ba cột dưới nói
        // CỔNG ĐÃ MỞ CHƯA. Hồ sơ ở `hm_review` mà `HmDecision = approved` vẫn nằm nguyên chỗ cũ —
        // nhân sự xếp lịch được là vì cổng mở, không phải vì hồ sơ đã nhảy sang trạng thái khác.

        /// <summary>pending | approved | rejected | bypassed — xem <see cref="Constants.HmDecision"/>. Null = chưa gửi duyệt.</summary>
        public string? HmDecision { get; set; }

        /// <summary>Người đã quyết định (Hiring Manager, hoặc quản trị viên khi vượt cổng).</summary>
        public Guid? HmDecisionByUserId { get; set; }

        public DateTimeOffset? HmDecidedAt { get; set; }

        /// <summary>Lý do từ chối, hoặc lý do vượt cổng (bắt buộc khi <c>bypassed</c>/<c>rejected</c>).</summary>
        public string? HmDecisionNote { get; set; }

        public string? InviteTokenHash { get; set; }
        public DateTimeOffset? InviteExpiresAt { get; set; }
        public bool PracticeSessionUsed { get; set; } = false;
        public string? DemographicData { get; set; } // JSON format
        public bool DemographicConsent { get; set; } = false;
        public Guid? CvJdAnalysisId { get; set; }
        public virtual CvJdAnalysis? CvJdAnalysis { get; set; } 
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
