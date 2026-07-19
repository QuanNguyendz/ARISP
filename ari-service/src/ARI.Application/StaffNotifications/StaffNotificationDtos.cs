using System;
using System.Collections.Generic;

namespace ARI.Application.StaffNotifications
{
    /// <summary>Một dòng thông báo staff — property khớp shape anonymous cũ của controller (camelCase khi serialize).</summary>
    public record StaffNotificationItemDto(
        Guid Id,
        string Type,
        string Title,
        string? Body,
        string? Link,
        bool IsRead,
        DateTimeOffset CreatedAt);

    /// <summary>Payload GET /api/staff/notifications: { items, unreadCount }.</summary>
    public record StaffNotificationListDto(
        IReadOnlyList<StaffNotificationItemDto> Items,
        int UnreadCount);
}
