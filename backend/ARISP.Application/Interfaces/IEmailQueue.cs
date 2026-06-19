using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    /// <summary>
    /// Một email đang chờ gửi trong hàng đợi nền.
    /// </summary>
    public record EmailQueueItem(string ToEmail, string Subject, string HtmlBody);

    /// <summary>
    /// Hàng đợi gửi email bất đồng bộ: enqueue KHÔNG chặn request,
    /// một BackgroundService sẽ gửi thật (SMTP) ở luồng nền.
    /// </summary>
    public interface IEmailQueue
    {
        /// <summary>Đưa email vào hàng đợi (non-blocking, trả về ngay).</summary>
        void Enqueue(EmailQueueItem item);

        /// <summary>Lấy email kế tiếp để gửi (chờ tới khi có item).</summary>
        ValueTask<EmailQueueItem> DequeueAsync(CancellationToken cancellationToken);
    }
}
