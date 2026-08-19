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
/// Xóa toàn bộ thông báo nhân sự (<see cref="DeleteAllStaffNotificationsCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "DeleteAllStaffNotifications" (UTCID01–03): không có → 0; có 2 → xóa 2; repo ném lỗi.
/// </summary>
public class DeleteAllStaffNotificationsCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static Notification Notif(Guid userId) => new() { RecipientUserId = userId, Type = "applied", Title = "T", CreatedAt = DateTimeOffset.UtcNow };

    // UTCID01 — không có thông báo → Success(0), không save
    [Fact]
    public async Task UTCID01_None_owned()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await new DeleteAllStaffNotificationsCommandHandler(uow).Handle(new DeleteAllStaffNotificationsCommand(UserId), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID02 — 2 thông báo → xóa cả 2
    [Fact]
    public async Task UTCID02_Two_owned()
    {
        var uow = new InMemoryUnitOfWork().Seed(Notif(UserId), Notif(UserId), Notif(Guid.NewGuid()));
        var res = await new DeleteAllStaffNotificationsCommandHandler(uow).Handle(new DeleteAllStaffNotificationsCommand(UserId), CancellationToken.None);
        Assert.Equal(2, res.Value);
        Assert.Single(uow.Repo<Notification>().Items);   // chỉ còn của người khác
    }

    // UTCID03 — repo ném lỗi
    [Fact]
    public async Task UTCID03_Repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<Notification>("Notification DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => new DeleteAllStaffNotificationsCommandHandler(uow).Handle(new DeleteAllStaffNotificationsCommand(UserId), CancellationToken.None));
        Assert.Equal("Notification DB Error", ex.Message);
    }
}

/// <summary>
/// Xóa 1 thông báo (<see cref="DeleteStaffNotificationCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "DeleteStaffNotification" (UTCID01–04): thiếu / của người khác → not found; của mình → Success; save ném lỗi.
/// </summary>
public class DeleteStaffNotificationCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static Notification Notif(Guid userId) => new() { RecipientUserId = userId, Type = "applied", Title = "T", CreatedAt = DateTimeOffset.UtcNow };

    // UTCID01 — không tồn tại
    [Fact]
    public async Task UTCID01_Missing()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await new DeleteStaffNotificationCommandHandler(uow).Handle(new DeleteStaffNotificationCommand(UserId, Guid.NewGuid()), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy thông báo.", res.Error);
    }

    // UTCID02 — của người khác
    [Fact]
    public async Task UTCID02_Foreign()
    {
        var n = Notif(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await new DeleteStaffNotificationCommandHandler(uow).Handle(new DeleteStaffNotificationCommand(UserId, n.Id), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy thông báo.", res.Error);
        Assert.Single(uow.Repo<Notification>().Items);
    }

    // UTCID03 — của mình → Success
    [Fact]
    public async Task UTCID03_Owned()
    {
        var n = Notif(UserId);
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await new DeleteStaffNotificationCommandHandler(uow).Handle(new DeleteStaffNotificationCommand(UserId, n.Id), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<Notification>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID04 — save ném lỗi
    [Fact]
    public async Task UTCID04_Save_error()
    {
        var n = Notif(UserId);
        var uow = new InMemoryUnitOfWork().Seed(n).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => new DeleteStaffNotificationCommandHandler(uow).Handle(new DeleteStaffNotificationCommand(UserId, n.Id), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Đánh dấu 1 thông báo đã đọc (<see cref="MarkStaffNotificationReadCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "MarkAllStaffNotificationsRead" (nội dung thực là lệnh MarkStaffNotificationRead, UTCID01–04): thiếu/của người khác,
/// đã đọc (no-op), chưa đọc (đánh dấu + save), save ném lỗi.
/// </summary>
public class MarkStaffNotificationReadCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static Notification Notif(Guid userId, bool isRead = false) => new() { RecipientUserId = userId, Type = "applied", Title = "T", IsRead = isRead, CreatedAt = DateTimeOffset.UtcNow };

    // UTCID01 — thiếu / của người khác → not found
    [Fact]
    public async Task UTCID01_Missing_or_foreign()
    {
        var n = Notif(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await new MarkStaffNotificationReadCommandHandler(uow).Handle(new MarkStaffNotificationReadCommand(UserId, n.Id), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy thông báo.", res.Error);
        Assert.False(n.IsRead);
    }

    // UTCID02 — của mình, đã đọc → no-op (không save)
    [Fact]
    public async Task UTCID02_Already_read()
    {
        var n = Notif(UserId, isRead: true);
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await new MarkStaffNotificationReadCommandHandler(uow).Handle(new MarkStaffNotificationReadCommand(UserId, n.Id), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID03 — của mình, chưa đọc → đánh dấu + save
    [Fact]
    public async Task UTCID03_Unread_marked()
    {
        var n = Notif(UserId);
        var uow = new InMemoryUnitOfWork().Seed(n);
        var res = await new MarkStaffNotificationReadCommandHandler(uow).Handle(new MarkStaffNotificationReadCommand(UserId, n.Id), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.True(n.IsRead);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID04 — save ném lỗi
    [Fact]
    public async Task UTCID04_Save_error()
    {
        var n = Notif(UserId);
        var uow = new InMemoryUnitOfWork().Seed(n).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => new MarkStaffNotificationReadCommandHandler(uow).Handle(new MarkStaffNotificationReadCommand(UserId, n.Id), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Danh sách + đếm chưa đọc (<see cref="GetStaffNotificationsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetStaffNotifications" (UTCID01–03): non-recruiter/non-HR không có → rỗng; recruiter 1 chưa đọc; repo ném lỗi.
/// </summary>
public class GetStaffNotificationsQueryHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static Notification Notif(Guid userId, bool isRead = false)
        => new() { RecipientUserId = userId, Type = "applied", Title = "Ứng viên mới ứng tuyển", Body = "Candidate User · Backend Developer",
            Link = "/recruiter/candidates", IsRead = isRead, CreatedAt = DateTimeOffset.UtcNow };

    // UTCID01 — non-recruiter/non-HR, không có thông báo → rỗng
    [Fact]
    public async Task UTCID01_Empty()
    {
        var res = await new GetStaffNotificationsQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetStaffNotificationsQuery(UserId, IsRecruiter: false, IsHrAdmin: false), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value.Items);
        Assert.Equal(0, res.Value.UnreadCount);
    }

    // UTCID02 — recruiter, 1 thông báo chưa đọc
    [Fact]
    public async Task UTCID02_Recruiter_one_unread()
    {
        var uow = new InMemoryUnitOfWork().Seed(Notif(UserId));   // không seed job → nhánh sync early-return
        var res = await new GetStaffNotificationsQueryHandler(uow)
            .Handle(new GetStaffNotificationsQuery(UserId, IsRecruiter: true, IsHrAdmin: false), CancellationToken.None);
        Assert.True(res.IsSuccess);
        var item = Assert.Single(res.Value.Items);
        Assert.Equal("applied", item.Type);
        Assert.False(item.IsRead);
        Assert.Equal(1, res.Value.UnreadCount);
    }

    // UTCID03 — repo ném lỗi
    [Fact]
    public async Task UTCID03_Repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<Notification>("Notification DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => new GetStaffNotificationsQueryHandler(uow)
            .Handle(new GetStaffNotificationsQuery(UserId, IsRecruiter: true, IsHrAdmin: false), CancellationToken.None));
        Assert.Equal("Notification DB Error", ex.Message);
    }
}

/// <summary>
/// Đánh dấu TẤT CẢ đã đọc (<see cref="MarkAllStaffNotificationsReadCommandHandler"/>) — KHÔNG có trong Report5
/// (report chỉ có lệnh single), giữ lại coverage sẵn có: chỉ chạm chưa-đọc của đúng user + trả số cập nhật.
/// </summary>
public class MarkAllStaffNotificationsReadCommandHandlerTests
{
    private static Notification Notif(Guid userId, bool isRead = false) => new() { RecipientUserId = userId, Type = "applied", Title = "T", IsRead = isRead, CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Marks_only_unread_of_user_and_returns_count()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var unread1 = Notif(me);
        var unread2 = Notif(me);
        var otherUnread = Notif(other);
        var uow = new InMemoryUnitOfWork().Seed(unread1, unread2, Notif(me, isRead: true), otherUnread);

        var res = await new MarkAllStaffNotificationsReadCommandHandler(uow).Handle(new MarkAllStaffNotificationsReadCommand(me), CancellationToken.None);

        Assert.Equal(2, res.Value);
        Assert.True(unread1.IsRead);
        Assert.False(otherUnread.IsRead);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task No_unread_returns_zero_without_saving()
    {
        var me = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(Notif(me, isRead: true));
        var res = await new MarkAllStaffNotificationsReadCommandHandler(uow).Handle(new MarkAllStaffNotificationsReadCommand(me), CancellationToken.None);
        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}
