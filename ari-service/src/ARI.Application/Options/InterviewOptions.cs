namespace ARI.Application.Options
{
    /// <summary>Cấu hình nghiệp vụ phỏng vấn. Bind từ section "Interview" (Program.cs).</summary>
    public class InterviewOptions
    {
        /// <summary>
        /// Số lượt phỏng vấn thử tối đa mỗi VÒNG (ADR-038: mặc định 1 lượt/vòng).
        /// Đặt &lt;= 0 để KHÔNG giới hạn — chỉ dùng cho môi trường dev/test
        /// (appsettings.Development.json), không bật ở production vì tốn phí media stack.
        /// </summary>
        public int PracticeAttemptsPerRound { get; set; } = 1;

        /// <summary>
        /// Trần thời lượng phỏng vấn THỬ (phút) — ADR-050 (hiện thực hoá ADR-038 điểm 5).
        /// Hết giờ → khoá mic, AI nói 1 câu kết thúc rồi đóng phiên. &lt;= 0 = không giới hạn.
        /// Cấu hình toàn cục (ops), không phải HR-knob per-job. Real dùng cấu hình riêng ở Phase 7.
        /// </summary>
        public int PracticeMaxDurationMinutes { get; set; } = 20;

        /// <summary>
        /// Trần thời lượng phỏng vấn THẬT (phút) — ADR-052. Hết giờ xử lý y hệt practice:
        /// khoá mic → AI nói câu kết → đóng phiên. &lt;= 0 = không giới hạn.
        /// Con số 20' vốn bằng trần mỗi phiên của gói avatar; avatar đã gỡ (ADR-067) nên nay nó
        /// thuần là quyết định nghiệp vụ về độ dài buổi phỏng vấn, đổi được tự do.
        /// </summary>
        public int RealMaxDurationMinutes { get; set; } = 20;

        /// <summary>
        /// Hạn lưu video phỏng vấn thật (ngày) — ADR-052. Job dọn dẹp chạy hằng ngày xoá file
        /// khỏi storage khi quá hạn. &lt;= 0 = giữ vĩnh viễn (tắt job).
        /// </summary>
        public int RecordingRetentionDays { get; set; } = 7;

        /// <summary>Dung lượng tối đa 1 file ghi hình (MB) — chặn upload rác từ Kiosk.</summary>
        public int MaxRecordingSizeMb { get; set; } = 300;

        /// <summary>Hạn token phiên Kiosk (giờ) — phải phủ hết buổi phỏng vấn + thời gian upload video.</summary>
        public int KioskSessionTokenHours { get; set; } = 3;
    }
}
