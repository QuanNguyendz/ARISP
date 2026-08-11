using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.ApproveUser;
using ARI.Application.Common;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Duyệt tài khoản staff chờ duyệt (<see cref="ApproveUserCommandHandler"/>, test-plan B29): user inactive →
/// IsActive=true (KHÔNG đụng LockReason, khác Activate) + audit 'user_approved'; thiếu → NotFound; đã active → lỗi.
/// </summary>
public class ApproveUserCommandHandlerTests
{
    private static ApproveUserCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    [Fact]
    public async Task Inactive_user_is_approved_without_touching_lock_reason()
    {
        var user = AuthData.Staff(active: false);
        user.LockReason = "Chờ duyệt";
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new ApproveUserCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(user.IsActive);
        Assert.Equal("Chờ duyệt", user.LockReason);   // KHÔNG xoá (khác ActivateUser)
        Assert.Equal("user_approved", Assert.Single(uow.Repo<AuditLog>().Items).Action);
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new ApproveUserCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Already_active_user_is_rejected()
    {
        var user = AuthData.Staff(active: true);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new ApproveUserCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("User already active", res.Error);
    }
}
