using System;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Vòng đời phiếu yêu cầu tuyển dụng (ADR-063).
    ///
    /// <c>rejected</c> KHÔNG phải trạng thái kết thúc: HR Leader từ chối là để trả phiếu về cho HM
    /// sửa (thường là chỉnh dải lương) rồi gửi lại — đúng như quy trình mô tả.
    ///
    /// <c>approved</c> cũng KHÔNG phải kết thúc (ADR-066). Nhu cầu tuyển thay đổi sau khi duyệt là
    /// chuyện thường: đội đổi yêu cầu, hoặc chỉ tiêu bị cắt. Trước đây phiếu duyệt xong là đóng băng
    /// — cách duy nhất để sửa là lập phiếu mới, khiến phiếu cũ nằm lại mãi ở trạng thái "sẵn sàng
    /// dựng tin" và Recruiter được phân công không biết việc đó đã bỏ. Nay HM (hoặc quản trị viên)
    /// <b>thu hồi phê duyệt</b> kèm lý do: mở lại để sửa (→ <c>pending</c>) hoặc đóng hẳn
    /// (→ <c>cancelled</c>). Trạng thái kết thúc thật sự chỉ còn <c>cancelled</c>.
    /// </summary>
    public static class RecruitmentRequestStatus
    {
        /// <summary>Đang chờ HR Leader duyệt.</summary>
        public const string Pending = "pending";

        /// <summary>Đã duyệt + đã phân công Recruiter. Sẵn sàng để dựng tin.</summary>
        public const string Approved = "approved";

        /// <summary>Bị trả lại kèm lý do. HM sửa rồi gửi lại → quay về <see cref="Pending"/>.</summary>
        public const string Rejected = "rejected";

        /// <summary>
        /// Phiếu không còn hiệu lực — HM tự rút khi chưa duyệt, hoặc đóng phiếu sau khi đã duyệt vì
        /// hết nhu cầu (ADR-066). Kết thúc, không dựng tin được.
        /// </summary>
        public const string Cancelled = "cancelled";

        public static readonly string[] All = { Pending, Approved, Rejected, Cancelled };

        public static bool Is(string? actual, string expected) =>
            !string.IsNullOrWhiteSpace(actual)
            && string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Phiếu chưa qua cổng duyệt: HM còn sửa nội dung và còn tự rút được, không cần lý do vì
        /// chưa ai duyệt gì để mà thu hồi.
        /// </summary>
        public static bool IsEditable(string? status) => Is(status, Pending) || Is(status, Rejected);

        /// <summary>
        /// Còn thu hồi được phê duyệt (ADR-066) — mở lại để sửa, hoặc đóng phiếu.
        ///
        /// Chỉ trả lời được phần <b>trạng thái</b>. Điều kiện thứ hai — <b>phiếu chưa sinh ra tin</b>
        /// — không nằm trong hàng phiếu (liên kết một chiều ở <c>JobPosting.RecruitmentRequestId</c>,
        /// ADR-063) nên phải kiểm trong handler. Đừng nhân bản nó thành một cột ở đây.
        /// </summary>
        public static bool IsRevocable(string? status) => Is(status, Approved);
    }
}
