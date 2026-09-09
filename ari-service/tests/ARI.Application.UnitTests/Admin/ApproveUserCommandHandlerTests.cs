using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.ApproveUser;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Duyệt (kích hoạt) tài khoản staff chờ duyệt (<see cref="ApproveUserCommandHandler"/>) — theo test-plan
/// Report5 Unit v1.2, tab "ApproveUser" (UTCID01–06): tồn tại user, biên Guid.Empty, đã active,
/// happy path kích hoạt + audit, actor nullable, và lỗi repository.
/// </summary>
public class ApproveUserCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static ApproveUserCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    private static User Target(bool active)
        => new() { Id = UserId, Email = "user@example.com", Role = "recruiter", FullName = "Target User", IsActive = active };

    // UTCID01 — user không tồn tại → not_found (Abnormal)
    [Fact]
    public async Task UTCID01_User_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new ApproveUserCommand(UserId, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("User not found.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID02 — Id=Guid.Empty (biên) → not_found (Boundary)
    [Fact]
    public async Task UTCID02_Empty_id_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new ApproveUserCommand(Guid.Empty, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("User not found.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID03 — user đã active → "User already active." (Abnormal)
    [Fact]
    public async Task UTCID03_Already_active_is_rejected()
    {
        var uow = new InMemoryUnitOfWork().Seed(Target(active: true));

        var res = await Handler(uow).Handle(new ApproveUserCommand(UserId, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("User already active.", res.Error);
        Assert.Null(res.ErrorCode);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID04 — user inactive → kích hoạt thành công + audit (Normal)
    [Fact]
    public async Task UTCID04_Inactive_user_is_activated_with_audit()
    {
        var user = Target(active: false);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new ApproveUserCommand(UserId, ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(user.IsActive);
        Assert.Equal("user_approved", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID05 — ActorId=null (không có actor) → vẫn thành công (Boundary)
    [Fact]
    public async Task UTCID05_Null_actor_still_activates()
    {
        var user = Target(active: false);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new ApproveUserCommand(UserId, null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(user.IsActive);
    }

    // UTCID06 — repository ném lỗi → thoát ra ngoài (Abnormal)
    [Fact]
    public async Task UTCID06_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new ApproveUserCommand(UserId, ActorA), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
