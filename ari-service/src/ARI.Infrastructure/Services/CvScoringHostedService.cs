using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CvScoring;
using ARI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.Services
{
    /// <summary>
    /// Chấm CV nền (ADR-070). Hai nhịp:
    ///
    /// 1. <b>Tiêu thụ hàng đợi</b> với số lượt gọi AI đồng thời có giới hạn (<c>CvScoring:MaxConcurrency</c>,
    ///    mặc định 3) — thay cho các <c>Task.Run</c> tự phát trước đây, vốn không giới hạn gì: HM lưu bộ tiêu
    ///    chí mới cho tin có 300 hồ sơ là 300 lời gọi Gemini cùng lúc.
    /// 2. <b>Quét định kỳ</b> (<c>CvScoring:SweepMinutes</c>, mặc định 10) tìm mọi hồ sơ còn thiếu điểm theo
    ///    bộ tiêu chí hiện hành, và nhắc HM của tin đang nhận hồ sơ mà chưa có bộ tiêu chí. Nhờ nhịp này,
    ///    việc rơi khỏi hàng (khởi động lại, AI lỗi) tự được làm lại.
    /// </summary>
    public class CvScoringHostedService : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);
        private const int SweepBatch = 200;
        private const int JobBatch = 1000;

        private readonly ICvScoringQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CvScoringHostedService> _logger;
        private readonly int _maxConcurrency;
        private readonly TimeSpan _sweepInterval;

        public CvScoringHostedService(
            ICvScoringQueue queue,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<CvScoringHostedService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _maxConcurrency = Math.Clamp(configuration.GetValue("CvScoring:MaxConcurrency", 3), 1, 16);
            _sweepInterval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("CvScoring:SweepMinutes", 10)));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workers = Enumerable.Range(0, _maxConcurrency).Select(_ => WorkerAsync(stoppingToken)).ToList();
            workers.Add(SweepLoopAsync(stoppingToken));
            return Task.WhenAll(workers);
        }

        private async Task WorkerAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                CvScoringWorkItem item;
                try
                {
                    item = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scorer = scope.ServiceProvider.GetRequiredService<CvApplicationScorer>();

                    if (item.ApplicationId is { } appId)
                    {
                        await scorer.ScoreApplicationAsync(appId, stoppingToken);
                    }
                    else if (item.JobPostingId is { } jobId)
                    {
                        foreach (var id in await scorer.FindStaleApplicationIdsAsync(jobId, JobBatch, stoppingToken))
                            _queue.EnqueueApplication(id);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Việc chấm CV nền thất bại ({@Item})", item);
                }
                finally
                {
                    if (item.ApplicationId is { } done) _queue.Complete(done);
                }
            }
        }

        private async Task SweepLoopAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scorer = scope.ServiceProvider.GetRequiredService<CvApplicationScorer>();

                    List<Guid> stale = await scorer.FindStaleApplicationIdsAsync(null, SweepBatch, stoppingToken);
                    foreach (var id in stale) _queue.EnqueueApplication(id);
                    if (stale.Count > 0)
                        _logger.LogInformation("Quét chấm CV: {Count} hồ sơ vào hàng", stale.Count);

                    await scorer.NotifyMissingRubricAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lượt quét chấm CV thất bại.");
                }

                try { await Task.Delay(_sweepInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
