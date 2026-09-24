using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.Services
{
    /// <summary>
    /// Sinh báo cáo đánh giá phỏng vấn nền (ADR-073). Hai nhịp, cùng khuôn với <see cref="CvScoringHostedService"/>:
    ///
    /// 1. <b>Tiêu thụ hàng đợi</b> với số lượt gọi AI đồng thời có giới hạn (<c>Evaluation:MaxConcurrency</c>,
    ///    mặc định 2). Lệnh đóng phiên chỉ ghi "chờ chấm" rồi đưa vào đây — ứng viên không phải chờ AI chấm
    ///    mới thấy lời chào kết thúc, và lỗi AI không làm hỏng lệnh đóng phiên.
    /// 2. <b>Quét định kỳ</b> (<c>Evaluation:SweepSeconds</c>, mặc định 120) tìm mọi phiên còn dang dở: chưa
    ///    chấm, kẹt ở "đang chấm", lỗi còn lượt thử, hay đang chờ bộ tiêu chí mà tin nay đã khai — và nhắc
    ///    Hiring Manager của tin còn thiếu bộ tiêu chí.
    /// </summary>
    public class EvaluationHostedService : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
        private const int SweepBatch = 100;

        private readonly IEvaluationQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EvaluationHostedService> _logger;
        private readonly int _maxConcurrency;
        private readonly TimeSpan _sweepInterval;

        public EvaluationHostedService(
            IEvaluationQueue queue,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<EvaluationHostedService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _maxConcurrency = Math.Clamp(configuration.GetValue("Evaluation:MaxConcurrency", 2), 1, 8);
            _sweepInterval = TimeSpan.FromSeconds(Math.Max(15, configuration.GetValue("Evaluation:SweepSeconds", 120)));
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
                Guid sessionId;
                try
                {
                    sessionId = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var evaluator = scope.ServiceProvider.GetRequiredService<InterviewEvaluator>();
                    var outcome = await evaluator.EvaluateSessionAsync(sessionId, stoppingToken);
                    _logger.LogInformation("Chấm phiên {SessionId}: {Outcome}", sessionId, outcome);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Việc chấm báo cáo nền thất bại (phiên {SessionId})", sessionId);
                }
                finally
                {
                    _queue.Complete(sessionId);
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
                    var evaluator = scope.ServiceProvider.GetRequiredService<InterviewEvaluator>();

                    var due = await evaluator.FindDueSessionIdsAsync(SweepBatch, stoppingToken);
                    foreach (var id in due) _queue.Enqueue(id);
                    if (due.Count > 0)
                        _logger.LogInformation("Quét chấm báo cáo: {Count} phiên vào hàng", due.Count);

                    await evaluator.NotifyMissingRubricAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lượt quét chấm báo cáo thất bại.");
                }

                try { await Task.Delay(_sweepInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
