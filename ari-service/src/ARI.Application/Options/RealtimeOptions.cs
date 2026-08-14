namespace ARI.Application.Options
{
    /// <summary>
    /// Cấu hình realtime ở tầng database (ADR-057). Bind từ section "Realtime" (DependencyInjection).
    /// </summary>
    public class RealtimeOptions
    {
        public DbListenerOptions DbListener { get; set; } = new();
    }

    /// <summary>Cấu hình tiến trình nền LISTEN kênh NOTIFY của Postgres.</summary>
    public class DbListenerOptions
    {
        /// <summary>
        /// Tắt listener khi cần cô lập sự cố. Trigger vẫn phát NOTIFY (chi phí không đáng kể, và
        /// NOTIFY không có ai nghe thì Postgres bỏ qua) — chỉ là không còn ai chuyển tiếp sang SignalR.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Tên kênh NOTIFY, phải khớp với hàm arisp_notify_change() trong migration.</summary>
        public string Channel { get; set; } = "arisp_changes";
    }
}
