using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    public interface INotificationService
    {
        Task SendEmailAsync(string toEmail, string subject, string content, CancellationToken ct = default);

        /// <summary>
        /// Như <see cref="SendEmailAsync"/> nhưng trả về <c>Message-Id</c> của thư vừa gửi và cho
        /// phép trả lời vào một thư trước đó (thư nhắc lịch bám vào luồng thư mời — ADR-059).
        /// </summary>
        Task<string?> SendThreadedEmailAsync(
            string toEmail, string subject, string content, string? inReplyToMessageId = null,
            CancellationToken ct = default);
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
