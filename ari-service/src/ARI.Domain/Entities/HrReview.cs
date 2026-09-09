using System;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Quyết định CHỐT trên một báo cáo đánh giá của AI.
    ///
    /// Từ ADR-061, người chốt mặc định là <b>Hiring Manager</b> của tin chứ không phải HR Leader:
    /// trong ATS, HR sở hữu quy trình và tuân thủ, còn quyết định tuyển hay không thuộc về trưởng
    /// bộ phận sẽ làm việc cùng ứng viên. HR Admin giữ vai DỰ PHÒNG (tin chưa gán HM, hoặc HM vắng
    /// — khi đó phải nhập lý do và bị ghi nhận là chốt thay).
    ///
    /// Tên bảng/lớp giữ nguyên <c>HrReview</c>: sự TỒN TẠI của một dòng ở đây là state chịu tải ở
    /// bốn nơi (khoá chấm lại, badge "chờ duyệt", trạng thái danh sách, cổng chia sẻ cho ứng viên).
    /// Đổi người ghi thì bốn hành vi đó vẫn đúng nguyên; tạo entity thứ hai mới là thứ làm vỡ chúng.
    /// </summary>
    public class HrReview
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid EvaluationId { get; set; }
        public Guid ReviewedByUserId { get; set; }
        public string FinalVerdict { get; set; } = "not_pass"; // pass | not_pass
        public bool IsOverride { get; set; } = false;
        public string? OverrideReason { get; set; }

        // ===== Ai đã chốt (ADR-061) =====

        /// <summary>
        /// Ảnh chụp vai trò của người chốt tại thời điểm chốt (<c>hiring_manager</c> |
        /// <c>hr_admin</c> | <c>super_admin</c>). Chụp lại chứ không join ngược <c>users.role</c>
        /// vì vai trò của một người có thể đổi, còn câu hỏi "ai đã quyết định tuyển người này, với
        /// tư cách gì" thì phải trả lời được mãi về sau.
        /// </summary>
        public string? ReviewerRole { get; set; }

        /// <summary>Quản trị viên chốt THAY trên tin vốn CÓ Hiring Manager.</summary>
        public bool IsHrFallback { get; set; } = false;

        /// <summary>Lý do chốt thay — bắt buộc khi <see cref="IsHrFallback"/>.</summary>
        public string? FallbackReason { get; set; }

        // ===== Đề xuất của người chốt, dùng để điền sẵn thư mời nhận việc (Phase 5) =====

        public string? SuggestedLevel { get; set; }
        public decimal? SuggestedSalaryMin { get; set; }
        public decimal? SuggestedSalaryMax { get; set; }
        public string? SuggestedSalaryCurrency { get; set; }

        /// <summary>Điểm mạnh / điểm cần lưu ý — nuôi nội dung thư offer và trang chi tiết ứng viên.</summary>
        public string? Strengths { get; set; }
        public string? Concerns { get; set; }
        public bool ShareRecording { get; set; } = false;
        public bool ShareTranscript { get; set; } = false;
        public bool ShareEvaluation { get; set; } = false;
        public bool ShareFeedback { get; set; } = false;
        public string? CandidateFeedback { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
