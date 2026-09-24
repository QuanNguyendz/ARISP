using System;
using System.Collections.Generic;

namespace ARI.Application.Emails
{
    /// <summary>
    /// Khoá của các mẫu thư có thể XEM TRƯỚC và SỬA trước khi gửi (ADR-061, Phase 4).
    ///
    /// Luật phân loại: <b>có người bấm nút thì có trình soạn; máy tự gửi thì không.</b>
    /// Thư xác nhận nộp hồ sơ, nhắc lịch 24h/3h, quét no-show, xác minh email… đều do hệ thống
    /// phát ra không ai đứng sau, nên không có gì để soạn — chúng vẫn dùng mẫu, chỉ là không đi
    /// qua đường này.
    /// </summary>
    public static class EmailTemplateKeys
    {
        /// <summary>
        /// Thư mời phỏng vấn kèm giờ hẹn — gửi khi nhân sự duyệt CV (vòng 1) hoặc xếp lịch vòng kế.
        /// Ngữ cảnh: <c>applicationId</c> + <c>slotId</c> (chưa có booking lúc xem trước).
        /// </summary>
        public const string InterviewInvite = "interview_invite";

        /// <summary>Thư cảm ơn khi loại hồ sơ. Ngữ cảnh: <c>applicationId</c>.</summary>
        public const string ApplicationRejected = "application_rejected";

        /// <summary>Thư mời nhận việc (ADR-061, Phase 5). Ngữ cảnh: <c>applicationId</c>.</summary>
        public const string OfferSent = "offer_sent";

        /// <summary>
        /// Thư kết quả vòng phỏng vấn — gửi KÈM lệnh Hiring Manager chốt kết quả (ADR-074).
        /// Ngữ cảnh: <c>applicationId</c> + <c>evaluationId</c> + <c>variant</c> = verdict sắp chốt
        /// (<c>pass</c>/<c>not_pass</c>); biến thể thư (qua vòng / vòng cuối / cảm ơn) do server suy ra.
        /// </summary>
        public const string InterviewResult = "interview_result";

        /// <summary>
        /// Thư nhắc lịch do nhân sự bấm gửi. Ngữ cảnh: <c>applicationId</c> + <c>bookingId</c>.
        ///
        /// Hai biến thể, do <see cref="ARI.Application.Scheduling.ScheduleReminder"/> suy ra chứ không
        /// do người gửi chọn: ứng viên CHƯA phản hồi thì nhắc vào xác nhận (kèm hai nút Xác nhận/Báo
        /// bận), ĐÃ xác nhận thì nhắc giờ và việc cần chuẩn bị.
        /// </summary>
        public const string ScheduleReminder = "schedule_reminder";

        public static readonly string[] All =
            { InterviewInvite, ApplicationRejected, OfferSent, InterviewResult, ScheduleReminder };

        private static readonly HashSet<string> AllSet = new(All, StringComparer.OrdinalIgnoreCase);

        public static bool IsKnown(string? key) =>
            !string.IsNullOrWhiteSpace(key) && AllSet.Contains(key.Trim());
    }
}
