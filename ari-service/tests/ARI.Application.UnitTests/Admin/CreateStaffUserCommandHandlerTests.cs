using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.CreateStaffUser;
using ARI.Application.Common;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Super Admin tạo tài khoản staff pre-provisioned (<see cref="CreateStaffUserCommandHandler"/>, test-plan B14):
/// thứ tự guard (email → tên → role hợp lệ), chống trùng email (normalize), và tạo user + audit + email chào mừng.
/// </summary>
public class CreateStaffUserCommandHandlerTests
{
    private static CreateStaffUserCommandHandler Handler(
        InMemoryUnitOfWork uow, FakePasswordHasher hasher, RecordingEmailService email)
        => new(uow, hasher, email);

    private static CreateStaffUserCommand Cmd(
        string email = "new@example.io", string fullName = "New Staff", string? role = "recruiter",
        string? department = "Engineering", Guid? actorId = null)
        => new(email, fullName, role, department, actorId ?? Guid.NewGuid());

    [Theory]
    [InlineData("super_admin")]
    [InlineData(null)]
    public async Task Invalid_role_is_rejected_without_side_effects(string? role)
    {
        var uow = new InMemoryUnitOfWork();
        var email = new RecordingEmailService();

        var res = await Handler(uow, new FakePasswordHasher(), email).Handle(Cmd(role: role), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Role phải là", res.Error);
        Assert.Empty(uow.Repo<User>().Items);
        Assert.Empty(uow.Repo<AuditLog>().Items);
        Assert.Empty(email.Sent);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Blank_full_name_short_circuits()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new FakePasswordHasher(), new RecordingEmailService())
            .Handle(Cmd(fullName: "   "), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Họ và tên là bắt buộc", res.Error);
        Assert.Empty(uow.Repo<User>().Items);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Duplicate_email_is_conflict_after_normalization()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: "a@x.com"));

        var res = await Handler(uow, new FakePasswordHasher(), new RecordingEmailService())
            .Handle(Cmd(email: " A@X.com "), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Contains("đã được sử dụng", res.Error);
    }

    [Fact]
    public async Task Valid_request_creates_hashed_user_audit_and_welcome_email()
    {
        var actor = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork();
        var hasher = new FakePasswordHasher();
        var email = new RecordingEmailService();

        var res = await Handler(uow, hasher, email).Handle(
            Cmd(email: "  New@X.io ", fullName: "  New Staff  ", role: "HR_Admin", department: "  Eng  ", actorId: actor),
            CancellationToken.None);

        Assert.True(res.IsSuccess);

        var user = Assert.Single(uow.Repo<User>().Items);
        Assert.Equal("new@x.io", user.Email);          // lower + trim
        Assert.Equal("hr_admin", user.Role);           // lower
        Assert.Equal("New Staff", user.FullName);      // trim
        Assert.Equal("Eng", user.Department);          // trim
        Assert.True(user.IsActive);
        Assert.StartsWith("hashed:", user.PasswordHash);           // đã hash, không phải plaintext
        Assert.True(user.PasswordHash!.Length > "hashed:".Length);

        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("staff_account_created", audit.Action);
        Assert.Equal("User", audit.EntityType);
        Assert.Equal(user.Id, audit.EntityId);
        Assert.Equal(1, uow.SaveChangesCount);

        var mail = Assert.Single(email.Sent);
        Assert.Equal("new@x.io", mail.To);

        // DTO echo
        Assert.Equal("new@x.io", res.Value.Email);
        Assert.Equal("hr_admin", res.Value.Role);
        Assert.Equal("New Staff", res.Value.FullName);
        Assert.True(res.Value.IsActive);
    }
}
