using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.CreateStaffUser;
using ARI.Application.Common;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Super Admin tạo tài khoản staff (<see cref="CreateStaffUserCommandHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "CreateStaffUser" (UTCID01–10): bắt buộc email/họ tên, validate role, chuẩn hoá, trùng email,
/// DTO trả về, giá trị nullable, và lỗi repository.
///
/// <b>ADR-065:</b> đội/bộ phận nay là KHOÁ NGOẠI chứ không còn chuỗi tự do, nên các ca vốn truyền
/// <c>"IT"</c> nay truyền id của một đội đang hoạt động được gieo sẵn — tên đội đi ra DTO bằng join.
/// </summary>
public class CreateStaffUserCommandHandlerTests
{
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DeptIt = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static string InvalidRoleError
        => $"Role phải là một trong: {string.Join(", ", RoleNames.AssignableStaff)}.";

    private static Department ItDepartment() => new() { Id = DeptIt, Name = "IT" };

    private static CreateStaffUserCommandHandler Handler(
        InMemoryUnitOfWork uow, FakePasswordHasher hasher, RecordingEmailService email)
        => new(uow, hasher, email, AuthData.EmptyConfig());

    private static CreateStaffUserCommandHandler Handler(InMemoryUnitOfWork uow, RecordingEmailService? email = null)
        => Handler(uow, new FakePasswordHasher(), email ?? new RecordingEmailService());

    private static CreateStaffUserCommand Cmd(
        string email = "new@example.io", string fullName = "New Staff", string? role = "recruiter",
        Guid? departmentId = null, Guid? actorId = null)
        => new(email, fullName, role, departmentId, actorId ?? Guid.NewGuid());

    private static User ExistingUser(string email)
        => new() { Email = email, Role = "recruiter", FullName = "Existing", IsActive = true };

    // UTCID01 — Email="" → "Email là bắt buộc."
    [Fact]
    public async Task UTCID01_Empty_email_is_required()
    {
        var res = await Handler(new InMemoryUnitOfWork().Seed(ItDepartment()))
            .Handle(new CreateStaffUserCommand("", "Staff User", "recruiter", DeptIt, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email là bắt buộc.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID02 — Email="   " → "Email là bắt buộc."
    [Fact]
    public async Task UTCID02_Whitespace_email_is_required()
    {
        var res = await Handler(new InMemoryUnitOfWork().Seed(ItDepartment()))
            .Handle(new CreateStaffUserCommand("   ", "Staff User", "recruiter", DeptIt, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email là bắt buộc.", res.Error);
    }

    // UTCID03 — FullName="" → "Họ và tên là bắt buộc."
    [Fact]
    public async Task UTCID03_Empty_full_name_is_required()
    {
        var res = await Handler(new InMemoryUnitOfWork().Seed(ItDepartment()))
            .Handle(new CreateStaffUserCommand("staff@example.com", "", "recruiter", DeptIt, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Họ và tên là bắt buộc.", res.Error);
    }

    // UTCID04 — FullName="   " → "Họ và tên là bắt buộc."
    [Fact]
    public async Task UTCID04_Whitespace_full_name_is_required()
    {
        var res = await Handler(new InMemoryUnitOfWork().Seed(ItDepartment()))
            .Handle(new CreateStaffUserCommand("staff@example.com", "   ", "recruiter", DeptIt, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Họ và tên là bắt buộc.", res.Error);
    }

    // UTCID05 — Role=null → role không hợp lệ
    [Fact]
    public async Task UTCID05_Null_role_is_rejected()
    {
        var res = await Handler(new InMemoryUnitOfWork().Seed(ItDepartment()))
            .Handle(new CreateStaffUserCommand("staff@example.com", "Staff User", null, DeptIt, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(InvalidRoleError, res.Error);
    }

    // UTCID06 — Role="super_admin" (không được phép) → role không hợp lệ
    [Fact]
    public async Task UTCID06_Disallowed_role_is_rejected()
    {
        var res = await Handler(new InMemoryUnitOfWork().Seed(ItDepartment()))
            .Handle(new CreateStaffUserCommand("staff@example.com", "Staff User", "super_admin", DeptIt, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(InvalidRoleError, res.Error);
    }

    // UTCID07 — email (sau chuẩn hoá) trùng user đã có → conflict
    [Fact]
    public async Task UTCID07_Duplicate_email_is_conflict()
    {
        var uow = new InMemoryUnitOfWork().Seed(ItDepartment()).Seed(ExistingUser("staff@example.com"));

        var res = await Handler(uow)
            .Handle(new CreateStaffUserCommand(" STAFF@EXAMPLE.COM ", "Staff User", "recruiter", DeptIt, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email này đã được sử dụng bởi tài khoản khác.", res.Error);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    // UTCID08 — tạo thành công, DTO đúng giá trị (Normal)
    [Fact]
    public async Task UTCID08_Creates_staff_user_with_expected_dto()
    {
        var uow = new InMemoryUnitOfWork().Seed(ItDepartment());

        var res = await Handler(uow)
            .Handle(new CreateStaffUserCommand("staff@example.com", "Staff User", "recruiter", DeptIt, ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("staff@example.com", res.Value.Email);
        Assert.Equal("Staff User", res.Value.FullName);
        Assert.Equal("recruiter", res.Value.Role);
        Assert.Equal("IT", res.Value.Department);      // tên tra từ bảng departments, không phải chuỗi client gửi
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
        var uow = new InMemoryUnitOfWork().Seed(ItDepartment()).FailRepo<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(
                new CreateStaffUserCommand("staff@example.com", "Staff User", "recruiter", DeptIt, ActorA),
                CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }

    /// <summary>
    /// Đội đã TẮT không gán được: tài khoản gán vào đó sẽ không lập được phiếu mà cũng không có lỗi
    /// nào chỉ ra vì sao (ADR-065).
    /// </summary>
    [Fact]
    public async Task Inactive_department_is_rejected()
    {
        var uow = new InMemoryUnitOfWork().Seed(new Department { Id = DeptIt, Name = "IT", IsActive = false });

        var res = await Handler(uow).Handle(Cmd(departmentId: DeptIt), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không tồn tại hoặc đã ngừng hoạt động", res.Error);
        Assert.Empty(uow.Repo<User>().Items);
    }

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
    public async Task Valid_request_creates_hashed_user_audit_and_welcome_email()
    {
        var actor = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork();
        var hasher = new FakePasswordHasher();
        var email = new RecordingEmailService();

        var res = await Handler(uow, hasher, email).Handle(
            Cmd(email: "  New@X.io ", fullName: "  New Staff  ", role: "HR_Admin", actorId: actor),
            CancellationToken.None);

        Assert.True(res.IsSuccess);

        var user = Assert.Single(uow.Repo<User>().Items);
        Assert.Equal("new@x.io", user.Email);          // lower + trim
        Assert.Equal("hr_admin", user.Role);           // lower
        Assert.Equal("New Staff", user.FullName);      // trim
        Assert.Null(user.DepartmentId);                // ADR-065: đội gán riêng, không gõ tay
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

    /// <summary>
    /// Thư chào mừng là nơi DUY NHẤT mật khẩu tạm xuất hiện, nên nút trong đó phải dẫn tới trang đăng
    /// nhập thật. Trước đây ghi cứng "http://localhost:3001/login": sai route (route thật là
    /// <c>/auth/login</c>, còn <c>/login</c> rơi vào catch-all rồi chuyển hướng <c>/404</c>) và sai cả
    /// máy chủ trên bản deploy. Test khoá cả hai vế.
    /// </summary>
    [Fact]
    public async Task Welcome_email_links_to_configured_staff_login_page()
    {
        var uow = new InMemoryUnitOfWork();
        var email = new RecordingEmailService();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:AdminFrontendUrl"] = "https://staff.arisp.io.vn/",   // dấu / thừa phải bị cắt
            })
            .Build();

        var res = await new CreateStaffUserCommandHandler(uow, new FakePasswordHasher(), email, config)
            .Handle(Cmd(role: "hiring_manager"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var mail = Assert.Single(email.Sent);
        Assert.Contains("https://staff.arisp.io.vn/auth/login", mail.Html);
        Assert.DoesNotContain("localhost", mail.Html);
        Assert.DoesNotContain("//staff.arisp.io.vn//auth", mail.Html);
    }
}
