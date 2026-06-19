using System;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARISP.Infrastructure.Services
{
    /// <summary>
    /// Worker nền tiêu thụ IEmailQueue và gửi email thật qua IEmailService.
    /// Mỗi email được gửi trong một DI scope riêng nên an toàn với scoped services.
    /// Lỗi gửi được log lại, không làm sập vòng lặp xử lý.
    /// </summary>
    public class EmailQueueHostedService : BackgroundService
    {
        private readonly IEmailQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailQueueHostedService> _logger;

        public EmailQueueHostedService(
            IEmailQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<EmailQueueHostedService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                EmailQueueItem item;
                try
                {
                    item = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // Ứng dụng đang tắt
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendEmailAsync(item.ToEmail, item.Subject, item.HtmlBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gửi email nền thất bại tới {ToEmail}", item.ToEmail);
                }
            }
        }
    }
}
