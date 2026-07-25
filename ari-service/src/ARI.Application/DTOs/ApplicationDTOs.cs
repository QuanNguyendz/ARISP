using System;
using System.ComponentModel.DataAnnotations;

namespace ARI.Application.DTOs
{
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

        /// <summary>Điểm phù hợp CV–JD (0–100) lấy từ cv_jd_analyses nếu có. Null nếu chưa phân tích.</summary>
        public int? MatchScore { get; set; }

        /// <summary>Tóm tắt CV từ kết quả phân tích CV-JD</summary>
        public string? CvJdSummary { get; set; }

        /// <summary>
        /// True nếu ứng viên đã đặt lịch buổi phỏng vấn thật (InterviewBooking "scheduled") — điều kiện
        /// để Recruiter cấp Interview Code On-site (ADR-015/016). Sàng lọc/chưa đặt lịch → false.
        /// </summary>
        public bool HasScheduledInterview { get; set; }

        /// <summary>Vòng hiện tại của ứng viên (null nếu ở giai đoạn CV ứng tuyển)</summary>
        public int? CurrentRound { get; set; }

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
    }

    public class UpdateApplicationStatusRequest
    {
        [Required(ErrorMessage = "Trạng thái (status) là bắt buộc.")]
        public string Status { get; set; } = string.Empty;
    }
}
