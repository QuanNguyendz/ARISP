using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Commands.StaffLogin;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đăng nhập staff bằng email + mật khẩu (<see cref="StaffLoginCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "StaffLogin" (UTCID01–15): happy path + fallback FullName, các guard (không tồn tại / inactive / SSO-only /
/// sai mật khẩu / khác hoa-thường), và 7 case lỗi phụ thuộc (lookup/verify/update/save/token/refresh add/refresh save).
/// </summary>
public class StaffLoginCommandHandlerTests
{
    private const string Email = "staff@example.com";
    private const string Password = "Password@123";

    private static StaffLoginCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token, FakePasswordHasher hasher)
        => new(uow, token, hasher);

    private static StaffLoginCommand Cmd(string email = Email, string password = Password) => new(email, password);

    // UTCID01 — staff active + mật khẩu đúng → Success
    [Fact]
    public async Task UTCID01_Active_staff_logs_in()
    {
        var user = AuthData.Staff(email: Email, active: true, fullName: "Staff User");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(string.IsNullOrEmpty(res.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal("Staff User", res.Value.FullName);
        Assert.Equal(user.Role, res.Value.Role);
        Assert.NotNull(user.LastLoginAt);
        Assert.Single(uow.Repo<RefreshToken>().Items);
    }

    // UTCID02 — FullName=null → fallback "Staff"
    [Fact]
    public async Task UTCID02_Null_full_name_falls_back()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true, fullName: null));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Staff", res.Value.FullName);
    }

    // UTCID03 — staff không tồn tại → invalid_credentials, không chạm hasher
    [Fact]
    public async Task UTCID03_Unknown_staff_invalid_credentials()
    {
        var hasher = new FakePasswordHasher();

        var res = await Handler(new InMemoryUnitOfWork(), new FakeTokenService(), hasher).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
        Assert.Equal(0, hasher.VerifyCallCount);
    }

    // UTCID04 — staff inactive → account_disabled
    [Fact]
    public async Task UTCID04_Inactive_staff_account_disabled()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: false));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.AccountDisabled, res.ErrorCode);
    }

    // UTCID05 — PasswordHash=null → sso_only
    [Fact]
    public async Task UTCID05_Null_password_hash_is_sso_only()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true, passwordHash: null));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.SsoOnly, res.ErrorCode);
    }

    // UTCID06 — PasswordHash="" → sso_only
    [Fact]
    public async Task UTCID06_Empty_password_hash_is_sso_only()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true, passwordHash: ""));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.SsoOnly, res.ErrorCode);
    }

    // UTCID07 — mật khẩu sai → invalid_credentials
    [Fact]
    public async Task UTCID07_Wrong_password_invalid_credentials()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = false })
            .Handle(Cmd(password: "WrongPassword"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID08 — email khác nhau chỉ ở hoa-thường (so khớp chính xác) → invalid_credentials
    [Fact]
    public async Task UTCID08_Case_only_difference_invalid_credentials()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true })
            .Handle(Cmd(email: "STAFF@EXAMPLE.COM"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID09 — user lookup ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID09_User_lookup_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID10 — password verifier ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID10_Verifier_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true));
        var hasher = new FakePasswordHasher { VerifyThrows = new Exception("Verify Error") };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), hasher).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Verify Error", ex.Message);
    }

    // UTCID11 — user Update ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID11_User_update_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).FailUpdateFor<User>("Update Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Update Error", ex.Message);
    }

    // UTCID12 — save (đóng dấu LastLogin) ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID12_Login_save_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).FailSaveOn(1, "Save Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Save Error", ex.Message);
    }

    // UTCID13 — token service ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID13_Token_service_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true));
        var token = new FakeTokenService { StaffThrows = new Exception("Token Error") };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, token, new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Token Error", ex.Message);
    }

    // UTCID14 — refresh-token AddAsync ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID14_Refresh_token_add_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).FailAddFor<RefreshToken>("Refresh Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Refresh Error", ex.Message);
    }

    // UTCID15 — refresh-token save (lần save thứ 2) ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID15_Refresh_token_save_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).FailSaveOn(2, "Refresh Save Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Refresh Save Error", ex.Message);
    }
}
