namespace ARISP.Application.Options
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
    }
}
