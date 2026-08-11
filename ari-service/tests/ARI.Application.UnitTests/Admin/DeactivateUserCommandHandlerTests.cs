using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.DeactivateUser;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Khóa tài khoản staff (<see cref="DeactivateUserCommandHandler"/>, test-plan B14): guard chống tự khóa
/// (TRƯỚC khi load), bắt buộc lý do, chặn khóa lại tài khoản đã khóa, và happy path (IsActive=false +
/// LockReason trim + audit 'user_deactivated' kèm lý do).
/// </summary>
public class DeactivateUserCommandHandlerTests
{
    private static DeactivateUserCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    [Fact]
    public async Task Locking_own_account_is_blocked_before_loading()
    {
        var me = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork(); // rỗng: guard chạy trước khi load user

        var res = await Handler(uow).Handle(new DeactivateUserCommand(me, "spam", me), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không thể khóa chính tài khoản", res.Error);
        Assert.Empty(uow.Repo<User>().Items);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Blank_reason_is_rejected(string? reason)
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new DeactivateUserCommand(user.Id, reason, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Vui lòng nhập lý do", res.Error);
        Assert.True(user.IsActive);   // chưa đụng vào user
    }

    [Fact]
    public async Task Already_locked_user_is_rejected()
    {
        var user = AuthData.Staff(active: false);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow).Handle(new DeactivateUserCommand(user.Id, "spam", Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã bị khóa", res.Error);
    }

    [Fact]
    public async Task Active_user_is_deactivated_with_reason_and_audit()
    {
        var user = AuthData.Staff(email: "u@x.io", active: true);
        var uow = new InMemoryUnitOfWork().Seed(user);
        var before = DateTimeOffset.UtcNow;

        var res = await Handler(uow).Handle(
            new DeactivateUserCommand(user.Id, "  Vi phạm quy định  ", Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(user.IsActive);
        Assert.Equal("Vi phạm quy định", user.LockReason);   // trim
        Assert.True(user.UpdatedAt >= before);

        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("user_deactivated", audit.Action);
        Assert.Equal(user.Id, audit.EntityId);
        Assert.Contains("reason", audit.Metadata);
        Assert.Contains("u@x.io", audit.Metadata);   // email nằm trong metadata (ASCII)
        Assert.Equal(1, uow.SaveChangesCount);
    }
}
