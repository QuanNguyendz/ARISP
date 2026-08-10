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
/// Đăng nhập nội bộ pre-provisioned (<see cref="StaffLoginCommandHandler"/>, test-plan B13): email lạ →
/// không tạo draft; tài khoản khoá kiểm TRƯỚC khi Verify; tài khoản SSO-only (không mật khẩu); happy path
/// (đóng dấu LastLoginAt, cấp JWT staff + refresh, Role = user.Role).
/// </summary>
public class StaffLoginCommandHandlerTests
{
    private static StaffLoginCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token, FakePasswordHasher hasher)
        => new(uow, token, hasher);

    [Fact]
    public async Task Unprovisioned_email_fails_invalid_credentials_and_creates_no_account()
    {
        var uow = new InMemoryUnitOfWork();
        var hasher = new FakePasswordHasher();

        var res = await Handler(uow, new FakeTokenService(), hasher)
            .Handle(new StaffLoginCommand("ghost@example.io", "pw"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
        Assert.Empty(uow.Repo<User>().Items);      // không self-register
        Assert.Equal(0, hasher.VerifyCallCount);
    }

    [Fact]
    public async Task Disabled_account_is_rejected_before_verifying_password()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: "hr@example.io", active: false));
        var hasher = new FakePasswordHasher { VerifyResult = true };

        var res = await Handler(uow, new FakeTokenService(), hasher)
            .Handle(new StaffLoginCommand("hr@example.io", "pw"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.AccountDisabled, res.ErrorCode);
        Assert.Equal(0, hasher.VerifyCallCount);   // khoá kiểm trước Verify
    }

    [Fact]
    public async Task Sso_only_account_without_password_is_rejected()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: "hr@example.io", passwordHash: null));
        var hasher = new FakePasswordHasher();

        var res = await Handler(uow, new FakeTokenService(), hasher)
            .Handle(new StaffLoginCommand("hr@example.io", "pw"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.SsoOnly, res.ErrorCode);
        Assert.Equal(0, hasher.VerifyCallCount);
    }

    [Fact]
    public async Task Wrong_password_fails_invalid_credentials_without_token()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: "hr@example.io"));
        var token = new FakeTokenService();
        var hasher = new FakePasswordHasher { VerifyResult = false };

        var res = await Handler(uow, token, hasher)
            .Handle(new StaffLoginCommand("hr@example.io", "wrong"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
        Assert.Equal(0, token.StaffCount);
    }

    [Fact]
    public async Task Correct_password_logs_in_with_user_role()
    {
        var staff = AuthData.Staff(email: "hr@example.io", role: AppRoles.HrAdmin, fullName: "HR Boss");
        var uow = new InMemoryUnitOfWork().Seed(staff);
        var token = new FakeTokenService { StaffToken = "staff-jwt" };
        var hasher = new FakePasswordHasher { VerifyResult = true };

        var res = await Handler(uow, token, hasher)
            .Handle(new StaffLoginCommand("hr@example.io", "pw"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("staff-jwt", res.Value.AccessToken);
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal(AppRoles.HrAdmin, res.Value.Role);            // Role = user.Role
        Assert.Equal("HR Boss", res.Value.FullName);
        Assert.NotNull(staff.LastLoginAt);
        Assert.Single(uow.Repo<RefreshToken>().Items);
    }
}
