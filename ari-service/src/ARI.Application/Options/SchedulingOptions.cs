namespace ARI.Application.Options
{
    /// <summary>Cấu hình nghiệp vụ xếp lịch phỏng vấn. Bind từ section "Scheduling" (DependencyInjection).</summary>
    public class SchedulingOptions
    {
        /// <summary>
        /// Số giờ kể từ lúc gửi email mời lịch (mốc <c>InterviewBooking.CreatedAt</c>) mà ứng viên phải
        /// XÁC NHẬN. Quá hạn vẫn "pending" → hệ thống tự chuyển booking sang "declined" (Rejected) và trả
        /// chỗ ở khung giờ để nhân sự xếp lại (giữ mô hình reschedule của ADR-048). &lt;= 0 = tắt auto-reject.
        /// </summary>
        public int ConfirmDeadlineHours { get; set; } = 48;
    }
}
