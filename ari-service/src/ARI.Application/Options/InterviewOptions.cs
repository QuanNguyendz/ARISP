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
        /// ADR-050: Practice mặc định KHÔNG dùng avatar (audio-only) để tránh cạnh tranh
        /// concurrency LiveAvatar với buổi thật + đốt credit không dự đoán được. Đặt true để
        /// bật lại avatar cho practice khi tài khoản LiveAvatar đủ quota. Real luôn có avatar.
        /// </summary>
        public bool PracticeUseAvatar { get; set; } = false;
    }
}
