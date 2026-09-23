using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Infrastructure.Services
{
    /// <summary>
    /// Hàng đợi chấm báo cáo phỏng vấn trong bộ nhớ (ADR-073). Mất khi tiến trình dừng là chấp nhận được:
    /// trạng thái thật nằm ở <c>interview_sessions.evaluation_status</c> và lượt quét của
    /// <see cref="EvaluationHostedService"/> tìm lại mọi phiên còn dang dở.
    /// Một phiên chỉ nằm trong hàng một lần — lượt quét định kỳ không được nhân bản việc đang chờ.
    /// </summary>
    public sealed class EvaluationBackgroundQueue : IEvaluationQueue
    {
        private readonly Channel<Guid> _channel =
            Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = false });

        private readonly ConcurrentDictionary<Guid, byte> _pending = new();

        public void Enqueue(Guid sessionId)
        {
            if (sessionId == Guid.Empty || !_pending.TryAdd(sessionId, 0)) return;
            if (!_channel.Writer.TryWrite(sessionId))
                _pending.TryRemove(sessionId, out _);
        }

        public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
            => _channel.Reader.ReadAsync(cancellationToken);

        public void Complete(Guid sessionId) => _pending.TryRemove(sessionId, out _);
    }
}
