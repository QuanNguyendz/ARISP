using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    public interface INotificationService
    {
        Task SendEmailAsync(string toEmail, string subject, string content, CancellationToken ct = default);
        Task SendSlackNotificationAsync(Guid organizationId, string message, CancellationToken ct = default);
        Task SendTeamsNotificationAsync(Guid organizationId, string message, CancellationToken ct = default);
        Task PublishInterviewSessionEventAsync(Guid sessionId, string eventType, object payload, CancellationToken ct = default);
    }
}
