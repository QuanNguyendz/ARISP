using System;
using System.Collections.Generic;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Vòng đời của <see cref="Entities.Application"/> — cột <c>applications.status</c>.
    ///
    /// Cột này là <c>text</c> tự do, KHÔNG có ràng buộc CHECK ở DB, và được so sánh ở khoảng
    /// 20 chỗ trong mã nguồn. Trước khi có lớp này, câu hỏi "hồ sơ đã đóng chưa" bị chép thành
    /// <b>7 danh sách khác nhau</b> (hosted service quét no-show, từ chối hồ sơ, xoá tin, lưu trữ
    /// tin, chuyển giao tin, thống kê tải recruiter, điều kiện thi trắc nghiệm) — mỗi bản thiếu
    /// một giá trị khác nhau. Đó là cách một trạng thái mới lặng lẽ bị ghi đè: hồ sơ đã
    /// <see cref="Hired"/> mà còn lịch cũ quá giờ sẽ bị tác vụ nền đánh trượt, vì danh sách
    /// "đã đóng" của tác vụ đó không biết <see cref="Hired"/> tồn tại.
    ///
    /// Thêm trạng thái mới thì thêm vào ĐÂY và vào đúng nhóm bên dưới — không thêm chuỗi rời
    /// vào một câu <c>if</c> nào nữa.
    /// </summary>
    public static class ApplicationStatuses
    {
        /// <summary>Nhân sự mời ứng viên nộp hồ sơ, chưa có CV.</summary>
        public const string Invited = "invited";

        /// <summary>Ứng viên đã nộp CV, chờ sàng lọc.</summary>
        public const string CvSubmitted = "cv_submitted";

        /// <summary>
        /// Recruiter đã chọn vào shortlist, đang chờ Hiring Manager duyệt (ADR-061).
        /// Cổng mở hay chưa nằm ở cột <c>applications.hm_decision</c>, không phải ở trạng thái này:
        /// <see cref="HmReview"/> chỉ nói "hồ sơ đang nằm ở bàn của HM".
        /// </summary>
        public const string HmReview = "hm_review";

        /// <summary>Bị loại ngay ở vòng duyệt CV — chưa từng vào phỏng vấn.</summary>
        public const string CvRejected = "cv_rejected";

        /// <summary>Đã qua vòng CV, đang chờ/đang xếp lịch phỏng vấn.</summary>
        public const string Screening = "screening";

        /// <summary>Đang trong chuỗi vòng phỏng vấn.</summary>
        public const string Interview = "interview";

        /// <summary>Đạt HẾT các vòng phỏng vấn (ADR-053) — sẵn sàng ra offer.</summary>
        public const string Pass = "pass";

        /// <summary>Không đạt ở một vòng nào đó, hoặc không tham dự buổi đã hẹn.</summary>
        public const string NotPass = "not_pass";

        /// <summary>Đã GỬI thư mời nhận việc, đang chờ ứng viên trả lời (ADR-061).</summary>
        public const string Offer = "offer";

        /// <summary>Ứng viên đã nhận offer — điểm kết thúc thành công của phễu.</summary>
        public const string Hired = "hired";

        /// <summary>Ứng viên từ chối offer, hoặc offer hết hạn mà không trả lời.</summary>
        public const string OfferDeclined = "offer_declined";

        /// <summary>Ứng viên tự rút hồ sơ.</summary>
        public const string Withdrawn = "withdrawn";

        /// <summary>
        /// Giá trị cũ không còn nơi nào ghi (đã rà toàn bộ mã nguồn: chỉ còn duy nhất một câu
        /// lệnh phòng thủ trong <c>RejectApplicationAsync</c>). Giữ trong <see cref="Terminal"/>
        /// để dữ liệu cũ — nếu có — vẫn được coi là đã đóng.
        /// </summary>
        public const string LegacyFailed = "failed";

        // Các nhóm dưới đây khai bằng MẢNG chứ không phải HashSet vì chúng được dùng cả trong
        // truy vấn EF (`!ApplicationStatuses.Terminal.Contains(a.Status)` dịch thành SQL `IN`,
        // giữ đúng ngữ nghĩa phân biệt hoa thường của các câu `a.Status != "..."` trước đây).
        // Bản HashSet không phân biệt hoa thường dùng cho so sánh trong bộ nhớ được suy ra từ
        // chính mảng đó, nên chỉ có MỘT nơi liệt kê giá trị.

        /// <summary>Mọi trạng thái hợp lệ, dùng cho thông báo lỗi và kiểm tra đầu vào.</summary>
        public static readonly string[] All =
        {
            Invited, CvSubmitted, HmReview, CvRejected, Screening,
            Interview, Pass, NotPass, Offer, Hired, OfferDeclined, Withdrawn
        };

        /// <summary>
        /// Hồ sơ đã đóng — KHÔNG tác vụ nền hay lệnh tự động nào được ghi đè trạng thái này,
        /// và hồ sơ ở nhóm này không chặn việc xoá/lưu trữ tin tuyển dụng.
        /// </summary>
        public static readonly string[] Terminal =
        {
            Pass, NotPass, CvRejected, Withdrawn, Hired, OfferDeclined, LegacyFailed
        };

        /// <summary>
        /// Hồ sơ còn đang ở giai đoạn duyệt CV — chưa được nhấc vào quy trình phỏng vấn.
        /// Từ chối ở giai đoạn này ghi <see cref="CvRejected"/>; sau giai đoạn này ghi <see cref="NotPass"/>.
        /// </summary>
        public static readonly string[] CvPhase = { Invited, CvSubmitted, HmReview };

        /// <summary>
        /// Đã qua vòng duyệt CV — điều kiện để được làm bài thi trắc nghiệm (ADR-049).
        /// <see cref="CvSubmitted"/> (chưa duyệt), <see cref="HmReview"/> (chờ HM duyệt),
        /// <see cref="CvRejected"/> (bị loại) và <see cref="Withdrawn"/> đều KHÔNG thuộc nhóm này.
        /// </summary>
        public static readonly string[] CvPassed =
        {
            Invited, Screening, Interview, Pass, NotPass, Offer, Hired, OfferDeclined
        };

        private static readonly HashSet<string> TerminalSet = new(Terminal, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> CvPhaseSet = new(CvPhase, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> CvPassedSet = new(CvPassed, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> AllSet = new(All, StringComparer.OrdinalIgnoreCase);

        /// <summary>So sánh trạng thái không phân biệt hoa thường và khoảng trắng thừa.</summary>
        public static bool Is(string? actual, string expected) =>
            !string.IsNullOrWhiteSpace(actual)
            && string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        /// <summary>Trạng thái có nằm trong bộ giá trị hợp lệ không.</summary>
        public static bool IsKnown(string? status) =>
            !string.IsNullOrWhiteSpace(status) && AllSet.Contains(status.Trim());

        /// <summary>Hồ sơ đã đóng chưa — xem <see cref="Terminal"/>.</summary>
        public static bool IsTerminal(string? status) =>
            !string.IsNullOrWhiteSpace(status) && TerminalSet.Contains(status.Trim());

        /// <summary>Hồ sơ còn ở giai đoạn duyệt CV chưa — xem <see cref="CvPhase"/>.</summary>
        public static bool IsCvPhase(string? status) =>
            !string.IsNullOrWhiteSpace(status) && CvPhaseSet.Contains(status.Trim());

        /// <summary>Hồ sơ đã qua vòng duyệt CV chưa — xem <see cref="CvPassed"/>.</summary>
        public static bool IsCvPassed(string? status) =>
            !string.IsNullOrWhiteSpace(status) && CvPassedSet.Contains(status.Trim());
    }
}
