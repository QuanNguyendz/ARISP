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
/// Super Admin tạo tài khoản staff (<see cref="CreateStaffUserCommandHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "CreateStaffUser" (UTCID01–10): bắt buộc email/họ tên, validate role, chuẩn hoá, trùng email,
/// DTO trả về, giá trị nullable, và lỗi repository.
/// </summary>
public class CreateStaffUserCommandHandlerTests
{
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static CreateStaffUserCommandHandler Handler(InMemoryUnitOfWork uow, RecordingEmailService? email = null)
        => new(uow, new FakePasswordHasher(), email ?? new RecordingEmailService());

    private static User ExistingUser(string email)
        => new() { Email = email, Role = "recruiter", FullName = "Existing", IsActive = true };

    // UTCID01 — Email="" → "Email là bắt buộc."
    [Fact]
    public async Task UTCID01_Empty_email_is_required()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new CreateStaffUserCommand("", "Staff User", "recruiter", "IT", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email là bắt buộc.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID02 — Email="   " → "Email là bắt buộc."
    [Fact]
    public async Task UTCID02_Whitespace_email_is_required()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new CreateStaffUserCommand("   ", "Staff User", "recruiter", "IT", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email là bắt buộc.", res.Error);
    }

    // UTCID03 — FullName="" → "Họ và tên là bắt buộc."
    [Fact]
    public async Task UTCID03_Empty_full_name_is_required()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new CreateStaffUserCommand("staff@example.com", "", "recruiter", "IT", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Họ và tên là bắt buộc.", res.Error);
    }

    // UTCID04 — FullName="   " → "Họ và tên là bắt buộc."
    [Fact]
    public async Task UTCID04_Whitespace_full_name_is_required()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new CreateStaffUserCommand("staff@example.com", "   ", "recruiter", "IT", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Họ và tên là bắt buộc.", res.Error);
    }

    // UTCID05 — Role=null → role không hợp lệ
    [Fact]
    public async Task UTCID05_Null_role_is_rejected()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new CreateStaffUserCommand("staff@example.com", "Staff User", null, "IT", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Role phải là 'hr_admin' hoặc 'recruiter'.", res.Error);
    }

    // UTCID06 — Role="super_admin" (không được phép) → role không hợp lệ
    [Fact]
    public async Task UTCID06_Disallowed_role_is_rejected()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new CreateStaffUserCommand("staff@example.com", "Staff User", "super_admin", "IT", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Role phải là 'hr_admin' hoặc 'recruiter'.", res.Error);
    }

    // UTCID07 — email (sau chuẩn hoá) trùng user đã có → conflict
    [Fact]
    public async Task UTCID07_Duplicate_email_is_conflict()
    {
        var uow = new InMemoryUnitOfWork().Seed(ExistingUser("staff@example.com"));

        var res = await Handler(uow)
            .Handle(new CreateStaffUserCommand(" STAFF@EXAMPLE.COM ", "Staff User", "recruiter", "IT", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email này đã được sử dụng bởi tài khoản khác.", res.Error);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    // UTCID08 — tạo thành công, DTO đúng giá trị (Normal)
    [Fact]
    public async Task UTCID08_Creates_staff_user_with_expected_dto()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow)
            .Handle(new CreateStaffUserCommand("staff@example.com", "Staff User", "recruiter", "IT", ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("staff@example.com", res.Value.Email);
        Assert.Equal("Staff User", res.Value.FullName);
        Assert.Equal("recruiter", res.Value.Role);
        Assert.Equal("IT", res.Value.Department);
        Assert.True(res.Value.IsActive);
        Assert.Equal("staff_account_created", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Single(uow.Repo<User>().Items);
    }

    // UTCID09 — chuẩn hoá email/họ tên/role + Department null + ActorId null (Boundary)
    [Fact]
    public async Task UTCID09_Normalizes_values_with_nullable_department_and_actor()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow)
            .Handle(new CreateStaffUserCommand(" HR@EXAMPLE.COM ", " HR User ", " HR_ADMIN ", null, null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hr@example.com", res.Value.Email);
        Assert.Equal("HR User", res.Value.FullName);
        Assert.Equal("hr_admin", res.Value.Role);
        Assert.Null(res.Value.Department);
        Assert.True(res.Value.IsActive);
    }

    // UTCID10 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID10_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new CreateStaffUserCommand("staff@example.com", "Staff User", "recruiter", "IT", ActorA), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
