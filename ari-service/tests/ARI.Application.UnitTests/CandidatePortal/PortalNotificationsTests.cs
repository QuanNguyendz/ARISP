using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Thông báo Candidate Portal (<c>PortalNotificationsFeature.cs</c>): danh sách + đếm chưa đọc, đánh dấu
/// đã đọc, xóa — gắn chặt <see cref="Notification.CandidateAccountId"/>. Không seed Application nào
/// nên nhánh sync sự kiện thoát sớm (apps.Count==0), cô lập đúng logic đọc/sắp xếp/đếm.
/// </summary>
public class GetPortalNotificationsQueryHandlerTests
{
    private static Notification Notif(Guid candidateId, string title, DateTimeOffset createdAt, bool isRead = false)
        => new() { CandidateAccountId = candidateId, Type = "result", Title = title, IsRead = isRead, CreatedAt = createdAt };

    [Fact]
    public async Task Returns_items_newest_first_with_unread_count()
    {
        var cand = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork().Seed(
            Notif(cand, "Cũ nhất", now.AddMinutes(-3), isRead: true),
            Notif(cand, "Giữa", now.AddMinutes(-2)),
            Notif(cand, "Mới nhất", now.AddMinutes(-1)));

        var res = await new GetPortalNotificationsQueryHandler(uow)
            .Handle(new GetPortalNotificationsQuery(cand), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(3, res.Value.Items.Count);
        Assert.Equal("Mới nhất", res.Value.Items[0].Title);
        Assert.Equal("Cũ nhất", res.Value.Items[2].Title);
        Assert.Equal(2, res.Value.UnreadCount);
    }

    [Fact]
    public async Task Only_returns_notifications_of_the_candidate()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork()
            .Seed(Notif(me, "Của tôi", now), Notif(other, "Của người khác", now));

        var res = await new GetPortalNotificationsQueryHandler(uow)
            .Handle(new GetPortalNotificationsQuery(me), CancellationToken.None);

        Assert.Equal("Của tôi", Assert.Single(res.Value.Items).Title);
        Assert.Equal(1, res.Value.UnreadCount);
    }

    [Fact]
    public async Task Empty_returns_zero_unread()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new GetPortalNotificationsQueryHandler(uow)
            .Handle(new GetPortalNotificationsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value.Items);
        Assert.Equal(0, res.Value.UnreadCount);
    }
}

/// <summary>Đánh dấu 1 thông báo portal đã đọc (<see cref="MarkPortalNotificationReadCommandHandler"/>): idempotent + chặn chéo.</summary>
public class MarkPortalNotificationReadCommandHandlerTests
{
    private static Notification Notif(Guid candidateId, bool isRead = false)
        => new() { CandidateAccountId = candidateId, Type = "result", Title = "T", IsRead = isRead, CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Marks_unread_read_and_persists()
    {
        var cand = Guid.NewGuid();
        var n = Notif(cand);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var res = await new MarkPortalNotificationReadCommandHandler(uow)
            .Handle(new MarkPortalNotificationReadCommand(cand, n.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(n.IsRead);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Already_read_is_noop_without_saving()
    {
        var cand = Guid.NewGuid();
        var n = Notif(cand, isRead: true);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var res = await new MarkPortalNotificationReadCommandHandler(uow)
            .Handle(new MarkPortalNotificationReadCommand(cand, n.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Foreign_or_missing_is_rejected()
    {
        var owner = Guid.NewGuid();
        var n = Notif(owner);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var foreign = await new MarkPortalNotificationReadCommandHandler(uow)
            .Handle(new MarkPortalNotificationReadCommand(Guid.NewGuid(), n.Id), CancellationToken.None);
        var missing = await new MarkPortalNotificationReadCommandHandler(uow)
            .Handle(new MarkPortalNotificationReadCommand(owner, Guid.NewGuid()), CancellationToken.None);

        Assert.False(foreign.IsSuccess);
        Assert.Contains("Không tìm thấy thông báo", foreign.Error);
        Assert.False(n.IsRead);
        Assert.False(missing.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}

/// <summary>Đánh dấu tất cả portal đã đọc (<see cref="MarkAllPortalNotificationsReadCommandHandler"/>): chỉ chưa-đọc của đúng ứng viên.</summary>
public class MarkAllPortalNotificationsReadCommandHandlerTests
{
    private static Notification Notif(Guid candidateId, bool isRead = false)
        => new() { CandidateAccountId = candidateId, Type = "result", Title = "T", IsRead = isRead, CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Marks_only_unread_of_candidate_and_returns_count()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var unread1 = Notif(me);
        var unread2 = Notif(me);
        var otherUnread = Notif(other);
        var uow = new InMemoryUnitOfWork().Seed(unread1, unread2, Notif(me, isRead: true), otherUnread);

        var res = await new MarkAllPortalNotificationsReadCommandHandler(uow)
            .Handle(new MarkAllPortalNotificationsReadCommand(me), CancellationToken.None);

        Assert.Equal(2, res.Value);
        Assert.True(unread1.IsRead);
        Assert.True(unread2.IsRead);
        Assert.False(otherUnread.IsRead);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task No_unread_returns_zero_without_saving()
    {
        var me = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(Notif(me, isRead: true));

        var res = await new MarkAllPortalNotificationsReadCommandHandler(uow)
            .Handle(new MarkAllPortalNotificationsReadCommand(me), CancellationToken.None);

        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}

/// <summary>Xóa thông báo portal (<see cref="DeletePortalNotificationCommandHandler"/> / <see cref="DeleteAllPortalNotificationsCommandHandler"/>).</summary>
public class DeletePortalNotificationCommandHandlerTests
{
    private static Notification Notif(Guid candidateId)
        => new() { CandidateAccountId = candidateId, Type = "result", Title = "T", CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Deletes_own_notification()
    {
        var me = Guid.NewGuid();
        var n = Notif(me);
        var uow = new InMemoryUnitOfWork().Seed(n);

        var res = await new DeletePortalNotificationCommandHandler(uow)
            .Handle(new DeletePortalNotificationCommand(me, n.Id), CancellationToken.None);

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

        var res = await new DeletePortalNotificationCommandHandler(uow)
            .Handle(new DeletePortalNotificationCommand(Guid.NewGuid(), n.Id), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Single(uow.Repo<Notification>().Items);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Delete_all_removes_only_candidate_notifications_and_returns_count()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var keep = Notif(other);
        var uow = new InMemoryUnitOfWork().Seed(Notif(me), Notif(me), keep);

        var res = await new DeleteAllPortalNotificationsCommandHandler(uow)
            .Handle(new DeleteAllPortalNotificationsCommand(me), CancellationToken.None);

        Assert.Equal(2, res.Value);
        Assert.Equal(keep, Assert.Single(uow.Repo<Notification>().Items));
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Delete_all_empty_returns_zero_without_saving()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new DeleteAllPortalNotificationsCommandHandler(uow)
            .Handle(new DeleteAllPortalNotificationsCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}
