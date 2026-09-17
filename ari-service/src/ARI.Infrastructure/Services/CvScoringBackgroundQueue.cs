using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Infrastructure.Services
{
    /// <summary>
    /// Hàng đợi chấm CV trong bộ nhớ (ADR-070). Mất khi tiến trình dừng là chấp nhận được: lượt quét của
    /// <see cref="CvScoringHostedService"/> tìm lại mọi hồ sơ còn thiếu điểm từ dữ liệu.
    /// Một hồ sơ chỉ nằm trong hàng một lần — lượt quét mười phút một lần không được nhân bản việc đang chờ.
    /// </summary>
    public sealed class CvScoringBackgroundQueue : ICvScoringQueue
    {
        private readonly Channel<CvScoringWorkItem> _channel =
            Channel.CreateUnbounded<CvScoringWorkItem>(new UnboundedChannelOptions { SingleReader = false });

        private readonly ConcurrentDictionary<Guid, byte> _pendingApps = new();

        public void EnqueueApplication(Guid applicationId)
        {
            if (applicationId == Guid.Empty || !_pendingApps.TryAdd(applicationId, 0)) return;
            if (!_channel.Writer.TryWrite(new CvScoringWorkItem(applicationId, null)))
                _pendingApps.TryRemove(applicationId, out _);
        }

        public void EnqueueJob(Guid jobPostingId)
        {
            if (jobPostingId == Guid.Empty) return;
            _channel.Writer.TryWrite(new CvScoringWorkItem(null, jobPostingId));
        }

        public ValueTask<CvScoringWorkItem> DequeueAsync(CancellationToken cancellationToken)
            => _channel.Reader.ReadAsync(cancellationToken);

        public void Complete(Guid applicationId) => _pendingApps.TryRemove(applicationId, out _);
    }
}
