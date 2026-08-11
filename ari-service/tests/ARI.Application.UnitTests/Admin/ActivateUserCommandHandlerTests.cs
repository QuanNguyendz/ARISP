using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.ActivateUser;
using ARI.Application.Common;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Mở khóa tài khoản staff (<see cref="ActivateUserCommandHandler"/>, test-plan B24): user đã khóa → mở +
/// xoá LockReason + audit 'user_activated'; user đang hoạt động → lỗi; không tìm thấy → NotFound.
/// </summary>
public class ActivateUserCommandHandlerTests
{
    private static ActivateUserCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    [Fact]
    public async Task Inactive_user_is_activated_and_lock_reason_cleared()
    {
        var user = AuthData.Staff(active: false);
        user.LockReason = "Vi phạm";
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new ActivateUserCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(user.IsActive);
        Assert.Null(user.LockReason);
        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("user_activated", audit.Action);
        Assert.Equal(user.Id, audit.EntityId);
    }

    [Fact]
    public async Task Already_active_user_is_rejected()
    {
        var user = AuthData.Staff(active: true);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new ActivateUserCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã đang hoạt động", res.Error);
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new ActivateUserCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }
}
