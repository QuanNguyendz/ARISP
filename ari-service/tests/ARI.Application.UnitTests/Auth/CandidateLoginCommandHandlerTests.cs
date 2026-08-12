using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Commands.CandidateLogin;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đăng nhập ứng viên bằng email + mật khẩu (<see cref="CandidateLoginCommandHandler"/>, test-plan B13):
/// thứ tự guard (không account → không Verify; Google passwordless; email chưa xác minh) và happy path
/// (đóng dấu LastLoginAt, cấp access + refresh token, Role Candidate, FullName fallback "Candidate").
/// </summary>
public class CandidateLoginCommandHandlerTests
{
    private static CandidateLoginCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token, FakePasswordHasher hasher)
        => new(uow, token, hasher);

    [Fact]
    public async Task Unknown_account_fails_invalid_credentials_without_verifying()
    {
        var hasher = new FakePasswordHasher();
        var res = await Handler(new InMemoryUnitOfWork(), new FakeTokenService(), hasher)
            .Handle(new CandidateLoginCommand("nobody@example.io", "pw"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
        Assert.Equal(0, hasher.VerifyCallCount); // không chạm hasher khi không có account
    }

    [Fact]
    public async Task Passwordless_google_account_is_redirected_to_google()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: "g@example.io", passwordHash: ""));
        var hasher = new FakePasswordHasher();

        var res = await Handler(uow, new FakeTokenService(), hasher)
            .Handle(new CandidateLoginCommand("g@example.io", "pw"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.PasswordlessGoogle, res.ErrorCode);
        Assert.Contains("Google", res.Error);
        Assert.Equal(0, hasher.VerifyCallCount);
    }

    [Fact]
    public async Task Unverified_email_is_blocked_even_with_correct_password()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: "u@example.io", verified: false));
        var token = new FakeTokenService();
        var hasher = new FakePasswordHasher { VerifyResult = true };

        var res = await Handler(uow, token, hasher)
            .Handle(new CandidateLoginCommand("u@example.io", "pw"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.EmailNotVerified, res.ErrorCode);
        Assert.Equal(0, token.CandidateCount);                     // không cấp token
        Assert.Empty(uow.Repo<CandidateRefreshToken>().Items);     // không cấp refresh
    }

    [Fact]
    public async Task Verified_candidate_with_correct_password_logs_in()
    {
        var candidate = AuthData.Candidate(email: "u@example.io", verified: true, fullName: null); // fullName null → fallback
        var uow = new InMemoryUnitOfWork().Seed(candidate);
        var token = new FakeTokenService { CandidateToken = "acc-jwt" };
        var hasher = new FakePasswordHasher { VerifyResult = true };

        var res = await Handler(uow, token, hasher)
            .Handle(new CandidateLoginCommand("u@example.io", "pw"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("acc-jwt", res.Value.AccessToken);
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
        Assert.Equal("Candidate", res.Value.FullName);             // fallback khi FullName null
        Assert.NotNull(candidate.LastLoginAt);
        Assert.Single(uow.Repo<CandidateRefreshToken>().Items);    // refresh token đã lưu
    }
}
