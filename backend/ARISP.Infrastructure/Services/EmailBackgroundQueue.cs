using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;

namespace ARISP.Infrastructure.Services
{
    /// <summary>
    /// Triển khai hàng đợi email dựa trên System.Threading.Channels (unbounded, single-reader).
    /// Đăng ký dạng Singleton để chia sẻ một channel duy nhất cho toàn ứng dụng.
    /// </summary>
    public class EmailBackgroundQueue : IEmailQueue
    {
        private readonly Channel<EmailQueueItem> _channel =
            Channel.CreateUnbounded<EmailQueueItem>(new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(EmailQueueItem item)
        {
            // Channel unbounded → TryWrite luôn thành công, không chặn luồng request.
            _channel.Writer.TryWrite(item);
        }

        public ValueTask<EmailQueueItem> DequeueAsync(CancellationToken cancellationToken)
            => _channel.Reader.ReadAsync(cancellationToken);
    }
}
