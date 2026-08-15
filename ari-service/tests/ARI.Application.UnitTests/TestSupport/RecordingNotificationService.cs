using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// Ghi lại các sự kiện realtime đã publish để test assert; có công tắc ném lỗi
/// để chứng minh handler xử lý best-effort (try/catch) không làm hỏng luồng chính.
/// </summary>
public sealed class RecordingNotificationService : INotificationService
{
    public List<(Guid UserId, string EventType)> UserEvents { get; } = new();
    public List<(string Group, string EventType)> GroupEvents { get; } = new();
    public List<string> AllEvents { get; } = new();
    public List<(Guid SessionId, string EventType)> SessionEvents { get; } = new();

    /// <summary>Nếu true: mọi Publish* ném lỗi (mô phỏng SignalR chết) để test đường best-effort.</summary>
    public bool ThrowOnPublish { get; set; }

    /// <summary>Thư đã gửi qua kênh notification (vd thư mời phỏng vấn kèm lịch) — để assert nội dung.</summary>
    public List<(string To, string Subject, string Body)> Emails { get; } = new();

    public Task SendEmailAsync(string toEmail, string subject, string content, CancellationToken ct = default)
    {
        Emails.Add((toEmail, subject, content));
        return Task.CompletedTask;
    }
    public Task SendSlackNotificationAsync(string message, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendTeamsNotificationAsync(string message, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishInterviewSessionEventAsync(Guid sessionId, string eventType, object payload, CancellationToken ct = default)
    {
        // Best-effort như production (không throw kể cả khi ThrowOnPublish) — chỉ ghi lại để assert.
        SessionEvents.Add((sessionId, eventType));
        return Task.CompletedTask;
    }

    public Task PublishUserEventAsync(Guid userId, string eventType, object payload, CancellationToken ct = default)
    {
        if (ThrowOnPublish) throw new InvalidOperationException("SignalR down");
        UserEvents.Add((userId, eventType));
        return Task.CompletedTask;
    }

    public Task PublishGroupEventAsync(string groupName, string eventType, object payload, CancellationToken ct = default)
    {
        if (ThrowOnPublish) throw new InvalidOperationException("SignalR down");
        GroupEvents.Add((groupName, eventType));
        return Task.CompletedTask;
    }

    public Task PublishAllEventAsync(string eventType, object payload, CancellationToken ct = default)
    {
        if (ThrowOnPublish) throw new InvalidOperationException("SignalR down");
        AllEvents.Add(eventType);
        return Task.CompletedTask;
    }
}
