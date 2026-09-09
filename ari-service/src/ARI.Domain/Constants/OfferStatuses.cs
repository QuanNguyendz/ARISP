using System;
using System.Collections.Generic;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Vòng đời thư mời nhận việc (ADR-061, Phase 5) — đoạn kết mà phễu tuyển dụng trước đây
    /// không có: <c>pass</c> là trạng thái cuối, và email chúc mừng nói thẳng "HR sẽ liên hệ để
    /// gửi Offer Letter", tức là quy trình rời khỏi hệ thống đúng ở bước quan trọng nhất.
    /// </summary>
    public static class OfferStatus
    {
        /// <summary>Đang soạn — chỉ nhân sự thấy, chưa hứa gì với ai.</summary>
        public const string Draft = "draft";

        /// <summary>Đã gửi cho Hiring Manager (hoặc quản trị viên) duyệt mức lương/điều kiện.</summary>
        public const string PendingApproval = "pending_approval";

        /// <summary>Đã duyệt, chờ nhân sự gửi cho ứng viên.</summary>
        public const string Approved = "approved";

        /// <summary>Đã GỬI cho ứng viên — từ đây hồ sơ mới chuyển sang <c>offer</c>.</summary>
        public const string Sent = "sent";

        /// <summary>Ứng viên đồng ý → hồ sơ <c>hired</c>.</summary>
        public const string Accepted = "accepted";

        /// <summary>Ứng viên từ chối → hồ sơ <c>offer_declined</c>.</summary>
        public const string Declined = "declined";

        /// <summary>Doanh nghiệp thu hồi (lý do bắt buộc).</summary>
        public const string Withdrawn = "withdrawn";

        /// <summary>Quá hạn trả lời — tác vụ nền đóng lại.</summary>
        public const string Expired = "expired";

        public static readonly string[] All =
        {
            Draft, PendingApproval, Approved, Sent, Accepted, Declined, Withdrawn, Expired,
        };

        /// <summary>
        /// Trạng thái mà ứng viên được phép NHÌN THẤY. Bản nháp và bản đang duyệt là đàm phán nội
        /// bộ — lộ ra là ứng viên thấy mức lương công ty còn đang cân nhắc.
        /// </summary>
        public static readonly string[] VisibleToCandidate = { Sent, Accepted, Declined, Expired };

        /// <summary>Đã khép — không còn chiếm "một offer sống" của hồ sơ nữa.</summary>
        public static readonly string[] Closed = { Withdrawn, Declined, Expired };

        private static readonly HashSet<string> VisibleSet = new(VisibleToCandidate, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ClosedSet = new(Closed, StringComparer.OrdinalIgnoreCase);

        public static bool Is(string? actual, string expected) =>
            !string.IsNullOrWhiteSpace(actual)
            && string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        public static bool IsVisibleToCandidate(string? status) =>
            !string.IsNullOrWhiteSpace(status) && VisibleSet.Contains(status.Trim());

        public static bool IsClosed(string? status) =>
            !string.IsNullOrWhiteSpace(status) && ClosedSet.Contains(status.Trim());
    }
}
