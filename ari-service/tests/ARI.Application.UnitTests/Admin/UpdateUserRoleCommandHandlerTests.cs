using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.UpdateUserRole;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Đổi vai trò staff (<see cref="UpdateUserRoleCommandHandler"/>, test-plan B24): role không hợp lệ → lỗi,
/// chống tự đổi vai trò mình, và happy path chuẩn hoá lower + audit 'user_role_updated'.
/// </summary>
public class UpdateUserRoleCommandHandlerTests
{
    private static UpdateUserRoleCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    [Fact]
    public async Task Invalid_role_is_rejected()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(user.Id, "super_admin", Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Invalid role", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Changing_own_role_is_blocked()
    {
        var me = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork();

        // Role hợp lệ nhưng ActorId == Id → chặn (kiểm sau validate role, trước load user).
        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(me, "hr_admin", me), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("cannot change your own role", res.Error);
        Assert.Empty(uow.Repo<User>().Items);
    }

    [Fact]
    public async Task Valid_role_is_normalized_lower_with_audit()
    {
        var user = AuthData.Staff(role: "recruiter");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new UpdateUserRoleCommand(user.Id, "  HR_Admin  ", Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hr_admin", user.Role);   // trim + lower
        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("user_role_updated", audit.Action);
        Assert.Equal(1, uow.SaveChangesCount);
    }
}
