using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ARI.Application.DTOs
{
    /// <summary>
    /// Điểm CV của một hồ sơ KÈM CÁCH RA ĐIỂM (ADR-070) — để Hiring Manager / Recruiter thấy con số được
    /// cộng từ những gì, theo chuẩn nào, với bằng chứng nào trong CV.
    /// </summary>
    public class CvScoreBreakdownDto
    {
        /// <summary><see cref="ARI.Domain.Constants.CvScoreStates"/>.</summary>
        public string State { get; set; } = string.Empty;

        /// <summary>Điểm cuối (đã làm tròn). Null khi không có điểm để hiện.</summary>
        public int? Total { get; set; }

        /// <summary>Kết quả phép chia trước khi làm tròn (vd 70.43).</summary>
        public decimal? ExactTotal { get; set; }

        /// <summary>Σ (điểm × trọng số) của các tiêu chí được tính.</summary>
        public decimal WeightedSum { get; set; }

        /// <summary>Σ trọng số của các tiêu chí được tính (tiêu chí AI bỏ sót không nằm trong đây).</summary>
        public decimal TotalWeight { get; set; }

        /// <summary>Tiêu chí được tính vào điểm, đúng thứ tự doanh nghiệp khai.</summary>
        public List<CvScoreCriterionDto> Criteria { get; set; } = new();

        /// <summary>Tiêu chí AI không chấm được — bị loại khỏi CẢ tử lẫn mẫu.</summary>
        public List<CvScoreCriterionDto> Excluded { get; set; } = new();

        public DateTimeOffset? ScoredAt { get; set; }

        /// <summary>Model đã chấm: "Gemini" hoặc "GPT-4o-mini" (dự phòng).</summary>
        public string? Model { get; set; }

        /// <summary>Bản chấm này dùng đúng bộ tiêu chí đang sống của tin không.</summary>
        public bool IsCurrentRubric { get; set; }

        /// <summary>Phiên bản bộ tiêu chí đang sống: lưu lúc nào, bởi ai.</summary>
        public DateTimeOffset? RubricSavedAt { get; set; }
        public string? RubricSavedBy { get; set; }

        /// <summary>Lý do khi file không phải CV.</summary>
        public string? InvalidReason { get; set; }

        /// <summary>
        /// Khi <c>State = scoring_failed</c>: mã lý do (<c>cv_unreadable</c> | <c>ai_unavailable</c> |
        /// <c>no_criterion_scored</c>), số lượt đã hỏng, và lúc hệ thống sẽ thử lại (đã qua = đang chờ lượt quét kế).
        /// </summary>
        public string? FailureReason { get; set; }
        public int? FailedAttempts { get; set; }
        public DateTimeOffset? RetryAt { get; set; }

        public string? Summary { get; set; }
        public List<string> SkillsMatched { get; set; } = new();
        public List<string> SkillsGaps { get; set; } = new();
        public List<string> RedFlags { get; set; } = new();
        public string? SeniorityAlignment { get; set; }
        public string? ExperienceRelevance { get; set; }

        /// <summary>Nhãn suy ra từ điểm (không phải phán đoán riêng của AI).</summary>
        public string? Recommendation { get; set; }
    }

    public class CvScoreCriterionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal? Weight { get; set; }
        public decimal? Score { get; set; }

        /// <summary>Số điểm tiêu chí này góp vào điểm cuối = điểm × trọng số ÷ Σ trọng số.</summary>
        public decimal? Contribution { get; set; }

        /// <summary>Dải điểm rơi vào: excellent | good | fair | poor.</summary>
        public string? Band { get; set; }

        /// <summary>Khoảng điểm của dải (vd 90–100) — để màn hình viết lại phép tính vị trí trong dải.</summary>
        public decimal? BandMin { get; set; }
        public decimal? BandMax { get; set; }

        /// <summary><c>checklist</c> = vị trí trong dải tính từ ý kiểm · <c>ai</c> = AI ước lượng (chưa có ý kiểm).</summary>
        public string? ScoreSource { get; set; }

        /// <summary>Số ý đạt / số ý AI đã trả lời (mẫu số của phép tính vị trí).</summary>
        public int? ChecksMet { get; set; }
        public int? ChecksAnswered { get; set; }
        public List<CvScoreCheckDto> Checks { get; set; } = new();

        public string? Evidence { get; set; }
        public string? Reasoning { get; set; }
        public string? Description { get; set; }
        public CvRubricLevelsDto? Levels { get; set; }
    }

    /// <summary>Một ý kiểm đã được AI trả lời.</summary>
    public class CvScoreCheckDto
    {
        public string Key { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        /// <summary>true = đạt · false = không đạt · null = AI không trả lời (không tính vào mẫu số).</summary>
        public bool? Met { get; set; }
        public string? Evidence { get; set; }
        /// <summary>AI đánh đạt nhưng không trích được bằng chứng — không tính.</summary>
        public bool Unsupported { get; set; }
    }

    public class CvRubricLevelsDto
    {
        public string? Excellent { get; set; }
        public string? Good { get; set; }
        public string? Fair { get; set; }
        public string? Poor { get; set; }
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

        /// <summary>
        /// Điểm CV (0–100) theo bộ tiêu chí của tin (ADR-070). Null khi chưa có điểm hợp lệ để hiện —
        /// xem <see cref="CvScoreStatus"/> để biết vì sao.
        /// </summary>
        public int? MatchScore { get; set; }

        /// <summary>
        /// Trạng thái điểm CV: scored | rescoring | queued | scoring_failed | pending_rubric | invalid_cv | no_cv
        /// (<see cref="ARI.Domain.Constants.CvScoreStates"/>).
        /// </summary>
        public string? CvScoreStatus { get; set; }

        /// <summary>Khi <see cref="CvScoreStatus"/> = <c>scoring_failed</c>: lúc hệ thống sẽ tự chấm lại.</summary>
        public DateTimeOffset? CvScoreRetryAt { get; set; }

        /// <summary>Tóm tắt CV từ kết quả phân tích CV-JD</summary>
        public string? CvJdSummary { get; set; }

        /// <summary>
        /// Cách ra điểm CV — chỉ có ở màn CHI TIẾT hồ sơ (danh sách không chở phần này).
        /// </summary>
        public CvScoreBreakdownDto? CvScore { get; set; }

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
        /// Điểm bài trắc nghiệm của VÒNG HIỆN TẠI (0–100), null khi vòng này không phải vòng trắc
        /// nghiệm hoặc ứng viên chưa nộp bài.
        ///
        /// Đây là DTO của phía NHÂN SỰ nên điểm đi kèm — khác hẳn <c>CandidateOnlineTestDto</c>, nơi
        /// điểm và kết quả đạt/trượt cố ý không rời server (kết quả chỉ công bố khi cả vòng đã chốt).
        /// </summary>
        public decimal? OnlineTestScore { get; set; }

        /// <summary>
        /// Bài trắc nghiệm của vòng hiện tại có đạt điểm sàn không. Null khi chưa nộp.
        /// <b>Không tự đổi trạng thái hồ sơ</b>: dưới sàn thì ứng viên vẫn ở nguyên vòng trắc nghiệm
        /// cho tới khi Recruiter quyết định loại — đây chỉ là con số để họ quyết định.
        /// </summary>
        public bool? OnlineTestPassed { get; set; }

        /// <summary>
        /// Bài trắc nghiệm của vòng hiện tại do hệ thống nộp thay khi hết hạn — ứng viên đã được hẹn
        /// giờ nhưng không vào làm. Điểm (thường là 0) vẫn là điểm thật; cờ này để bảng nói rõ "không
        /// làm bài" thay vì trông như "làm sai hết". Null khi chưa có bài.
        /// </summary>
        public bool? OnlineTestExpired { get; set; }

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
