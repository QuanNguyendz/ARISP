using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ARI.Application.DTOs
{
    /// <summary>Điểm một tiêu chí chấm CV kèm nhãn + trọng số tại thời điểm chấm (ADR-060).</summary>
    public class CvCriterionScoreDto
    {
        public string Key { get; set; } = string.Empty;
        public decimal Score { get; set; }
        /// <summary>Tên hiển thị doanh nghiệp đặt. Null với bản phân tích cũ (dạng JSON phẳng).</summary>
        public string? Label { get; set; }
        /// <summary>Trọng số (%). Null với bản phân tích cũ.</summary>
        public decimal? Weight { get; set; }
    }

    public class SubmitApplicationRequest
    {
        public Guid JobPostingId { get; set; }
        public Guid? CandidateAccountId { get; set; }
        public string CandidateEmail { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidatePhone { get; set; }
        public string? CvFileUrl { get; set; }
        public string? CvText { get; set; }
        public string? CvFileHash { get; set; }
        public string? CoverLetter { get; set; }
        public string? NoticePeriod { get; set; }
    }

    public class ApplicationResponse
    {
        public Guid Id { get; set; }
        public Guid JobPostingId { get; set; }
        public string? JobTitle { get; set; }
        public string CandidateEmail { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidatePhone { get; set; }
        public string? CvFileUrl { get; set; }
        public string? CvText { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool PracticeSessionUsed { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public Guid? CvJdAnalysisId { get; set; }

        /// <summary>
        /// Cổng duyệt của Hiring Manager (ADR-061): pending | approved | rejected | bypassed.
        /// Null = chưa gửi duyệt. Giao diện dùng cột này để biết bật/tắt nút xếp lịch — trạng thái
        /// <c>hm_review</c> một mình KHÔNG nói được cổng đã mở hay chưa.
        /// </summary>
        public string? HmDecision { get; set; }

        /// <summary>Ghi chú của người quyết định: lý do từ chối, hoặc lý do vượt cổng.</summary>
        public string? HmDecisionNote { get; set; }

        public DateTimeOffset? HmDecidedAt { get; set; }

        /// <summary>Điểm phù hợp CV–JD (0–100) lấy từ cv_jd_analyses nếu có. Null nếu chưa phân tích.</summary>
        public int? MatchScore { get; set; }

        /// <summary>Tóm tắt CV từ kết quả phân tích CV-JD</summary>
        public string? CvJdSummary { get; set; }

        /// <summary>
        /// Điểm từng tiêu chí chấm CV kèm nhãn + trọng số của doanh nghiệp (ADR-060). Rỗng khi tin
        /// chưa khai bộ tiêu chí chấm CV — khi đó <see cref="MatchScore"/> vẫn là con số Gemini tự
        /// đưa ra và giao diện chỉ hiện điểm tổng như trước.
        /// </summary>
        public List<CvCriterionScoreDto> CvCriterionScores { get; set; } = new();

        /// <summary>
        /// True nếu ứng viên đã đặt lịch buổi phỏng vấn thật (InterviewBooking "scheduled") — điều kiện
        /// để Recruiter cấp Interview Code On-site (ADR-015/016). Sàng lọc/chưa đặt lịch → false.
        /// </summary>
        public bool HasScheduledInterview { get; set; }

        /// <summary>Vòng hiện tại của ứng viên (null nếu ở giai đoạn CV ứng tuyển)</summary>
        public int? CurrentRound { get; set; }

        /// <summary>
        /// Việc đang thật sự diễn ra ở vòng hiện tại — xem <c>ApplicationStageStatus</c>.
        ///
        /// <see cref="Status"/> chỉ nói hồ sơ ở KHÚC nào của phễu, nên suốt cả một vòng nó đứng yên
        /// ở "interview" trong khi thực tế đã đi qua xếp lịch → xác nhận → làm bài/vào phòng → chờ
        /// chốt. Cột này SUY RA từ dữ liệu, không lưu thêm ở DB.
        /// </summary>
        public string? StageStatus { get; set; }

        public string? CoverLetter { get; set; }
        public string? NoticePeriod { get; set; }
        public decimal? InterviewScore { get; set; }
        public DateTimeOffset? InterviewDate { get; set; }

        /// <summary>
        /// Phản hồi của ứng viên với lịch vòng hiện tại đang được xếp: pending | confirmed.
        /// Null nếu chưa có lịch "scheduled" cho vòng hiện tại (ADR-048).
        /// </summary>
        public string? ScheduleConfirmationStatus { get; set; }

        /// <summary>
        /// Lý do ứng viên báo bận ở lần xếp lịch gần nhất của vòng hiện tại — hiển thị cho nhân sự
        /// khi đang chờ xếp lại (chỉ set khi hiện KHÔNG còn lịch "scheduled").
        /// </summary>
        public string? ScheduleDeclineReason { get; set; }

        // Candidate Profile fields (Online Profile)
        public string? CandidateHeadline { get; set; }
        public string? CandidateAbout { get; set; }
        public string? CandidateLocation { get; set; }
        public string? CandidateDateOfBirth { get; set; }
        public string? CandidateLinkedinUrl { get; set; }
        public string? CandidateGithubUrl { get; set; }
        public string? CandidatePortfolioUrl { get; set; }
        public bool AllowHrViewProfile { get; set; } = true;
        public System.Collections.Generic.List<string> CandidateSkills { get; set; } = new();
        public System.Collections.Generic.List<CandidateExperienceItem> CandidateExperience { get; set; } = new();
        public System.Collections.Generic.List<CandidateEducationItem> CandidateEducation { get; set; } = new();
    }

    public class UpdateApplicationStatusRequest
    {
        [Required(ErrorMessage = "Trạng thái (status) là bắt buộc.")]
        public string Status { get; set; } = string.Empty;
    }
}
