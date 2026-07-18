using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    public interface INotificationService
    {
        Task SendEmailAsync(string toEmail, string subject, string content, CancellationToken ct = default);
        Task SendSlackNotificationAsync(string message, CancellationToken ct = default);
        Task SendTeamsNotificationAsync(string message, CancellationToken ct = default);
        Task PublishInterviewSessionEventAsync(Guid sessionId, string eventType, object payload, CancellationToken ct = default);
        
        // Cập nhật realtime cho 1 User cụ thể (ví dụ: Candidate Notification, HR cá nhân)
        Task PublishUserEventAsync(Guid userId, string eventType, object payload, CancellationToken ct = default);
        
        // Cập nhật realtime cho 1 Group hoặc Role (ví dụ: hr_admin, super_admin, recruiter)
        Task PublishGroupEventAsync(string groupName, string eventType, object payload, CancellationToken ct = default);
        
        // Cập nhật realtime cho TẤT CẢ clients (ví dụ: Public Job Board)
        Task PublishAllEventAsync(string eventType, object payload, CancellationToken ct = default);
    }
}
