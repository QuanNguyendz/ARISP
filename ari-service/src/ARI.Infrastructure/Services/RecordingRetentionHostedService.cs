using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.Services
{
    /// <summary>
    /// Dọn video phỏng vấn thật quá hạn lưu (ADR-052): mỗi ngày quét các phiên có
    /// <c>recording_expires_at</c> đã qua → xoá file khỏi storage, xoá <c>recording_url</c> và
    /// đánh dấu <c>recording_deleted_at</c> (giữ dấu vết để HR hiểu vì sao không còn video).
    ///
    /// Transcript và bản đánh giá KHÔNG bị xoá — chỉ file media.
    /// Tắt bằng <c>Interview:RecordingRetentionDays &lt;= 0</c>.
    /// </summary>
    public class RecordingRetentionHostedService : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(12);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly InterviewOptions _options;
        private readonly ILogger<RecordingRetentionHostedService> _logger;

        public RecordingRetentionHostedService(
            IServiceScopeFactory scopeFactory,
            InterviewOptions options,
            ILogger<RecordingRetentionHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.RecordingRetentionDays <= 0)
            {
                _logger.LogInformation("Retention video phỏng vấn: TẮT (RecordingRetentionDays <= 0).");
                return;
            }

            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PurgeExpiredAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Lỗi tạm (DB/storage) không được làm chết worker — thử lại ở chu kỳ sau.
                    _logger.LogError(ex, "Dọn video phỏng vấn quá hạn thất bại.");
                }

                try { await Task.Delay(ScanInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task PurgeExpiredAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            var nowUtc = DateTimeOffset.UtcNow;
            var expired = (await unitOfWork.Repository<InterviewSession>().FindAsync(
                s => s.RecordingUrl != null && s.RecordingExpiresAt != null && s.RecordingExpiresAt <= nowUtc, ct)).ToList();

            if (expired.Count == 0) return;

            foreach (var session in expired)
            {
                var storageKey = session.RecordingUrl!;
                try
                {
                    await storage.DeleteAsync(storageKey, ct);
                }
                catch (Exception ex)
                {
                    // File có thể đã bị xoá tay — vẫn dọn tham chiếu trong DB để không quét lại mãi.
                    _logger.LogWarning(ex, "Không xoá được file ghi hình {StorageKey} (phiên {SessionId}).",
                        storageKey, session.Id);
                }

                session.RecordingUrl = null;
                session.RecordingExpiresAt = null;
                session.RecordingDeletedAt = nowUtc;
                unitOfWork.Repository<InterviewSession>().Update(session);
            }

            await unitOfWork.SaveChangesAsync(ct);
            _logger.LogInformation("Đã dọn {Count} video phỏng vấn quá hạn lưu ({Days} ngày).",
                expired.Count, _options.RecordingRetentionDays);
        }
    }
}
