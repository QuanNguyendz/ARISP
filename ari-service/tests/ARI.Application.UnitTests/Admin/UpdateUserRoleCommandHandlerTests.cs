using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.UpdateUserRole;
using ARI.Application.Common;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Đổi vai trò staff (<see cref="UpdateUserRoleCommandHandler"/>) — bám theo test-plan Report5 Unit v1.2,
/// tab "UpdateUserRole" (UTCID01–09): bắt buộc role, role hợp lệ, chuẩn hoá, chống tự đổi vai trò,
/// tồn tại user, actor nullable, thành công + audit, và lỗi repository.
/// </summary>
public class UpdateUserRoleCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static UpdateUserRoleCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    private static User TargetUser(string role = "recruiter")
        => new() { Id = UserId, Email = "user@example.com", Role = role, FullName = "Target User", IsActive = true };

    // UTCID01 — Role=null → "Role is required." (Boundary)
    [Fact]
    public async Task UTCID01_Null_role_is_required()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(UserId, null, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Role is required.", res.Error);
        Assert.Null(res.ErrorCode);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID02 — Role="   " (whitespace) → "Role is required." (Boundary)
    [Fact]
    public async Task UTCID02_Whitespace_role_is_required()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(UserId, "   ", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Role is required.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID03 — Role="super_admin" (không được phép) → invalid role (Abnormal)
    [Fact]
    public async Task UTCID03_Disallowed_role_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(UserId, "super_admin", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Invalid role. Role must be 'hr_admin' or 'recruiter'.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID04 — Id == ActorId → chặn tự đổi vai trò của mình (Abnormal)
    [Fact]
    public async Task UTCID04_Changing_own_role_is_blocked()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(ActorA, "recruiter", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("You cannot change your own role.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID05 — user không tồn tại → not_found (Abnormal)
    [Fact]
    public async Task UTCID05_User_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(UserId, "recruiter", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("User not found.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID06 — user tồn tại, role hợp lệ → thành công + audit (Normal)
    [Fact]
    public async Task UTCID06_Valid_role_succeeds_with_audit()
    {
        var user = TargetUser(role: "recruiter");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(UserId, "recruiter", ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("recruiter", user.Role);
        Assert.Equal("user_role_updated", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID07 — Role=" HR_ADMIN " → chuẩn hoá trim + lower thành "hr_admin" (Boundary)
    [Fact]
    public async Task UTCID07_Role_is_normalized_trim_lower()
    {
        var user = TargetUser(role: "recruiter");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(UserId, " HR_ADMIN ", ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hr_admin", user.Role);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID08 — ActorId=null (không có actor) → vẫn thành công (Boundary)
    [Fact]
    public async Task UTCID08_Null_actor_still_succeeds()
    {
        var user = TargetUser(role: "recruiter");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(UserId, "recruiter", null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("recruiter", user.Role);
    }

    // UTCID09 — repository ném lỗi → thoát ra ngoài (Abnormal)
    [Fact]
    public async Task UTCID09_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new UpdateUserRoleCommand(UserId, "recruiter", ActorA), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
