using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.StaffNotifications.Commands.DeleteAllStaffNotifications;
using ARI.Application.StaffNotifications.Commands.DeleteStaffNotification;
using ARI.Application.StaffNotifications.Commands.MarkAllStaffNotificationsRead;
using ARI.Application.StaffNotifications.Commands.MarkStaffNotificationRead;
using ARI.Application.StaffNotifications.Queries.GetStaffNotifications;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.StaffNotifications;

/// <summary>
/// Thông báo nhân sự nội bộ (<c>StaffNotifications/*</c>): danh sách + đếm chưa đọc, đánh dấu đã đọc,
/// xóa — tất cả gắn chặt với <see cref="Notification.RecipientUserId"/> (không lộ chéo giữa các user).
/// Query lấy <c>IsRecruiter=false, IsHrAdmin=false</c> để bỏ qua nhánh sync sự kiện (không seed job),
/// cô lập đúng logic đọc/sắp xếp/đếm.
/// </summary>
public class GetStaffNotificationsQueryHandlerTests
{
    private static Notification Notif(Guid userId, string title, DateTimeOffset createdAt, bool isRead = false)
        => new() { RecipientUserId = userId, Type = "applied", Title = title, IsRead = isRead, CreatedAt = createdAt };

    [Fact]
    public async Task Returns_items_newest_first_with_unread_count()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var oldest = Notif(userId, "Cũ nhất", now.AddMinutes(-3), isRead: true);
        var middle = Notif(userId, "Giữa", now.AddMinutes(-2));
        var newest = Notif(userId, "Mới nhất", now.AddMinutes(-1));
        var uow = new InMemoryUnitOfWork().Seed(oldest, middle, newest);

        var res = await new GetStaffNotificationsQueryHandler(uow)
            .Handle(new GetStaffNotificationsQuery(userId, IsRecruiter: false, IsHrAdmin: false), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(3, res.Value.Items.Count);
        Assert.Equal("Mới nhất", res.Value.Items[0].Title);   // mới nhất trước
        Assert.Equal("Cũ nhất", res.Value.Items[2].Title);
        Assert.Equal(2, res.Value.UnreadCount);               // chỉ 2 chưa đọc
    }

    [Fact]
    public async Task Only_returns_notifications_of_the_requesting_user()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork()
            .Seed(Notif(me, "Của tôi", now), Notif(other, "Của người khác", now));

        var res = await new GetStaffNotificationsQueryHandler(uow)
            .Handle(new GetStaffNotificationsQuery(me, IsRecruiter: false, IsHrAdmin: false), CancellationToken.None);

        Assert.Equal("Của tôi", Assert.Single(res.Value.Items).Title);
        Assert.Equal(1, res.Value.UnreadCount);
    }

    [Fact]
    public async Task Empty_returns_zero_unread()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new GetStaffNotificationsQueryHandler(uow)
            .Handle(new GetStaffNotificationsQuery(Guid.NewGuid(), IsRecruiter: false, IsHrAdmin: false), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value.Items);
        Assert.Equal(0, res.Value.UnreadCount);
    }
}

/// <summary>Đánh dấu 1 thông báo đã đọc (<see cref="MarkStaffNotificationReadCommandHandler"/>): idempotent + chặn chéo user.</summary>
public class MarkStaffNotificationReadCommandHandlerTests
{
    private static Notification Notif(Guid userId, bool isRead = false)
        => new() { RecipientUserId = userId, Type = "applied", Title = "T", IsRead = isRead, CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Marks_unread_notification_read_and_persists()
    {
        var userId = Guid.NewGuid();
        var n = Notif(userId);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var res = await new MarkStaffNotificationReadCommandHandler(uow)
            .Handle(new MarkStaffNotificationReadCommand(userId, n.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(n.IsRead);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Already_read_is_noop_without_saving()
    {
        var userId = Guid.NewGuid();
        var n = Notif(userId, isRead: true);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var res = await new MarkStaffNotificationReadCommandHandler(uow)
            .Handle(new MarkStaffNotificationReadCommand(userId, n.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);  // đã đọc → không ghi
    }

    [Fact]
    public async Task Foreign_or_missing_notification_is_rejected()
    {
        var owner = Guid.NewGuid();
        var n = Notif(owner);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var foreign = await new MarkStaffNotificationReadCommandHandler(uow)
            .Handle(new MarkStaffNotificationReadCommand(Guid.NewGuid(), n.Id), CancellationToken.None);
        var missing = await new MarkStaffNotificationReadCommandHandler(uow)
            .Handle(new MarkStaffNotificationReadCommand(owner, Guid.NewGuid()), CancellationToken.None);

        Assert.False(foreign.IsSuccess);
        Assert.Contains("Không tìm thấy thông báo", foreign.Error);
        Assert.False(n.IsRead);                 // không bị đọc bởi người lạ
        Assert.False(missing.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}

/// <summary>Đánh dấu tất cả đã đọc (<see cref="MarkAllStaffNotificationsReadCommandHandler"/>): chỉ chạm chưa-đọc của đúng user + trả số cập nhật.</summary>
public class MarkAllStaffNotificationsReadCommandHandlerTests
{
    private static Notification Notif(Guid userId, bool isRead = false)
        => new() { RecipientUserId = userId, Type = "applied", Title = "T", IsRead = isRead, CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Marks_only_unread_of_user_and_returns_count()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var unread1 = Notif(me);
        var unread2 = Notif(me);
        var alreadyRead = Notif(me, isRead: true);
        var otherUnread = Notif(other);
        var uow = new InMemoryUnitOfWork().Seed(unread1, unread2, alreadyRead, otherUnread);

        var res = await new MarkAllStaffNotificationsReadCommandHandler(uow)
            .Handle(new MarkAllStaffNotificationsReadCommand(me), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value);             // chỉ 2 chưa đọc của tôi
        Assert.True(unread1.IsRead);
        Assert.True(unread2.IsRead);
        Assert.False(otherUnread.IsRead);       // không đụng người khác
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task No_unread_returns_zero_without_saving()
    {
        var me = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(Notif(me, isRead: true));

        var res = await new MarkAllStaffNotificationsReadCommandHandler(uow)
            .Handle(new MarkAllStaffNotificationsReadCommand(me), CancellationToken.None);

        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}

/// <summary>Xóa 1 thông báo (<see cref="DeleteStaffNotificationCommandHandler"/>): chỉ của đúng user.</summary>
public class DeleteStaffNotificationCommandHandlerTests
{
    private static Notification Notif(Guid userId)
        => new() { RecipientUserId = userId, Type = "applied", Title = "T", CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Deletes_own_notification()
    {
        var me = Guid.NewGuid();
        var n = Notif(me);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var res = await new DeleteStaffNotificationCommandHandler(uow)
            .Handle(new DeleteStaffNotificationCommand(me, n.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<Notification>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Foreign_or_missing_is_rejected()
    {
        var owner = Guid.NewGuid();
        var n = Notif(owner);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var res = await new DeleteStaffNotificationCommandHandler(uow)
            .Handle(new DeleteStaffNotificationCommand(Guid.NewGuid(), n.Id), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("Không tìm thấy thông báo", res.Error);
        Assert.Single(uow.Repo<Notification>().Items);   // vẫn còn
        Assert.Equal(0, uow.SaveChangesCount);
    }
}

/// <summary>Xóa toàn bộ thông báo (<see cref="DeleteAllStaffNotificationsCommandHandler"/>): chỉ của đúng user + trả số xóa.</summary>
public class DeleteAllStaffNotificationsCommandHandlerTests
{
    private static Notification Notif(Guid userId)
        => new() { RecipientUserId = userId, Type = "applied", Title = "T", CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Deletes_all_of_user_and_returns_count()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var keep = Notif(other);
        var uow = new InMemoryUnitOfWork().Seed(Notif(me), Notif(me), keep);

        var res = await new DeleteAllStaffNotificationsCommandHandler(uow)
            .Handle(new DeleteAllStaffNotificationsCommand(me), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value);
        Assert.Equal(keep, Assert.Single(uow.Repo<Notification>().Items));  // chỉ còn của người khác
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Empty_returns_zero_without_saving()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new DeleteAllStaffNotificationsCommandHandler(uow)
            .Handle(new DeleteAllStaffNotificationsCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}
