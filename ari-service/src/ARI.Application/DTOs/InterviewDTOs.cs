using System;
using System.Collections.Generic;

namespace ARI.Application.DTOs
{
    public class StartSessionRequest
    {
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string SessionType { get; set; } = "real"; // practice | real
        /// <summary>
        /// Ngôn ngữ giao diện ứng viên đang dùng (vi | en) — AI viết nhận xét bằng ngôn ngữ này
        /// để màn xem lại không trộn hai thứ tiếng (ADR-051). Bỏ trống = theo ngôn ngữ phỏng vấn.
        /// </summary>
        public string? UiLanguage { get; set; }
    }

    public class StartSessionResponse
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = "active";
        public string Language { get; set; } = "vi";
        public string? HeyGenSdpOffer { get; set; }
        public string? HeyGenSessionId { get; set; }
    }

    /// <summary>
    /// Kết quả nhập Interview Code tại Kiosk (ADR-052). Mã hợp lệ → phiên phỏng vấn thật đã được
    /// tạo, kèm token phạm vi đúng phiên đó để máy Kiosk gọi API/SignalR mà không cần đăng nhập.
    /// </summary>
    public class KioskSessionResponse
    {
        public bool Valid { get; set; }
        /// <summary>Lý do khi mã không dùng được: not_found | used | expired.</summary>
        public string? Reason { get; set; }
        public Guid? SessionId { get; set; }
        public string? Token { get; set; }
        public DateTimeOffset? TokenExpiresAt { get; set; }
        public string? CandidateName { get; set; }
        public string? JobTitle { get; set; }
        public int RoundNumber { get; set; }
        public string? RoundType { get; set; }
        public string Language { get; set; } = "vi";
    }

    /// <summary>Kết quả tải video buổi phỏng vấn thật lên storage (ADR-052).</summary>
    public class RecordingUploadResponse
    {
        public bool Saved { get; set; }
        public long SizeBytes { get; set; }
        /// <summary>Hạn lưu — quá hạn thì job dọn dẹp xoá file.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    /// <summary>Cấu hình media trả cho FE khi vào phòng phỏng vấn (token Deepgram + HeyGen).</summary>
    public class PracticeMediaConfigResponse
    {
        public Guid SessionId { get; set; }
        public string Language { get; set; } = "vi";
        public string SessionType { get; set; } = "practice";

        /// <summary>Trần thời lượng phiên (giây) để FE vẽ đếm ngược; 0 = không giới hạn (ADR-050).</summary>
        public int MaxDurationSeconds { get; set; }

        /// <summary>Mốc bắt đầu phiên (UTC) để FE tính thời gian còn lại khớp giờ server.</summary>
        public DateTimeOffset? StartedAtUtc { get; set; }

        public DeepgramConfigDto? Deepgram { get; set; }
        public HeyGenConfigDto? HeyGen { get; set; }
    }

    public class DeepgramConfigDto
    {
        public string Token { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
        public string Model { get; set; } = "nova-3";
    }

    public class HeyGenConfigDto
    {
        public string Token { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public string? AvatarId { get; set; }
        public string? VoiceId { get; set; }
    }

    public class TtsRequest
    {
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Tín hiệu nghi vấn client báo về (ADR-054): fullscreen_exit | tab_hidden | window_blur | ...</summary>
    public class ReportSignalRequest
    {
        public string SignalType { get; set; } = string.Empty;
        /// <summary>JSON tuỳ ý (vd {"at":"...","count":3}) — server chặn kích thước và ép JSON hợp lệ.</summary>
        public string? Payload { get; set; }
    }

    public class SubmitAnswerRequest
    {
        public Guid QuestionId { get; set; }
        public string Transcript { get; set; } = string.Empty;
        public int? ResponseTimeMs { get; set; }
    }

    public class GenerateCodeRequest
    {
        public Guid ApplicationId { get; set; }
        public int? RoundNumber { get; set; }
    }

    public class GenerateBatchRequest
    {
        public List<Guid> ApplicationIds { get; set; } = new();
        public int? RoundNumber { get; set; }
    }

    public class ValidateCodeRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public class InterviewCodeSummaryDto
    {
        public string Code { get; set; } = string.Empty;
        public int RoundNumber { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? UsedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Một phiên phỏng vấn hiển thị trong danh sách HR.
    /// </summary>
    public class HrInterviewSessionItem
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public int RoundNumber { get; set; }
        public string RoundType { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty; // practice | real
        public string Status { get; set; } = string.Empty;      // pending | active | completed | aborted | error
        public string InterviewLanguage { get; set; } = "vi";
        public int? DurationSeconds { get; set; }
        public bool HasRecording { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public Guid? EvaluationId { get; set; }
        public string? Verdict { get; set; }
    }

    // ─────────── Interview Management: Job → Slot → Candidate ───────────

    /// <summary>
    /// Một vị trí tuyển dụng có phỏng vấn, kèm thống kê nhanh cho trang quản lý phiên.
    /// </summary>
    public class InterviewJobSummaryDto
    {
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string JobStatus { get; set; } = string.Empty; // active | closed | ...
        public int TotalSlots { get; set; }          // Tổng số ca đã tạo
        public int TotalBooked { get; set; }         // Tổng ứng viên đã đặt lịch
        public int TotalConfirmed { get; set; }      // Ứng viên xác nhận
        public int MaxRound { get; set; }            // Vòng cao nhất đang active
        public int TotalSessions { get; set; }       // Tổng phiên AI đã tạo
        public int CompletedSessions { get; set; }   // Phiên đã hoàn thành
        public DateTimeOffset? NextSlotTime { get; set; } // Ca gần nhất sắp diễn ra
    }

    /// <summary>
    /// Chi tiết một ca phỏng vấn (AvailabilitySlot), kèm thống kê đặt lịch.
    /// </summary>
    public class InterviewSlotDetailDto
    {
        public Guid SlotId { get; set; }
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public int Capacity { get; set; }
        public int BookedCount { get; set; }
        public int ConfirmedCount { get; set; }   // ConfirmationStatus = confirmed
        public int DeclinedCount { get; set; }    // ConfirmationStatus = declined
        public int PendingCount { get; set; }     // ConfirmationStatus = pending
        public bool IsPast { get; set; }          // StartTime < now
    }

    /// <summary>
    /// Một ứng viên trong một ca phỏng vấn cụ thể.
    /// </summary>
    public class SlotCandidateDto
    {
        public Guid ApplicationId { get; set; }
        public Guid BookingId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;
        public string ConfirmationStatus { get; set; } = "pending"; // pending | confirmed | declined
        public string? DeclineReason { get; set; }
        public string BookingStatus { get; set; } = "scheduled";    // scheduled | completed | cancelled
        public Guid? SessionId { get; set; }
        public string? SessionStatus { get; set; }   // active | completed | aborted
        public int? DurationSeconds { get; set; }
        public Guid? EvaluationId { get; set; }
        public string? Verdict { get; set; }          // Pass | Not Pass
        public int? OverallScore { get; set; }
        public string? InterviewCode { get; set; }
        public DateTimeOffset? CodeExpiresAt { get; set; }
    }

    public class RescheduleRequest
    {
        public Guid TargetSlotId { get; set; }
    }
}
