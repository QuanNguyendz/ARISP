using System;
using System.Linq;

namespace ARI.Application.Options
{
    /// <summary>Cấu hình nghiệp vụ xếp lịch phỏng vấn. Bind từ section "Scheduling" (DependencyInjection).</summary>
    public class SchedulingOptions
    {
        /// <summary>
        /// <b>Đã ngừng dùng để tự huỷ lịch.</b> Trước đây quá hạn xác nhận là hệ thống tự chuyển booking
        /// sang "declined" rồi trả chỗ cho nhân sự xếp lại — nhưng như vậy người <i>phớt lờ</i> thư mời
        /// lại được đối xử y hệt người <i>chủ động báo bận</i>. Nay: im lặng thì bị nhắc, và nếu không
        /// tham dự thì hồ sơ dừng lại (ADR-059). Giá trị này chỉ còn dùng cho câu chữ tham chiếu cũ.
        /// </summary>
        public int ConfirmDeadlineHours { get; set; } = 48;

        /// <summary>
        /// Các mốc NHẮC ứng viên chưa phản hồi, tính bằng số giờ TRƯỚC giờ hẹn (mặc định "24,3").
        /// Mỗi mốc gửi đúng một lần: web notification + email trả lời vào chính luồng thư mời.
        /// Để rỗng = tắt nhắc tự động.
        /// </summary>
        public string ReminderHoursBeforeSlot { get; set; } = "24,3";

        /// <summary>Các mốc nhắc đã chuẩn hoá: số nguyên dương, giảm dần, không trùng.</summary>
        public int[] ReminderMarks() =>
            (ReminderHoursBeforeSlot ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var hours) ? hours : 0)
                .Where(hours => hours > 0)
                .Distinct()
                .OrderByDescending(hours => hours)
                .ToArray();

        /// <summary>
        /// Các mốc NHẮC ứng viên chưa phản hồi thư mời nhận việc, tính bằng số GIỜ trước hạn
        /// (mặc định "48,12" — tức trước 2 ngày và trước nửa ngày).
        ///
        /// Trước đây không có nhánh nhắc nào: `OfferEmail.BuildReminder` được viết ra rồi **không nơi
        /// nào gọi**, nên ứng viên quá hạn bị âm thầm đánh dấu từ chối mà chưa từng được cảnh báo —
        /// trong khi luồng phỏng vấn ngay bên cạnh vẫn nhắc ở 24h/3h. Để rỗng = tắt nhắc.
        /// </summary>
        public string OfferReminderHoursBeforeExpiry { get; set; } = "48,12";

        /// <summary>Mốc nhắc thư mời đã chuẩn hoá: số nguyên dương, giảm dần, không trùng.</summary>
        public int[] OfferReminderMarks() =>
            (OfferReminderHoursBeforeExpiry ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var hours) ? hours : 0)
                .Where(hours => hours > 0)
                .Distinct()
                .OrderByDescending(hours => hours)
                .ToArray();

        /// <summary>
        /// Tự đánh trượt hồ sơ khi ứng viên KHÔNG tham dự buổi phỏng vấn đã hẹn (qua giờ mà không có
        /// phiên phỏng vấn thật nào). Tắt đi thì lịch quá giờ nằm im chờ nhân sự xử lý tay.
        /// </summary>
        public bool AutoFailNoShow { get; set; } = true;

        /// <summary>
        /// Chờ thêm bao lâu sau giờ KẾT THÚC của ca mới đánh trượt — chừa khoảng cho ứng viên đến muộn
        /// và cho phiên phỏng vấn đang chạy kịp ghi nhận. Mặc định 2 giờ.
        /// </summary>
        public int NoShowGraceHours { get; set; } = 2;
    }
}
