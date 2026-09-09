using System;

namespace ARI.Domain.Entities
{
    public class InterviewSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string RoundType { get; set; } = string.Empty;
        public string SessionType { get; set; } = "real"; // practice | real
        public string InterviewLanguage { get; set; } = "vi";
        /// <summary>
        /// Ngôn ngữ VIẾT báo cáo AI (nhận xét/phân tích) — lấy theo ngôn ngữ ứng viên đang dùng
        /// trên web lúc bắt đầu phiên, để màn xem lại không trộn Việt–Anh (ADR-051).
        /// </summary>
        public string? ReportLanguage { get; set; }
        /// <summary>Xem <see cref="ARI.Domain.Constants.InterviewSessionStatuses"/>.</summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Hiring Manager đã vào phòng phỏng vấn cùng AI (ADR-067). Buổi THẬT không bắt đầu được
        /// khi cột này còn rỗng — đây là điều kiện, không phải dấu vết.
        /// </summary>
        public DateTimeOffset? HmJoinedAt { get; set; }

        /// <summary>Ai đang ngồi trong phòng — HM của tin, hoặc quản trị viên vào thay.</summary>
        public Guid? HmJoinedByUserId { get; set; }

        /// <summary>
        /// Thời điểm ứng viên được cho vào phòng. Tách khỏi <see cref="StartedAt"/> vì hai mốc trả
        /// lời hai câu khác nhau: "được duyệt vào lúc nào" và "phiên chạy từ lúc nào" — mốc sau là
        /// gốc tính trần thời lượng, gộp lại thì thời gian ngồi chờ bị trừ vào giờ phỏng vấn.
        /// </summary>
        public DateTimeOffset? AdmittedAt { get; set; }

        public Guid? AdmittedByUserId { get; set; }

        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int? DurationSeconds { get; set; }
        public string? RecordingUrl { get; set; }
        public bool RecordingVisibleToCandidate { get; set; } = false;
        /// <summary>Kích thước file ghi hình (bytes) — hiển thị cho HR + theo dõi dung lượng lưu trữ.</summary>
        public long? RecordingSizeBytes { get; set; }
        /// <summary>
        /// Hạn lưu video phỏng vấn thật (ADR-052): quá hạn thì job dọn dẹp xoá file khỏi storage.
        /// Mặc định = lúc tải lên + <c>Interview:RecordingRetentionDays</c> (7 ngày).
        /// </summary>
        public DateTimeOffset? RecordingExpiresAt { get; set; }
        /// <summary>Thời điểm file ghi hình đã bị xoá theo hạn lưu — giữ dấu vết để HR hiểu vì sao mất video.</summary>
        public DateTimeOffset? RecordingDeletedAt { get; set; }
        /// <summary>Câu chào kết thúc AI đã nói — lưu để transcript xem lại đủ (ADR-051).</summary>
        public string? ClosingText { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
