using System;
using ARI.Domain.Constants;

namespace ARI.Application.Scheduling
{
    /// <summary>
    /// Nút "Nhắc lịch" đang ở trạng thái nào — và vì sao.
    ///
    /// <b>Vì sao là một hàm thuần, ở một chỗ.</b> Cùng câu hỏi này được hỏi ở ba nơi: giao diện (bật
    /// hay tắt nút), trình soạn thư (dựng bản nào để sửa), và lệnh gửi (có cho gửi không). Ba nơi tự
    /// suy lấy là ba luật sẽ trôi khỏi nhau — đúng vết xe của <c>declineReason.includes('loại')</c>
    /// mà <c>ResolveCandidateState</c> sinh ra để dọn.
    ///
    /// Thứ tự xét CÓ Ý NGHĨA: lịch đã đóng thì nói "đã đóng" kể cả khi cũng đã qua giờ — người dùng
    /// cần biết ứng viên báo bận, chứ "đã qua giờ" thì không gợi ra việc gì phải làm tiếp.
    /// </summary>
    public static class ScheduleReminder
    {
        public static class States
        {
            /// <summary>Chưa xác nhận → nhắc ứng viên vào xác nhận lịch.</summary>
            public const string ConfirmNeeded = "confirm";

            /// <summary>Đã xác nhận → nhắc giờ đi phỏng vấn.</summary>
            public const string TimeReminder = "remind";

            /// <summary>Đã qua giờ hẹn → không còn gì để nhắc.</summary>
            public const string Past = "past";

            /// <summary>Ứng viên báo bận, hệ thống huỷ, hoặc nhân sự loại → lịch không còn hiệu lực.</summary>
            public const string Closed = "closed";
        }

        /// <param name="slotStart">Giờ BẮT ĐẦU ca (UTC). Qua mốc này thì lời nhắc không còn tác dụng.</param>
        public static string Resolve(
            string? bookingStatus, string? confirmationStatus, DateTimeOffset slotStart, DateTimeOffset now)
        {
            if (!string.Equals(bookingStatus, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
                return States.Closed;

            if (slotStart <= now) return States.Past;

            return string.Equals(confirmationStatus, BookingConfirmationStatus.Confirmed, StringComparison.OrdinalIgnoreCase)
                ? States.TimeReminder
                : States.ConfirmNeeded;
        }

        public static bool CanSend(string? state) =>
            state == States.ConfirmNeeded || state == States.TimeReminder;

        /// <summary>Câu trả lời cho "vì sao không nhắc được" — dùng chung cho lệnh gửi và trình soạn thư.</summary>
        public static string BlockedMessage(string? state) => state switch
        {
            States.Past =>
                "Đã qua giờ hẹn nên không nhắc lịch được nữa. Nếu ứng viên không tham dự, hãy xếp lại một ca khác.",
            _ =>
                "Lịch này không còn hiệu lực (ứng viên đã báo bận, hoặc ca đã bị huỷ) nên không nhắc được. "
                + "Hãy xếp lại một ca khác cho ứng viên.",
        };
    }
}
