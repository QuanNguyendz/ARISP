using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.API.Hubs;
using ARI.Application.Hubs;
using ARI.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ARI.API.Services
{
    /// <summary>
    /// NotificationService thật: đẩy sự kiện phiên phỏng vấn qua SignalR (SessionHub group = sessionId)
    /// để FE nhận ReceiveQuestion / ReceiveQuestionAudio / ReceiveAnswerAnalysis / ReceiveSessionStatus realtime.
    /// Session events dùng SendAsync động (giữ nguyên payload — FE đọc camelCase, gồm cả questionId)
    /// thay vì map qua ISessionClient để không rơi field khi thêm event mới.
    /// Thông báo ứng dụng (user/role/all) đẩy qua AppNotificationHub. Email uỷ thác IEmailService.
    /// </summary>
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<SessionHub> _sessionHub;
        private readonly IHubContext<AppNotificationHub, IAppNotificationClient> _appNotificationHub;
        private readonly IEmailService _emailService;
        private readonly ILogger<SignalRNotificationService> _logger;

        public SignalRNotificationService(
            IHubContext<SessionHub> sessionHub,
            IHubContext<AppNotificationHub, IAppNotificationClient> appNotificationHub,
            IEmailService emailService,
            ILogger<SignalRNotificationService> logger)
        {
            _sessionHub = sessionHub;
            _appNotificationHub = appNotificationHub;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string content, CancellationToken ct = default)
        {
            try
            {
                await _emailService.SendEmailAsync(toEmail, subject, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email tới {Email} thất bại", toEmail);
            }
        }

        public Task SendSlackNotificationAsync(string message, CancellationToken ct = default)
        {
            _logger.LogInformation("[SLACK] {Message}", message);
            return Task.CompletedTask;
        }

        public Task SendTeamsNotificationAsync(string message, CancellationToken ct = default)
        {
            _logger.LogInformation("[TEAMS] {Message}", message);
            return Task.CompletedTask;
        }

        public Task PublishInterviewSessionEventAsync(Guid sessionId, string eventType, object payload, CancellationToken ct = default)
        {
            // Group = sessionId.ToString() (SessionHub.JoinSession). FE: connection.on(eventType, payload => ...)
            return _sessionHub.Clients.Group(sessionId.ToString()).SendAsync(eventType, payload, ct);
        }

        public async Task PublishUserEventAsync(Guid userId, string eventType, object payload, CancellationToken ct = default)
        {
            await _appNotificationHub.Clients.Group($"user_{userId}").ReceiveSystemEvent(eventType, payload);
        }

        public async Task PublishGroupEventAsync(string groupName, string eventType, object payload, CancellationToken ct = default)
        {
            await _appNotificationHub.Clients.Group($"role_{groupName}").ReceiveSystemEvent(eventType, payload);
        }

        public async Task PublishAllEventAsync(string eventType, object payload, CancellationToken ct = default)
        {
            await _appNotificationHub.Clients.All.ReceiveSystemEvent(eventType, payload);
        }
    }
}
