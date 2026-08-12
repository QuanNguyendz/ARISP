using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.DeleteUser;
using ARI.Application.Common;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Xóa (soft delete) tài khoản staff (<see cref="DeleteUserCommandHandler"/>, test-plan B24): chống tự xóa
/// (kiểm trước load), không tìm thấy → NotFound, và happy path xóa + audit 'user_deleted'.
/// </summary>
public class DeleteUserCommandHandlerTests
{
    private static DeleteUserCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    [Fact]
    public async Task Deleting_own_account_is_blocked_before_loading()
    {
        var me = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new DeleteUserCommand(me, me), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không thể xóa chính tài khoản", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new DeleteUserCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task User_is_deleted_with_audit()
    {
        var user = AuthData.Staff(email: "u@x.io");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new DeleteUserCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<User>().Items);
        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("user_deleted", audit.Action);
        Assert.Equal(user.Id, audit.EntityId);
        Assert.Equal(1, uow.SaveChangesCount);
    }
}
