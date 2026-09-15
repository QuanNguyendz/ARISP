using System;
using System.Collections.Generic;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Quyết định của Hiring Manager tại một CỔNG DUYỆT (ADR-061).
    ///
    /// Cố ý tách khỏi <see cref="ApplicationStatuses"/>: <c>Status</c> nói hồ sơ đang ở ĐÂU trong
    /// phễu, cột quyết định nói CỔNG ĐÃ MỞ CHƯA. Nếu mã hoá "HM đã duyệt" bằng một trạng thái nữa
    /// thì hồ sơ buộc phải đi lùi về <c>cv_submitted</c> để tiếp tục — mà máy trạng thái từ chối,
    /// và câu hỏi "ứng viên này đang ở đâu" mất câu trả lời duy nhất.
    ///
    /// Cùng khuôn với <c>AccountRequest</c> (ADR-041): trạng thái + người duyệt + thời điểm + lý do.
    /// </summary>
    public static class HmDecision
    {
        /// <summary>Đã gửi cho Hiring Manager, đang chờ quyết định — cổng ĐÓNG.</summary>
        public const string Pending = "pending";

        /// <summary>Hiring Manager đồng ý — cổng MỞ, nhân sự xếp lịch tiếp được.</summary>
        public const string Approved = "approved";

        /// <summary>Hiring Manager từ chối — hồ sơ dừng lại.</summary>
        public const string Rejected = "rejected";

        /// <summary>
        /// Quản trị viên vượt cổng (HM nghỉ, hoặc gấp). Cổng MỞ nhưng ghi rõ là đã vượt —
        /// kèm lý do bắt buộc, audit log, và thông báo cho chính HM bị vượt. Bypass im lặng mới
        /// là thất bại quản trị.
        /// </summary>
        public const string Bypassed = "bypassed";

        public static readonly string[] All = { Pending, Approved, Rejected, Bypassed };

        /// <summary>Cổng đã mở chưa — chỉ hai giá trị này cho phép hồ sơ đi tiếp.</summary>
        public static readonly string[] Open = { Approved, Bypassed };

        private static readonly HashSet<string> OpenSet = new(Open, StringComparer.OrdinalIgnoreCase);

        public static bool Is(string? actual, string expected) =>
            !string.IsNullOrWhiteSpace(actual)
            && string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        /// <summary>Cổng duyệt của Hiring Manager đã mở chưa.</summary>
        public static bool IsOpen(string? decision) =>
            !string.IsNullOrWhiteSpace(decision) && OpenSet.Contains(decision.Trim());
    }

    /// <summary>Chữ ký duyệt JD của Hiring Manager trên một tin (ADR-061, cổng 3b).</summary>
    public static class HmSignOffStatus
    {
        /// <summary>Recruiter đã gửi duyệt, đang chờ Hiring Manager ký.</summary>
        public const string Pending = "pending";

        /// <summary>Hiring Manager đã ký — tin được đăng.</summary>
        public const string Approved = "approved";

        /// <summary>
        /// Hiring Manager yêu cầu sửa — tin đồng thời về <c>rejected</c> để Recruiter sửa rồi gửi lại
        /// (ADR-068). Gửi lại thì cổng tự về <see cref="Pending"/>.
        /// </summary>
        public const string Rejected = "rejected";

        /// <summary>
        /// Quản trị viên đăng tin mà không chờ chữ ký (lý do ≥10 ký tự, audit log, báo cho HM bị vượt).
        /// Ghi thành giá trị riêng chứ không để nguyên <see cref="Pending"/> — nếu không, tin đã đăng vẫn
        /// hiện nút ký cho HM và vẫn nằm trong danh sách "chờ bạn ký".
        /// </summary>
        public const string Bypassed = "bypassed";

        public static readonly string[] All = { Pending, Approved, Rejected, Bypassed };

        public static bool Is(string? actual, string expected) =>
            !string.IsNullOrWhiteSpace(actual)
            && string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Cổng ký duyệt đã được giải chưa — chỉ khi đó tin mới được đăng LẦN ĐẦU.
        ///
        /// <c>null</c> KHÔNG còn nghĩa là "không có cổng" (ADR-068: mọi tin luôn có Hiring Manager):
        /// null là tin chưa từng gửi duyệt, nên đăng thẳng từ bản nháp cũng phải vượt cổng có lý do.
        /// </summary>
        public static bool IsCleared(string? status) =>
            Is(status, Approved) || Is(status, Bypassed);
    }
}
