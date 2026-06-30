using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ARISP.Application.Interfaces;
using ARISP.Application.Hubs;
using ARISP.API.Hubs;

namespace ARISP.API.Services
{
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<SessionHub, ISessionClient> _sessionHubContext;
        private readonly IHubContext<AppNotificationHub, IAppNotificationClient> _appNotificationHubContext;
        private readonly IEmailService _emailService;

        public SignalRNotificationService(
            IHubContext<SessionHub, ISessionClient> sessionHubContext,
            IHubContext<AppNotificationHub, IAppNotificationClient> appNotificationHubContext,
            IEmailService emailService)
        {
            _sessionHubContext = sessionHubContext;
            _appNotificationHubContext = appNotificationHubContext;
            _emailService = emailService;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string content, CancellationToken ct = default)
        {
            Console.WriteLine($"[EMAIL] Sending actual email to: {toEmail} | Subject: {subject}");
            try
            {
                await _emailService.SendEmailAsync(toEmail, subject, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to send email via SMTP: {ex.Message}");
            }
        }

        public Task SendSlackNotificationAsync(string message, CancellationToken ct = default)
        {
            Console.WriteLine($"[SLACK] Msg: {message}");
            return Task.CompletedTask;
        }

        public Task SendTeamsNotificationAsync(string message, CancellationToken ct = default)
        {
            Console.WriteLine($"[TEAMS] Msg: {message}");
            return Task.CompletedTask;
        }

        public async Task PublishInterviewSessionEventAsync(Guid sessionId, string eventType, object payload, CancellationToken ct = default)
        {
            var group = _sessionHubContext.Clients.Group(sessionId.ToString());
            
            // Map eventType to corresponding ISessionClient method
            // Since payload is dynamic/anonymous type, we serialize and broadcast, or ideally map strongly.
            // But since ISessionClient has strongly typed methods, we'll try to handle known ones:
            if (eventType == "ReceiveQuestion" && payload != null)
            {
                var dict = (System.Collections.Generic.IDictionary<string, object>)payload.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(payload));
                await group.ReceiveQuestion(
                    (int)(dict["sequenceNumber"] ?? 0),
                    (string)(dict["questionText"] ?? ""),
                    (string?)(dict.ContainsKey("questionType") ? dict["questionType"] : null),
                    (int)(dict["difficultyLevel"] ?? 3)
                );
            }
            else if (eventType == "ReceiveAnswerAnalysis" && payload != null)
            {
                var dict = (System.Collections.Generic.IDictionary<string, object>)payload.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(payload));
                await group.ReceiveAnswerAnalysis((string)(dict["feedback"] ?? ""));
            }
            else if (eventType == "ReceiveSessionStatus" && payload != null)
            {
                var dict = (System.Collections.Generic.IDictionary<string, object>)payload.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(payload));
                await group.ReceiveSessionStatus((string)(dict["status"] ?? ""));
            }
            else
            {
                Console.WriteLine($"[SignalR] Unknown session event: {eventType}");
            }
        }

        public async Task PublishUserEventAsync(Guid userId, string eventType, object payload, CancellationToken ct = default)
        {
            await _appNotificationHubContext.Clients.Group($"user_{userId}").ReceiveSystemEvent(eventType, payload);
        }

        public async Task PublishGroupEventAsync(string groupName, string eventType, object payload, CancellationToken ct = default)
        {
            await _appNotificationHubContext.Clients.Group($"role_{groupName}").ReceiveSystemEvent(eventType, payload);
        }

        public async Task PublishAllEventAsync(string eventType, object payload, CancellationToken ct = default)
        {
            await _appNotificationHubContext.Clients.All.ReceiveSystemEvent(eventType, payload);
        }
    }
}
