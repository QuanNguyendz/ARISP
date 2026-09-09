using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.DeleteUser;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Xóa / từ chối tài khoản staff (<see cref="DeleteUserCommandHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "DeleteUser" (UTCID01–06): chống tự xóa, tồn tại user, happy path xóa + audit (active/inactive),
/// actor nullable, và lỗi repository.
/// </summary>
public class DeleteUserCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static DeleteUserCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    private static User Target(bool active)
        => new() { Id = UserId, Email = "user@example.com", Role = "recruiter", FullName = "Target User", IsActive = active };

    // UTCID01 — Id == ActorId → chặn tự xóa
    [Fact]
    public async Task UTCID01_Deleting_self_is_blocked()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new DeleteUserCommand(ActorA, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không thể xóa chính tài khoản của mình.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID02 — user không tồn tại → not_found
    [Fact]
    public async Task UTCID02_User_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new DeleteUserCommand(UserId, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("User not found.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID03 — user active → xóa thành công + audit
    [Fact]
    public async Task UTCID03_Active_user_is_deleted_with_audit()
    {
        var user = Target(active: true);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new DeleteUserCommand(UserId, ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<User>().Items);
        Assert.Equal("user_deleted", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID04 — user inactive → vẫn xóa thành công
    [Fact]
    public async Task UTCID04_Inactive_user_is_deleted()
    {
        var user = Target(active: false);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new DeleteUserCommand(UserId, ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<User>().Items);
    }

    // UTCID05 — ActorId=null → vẫn xóa thành công
    [Fact]
    public async Task UTCID05_Null_actor_still_deletes()
    {
        var user = Target(active: true);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new DeleteUserCommand(UserId, null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<User>().Items);
    }

    // UTCID06 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID06_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new DeleteUserCommand(UserId, ActorA), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
