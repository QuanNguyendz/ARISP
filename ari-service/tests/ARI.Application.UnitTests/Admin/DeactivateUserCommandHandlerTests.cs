using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.DeactivateUser;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Khóa tài khoản staff (<see cref="DeactivateUserCommandHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "DeactivateUser" (UTCID01–08): chống tự khóa, bắt buộc lý do, tồn tại user, đã bị khóa,
/// happy path khóa + audit, actor nullable, và lỗi repository.
/// </summary>
public class DeactivateUserCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string Reason = "Policy violation";

    private static DeactivateUserCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    private static User Target(bool active)
        => new() { Id = UserId, Email = "user@example.com", Role = "recruiter", FullName = "Target User", IsActive = active };

    // UTCID01 — Id == ActorId → chặn tự khóa (ưu tiên trước cả kiểm lý do)
    [Fact]
    public async Task UTCID01_Locking_self_is_blocked()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new DeactivateUserCommand(ActorA, null, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không thể khóa chính tài khoản của mình.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID02 — Reason=null → bắt buộc lý do
    [Fact]
    public async Task UTCID02_Null_reason_is_required()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new DeactivateUserCommand(UserId, null, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Vui lòng nhập lý do khóa tài khoản.", res.Error);
    }

    // UTCID03 — Reason="   " (whitespace) → bắt buộc lý do
    [Fact]
    public async Task UTCID03_Whitespace_reason_is_required()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new DeactivateUserCommand(UserId, "   ", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Vui lòng nhập lý do khóa tài khoản.", res.Error);
    }

    // UTCID04 — user không tồn tại → not_found
    [Fact]
    public async Task UTCID04_User_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new DeactivateUserCommand(UserId, Reason, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("User not found.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID05 — user đã bị khóa → "Tài khoản đã bị khóa."
    [Fact]
    public async Task UTCID05_Already_locked_is_rejected()
    {
        var uow = new InMemoryUnitOfWork().Seed(Target(active: false));

        var res = await Handler(uow).Handle(new DeactivateUserCommand(UserId, Reason, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Tài khoản đã bị khóa.", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID06 — user đang active → khóa thành công + audit + lưu lý do
    [Fact]
    public async Task UTCID06_Active_user_is_locked_with_audit()
    {
        var user = Target(active: true);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new DeactivateUserCommand(UserId, Reason, ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(user.IsActive);
        Assert.Equal(Reason, user.LockReason);
        Assert.Equal("user_deactivated", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID07 — ActorId=null → vẫn khóa thành công
    [Fact]
    public async Task UTCID07_Null_actor_still_locks()
    {
        var user = Target(active: true);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new DeactivateUserCommand(UserId, Reason, null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(user.IsActive);
    }

    // UTCID08 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID08_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new DeactivateUserCommand(UserId, Reason, ActorA), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
