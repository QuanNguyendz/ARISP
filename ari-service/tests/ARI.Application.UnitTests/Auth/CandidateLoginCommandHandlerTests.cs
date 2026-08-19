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
/// Đăng nhập ứng viên bằng email + mật khẩu (<see cref="CandidateLoginCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "CandidateLogin" (UTCID01–12): happy path + chuẩn hoá + fallback FullName, các guard theo mã lỗi, và lỗi phụ thuộc.
/// </summary>
/// <remarks>
/// Report ghi thông điệp lỗi cũ cho UTCID04/07 ("Invalid email or password.") và UTCID05/06
/// ("Tài khoản này đăng ký qua Google…"). Handler hiện tại đổi câu chữ (ADR-035) nhưng GIỮ NGUYÊN mã lỗi
/// (invalid_credentials / passwordless_google / email_not_verified). Test bám mã lỗi + thông điệp thật để luôn xanh.
/// </remarks>
public class CandidateLoginCommandHandlerTests
{
    private const string Email = "candidate@example.com";
    private const string Password = "Password@123";

    private static CandidateLoginCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token, FakePasswordHasher hasher)
        => new(uow, token, hasher);

    private static CandidateLoginCommand Cmd(string email = Email, string password = Password) => new(email, password);

    // UTCID01 — candidate đã xác minh + mật khẩu đúng → Success
    [Fact]
    public async Task UTCID01_Verified_candidate_logs_in()
    {
        var candidate = AuthData.Candidate(email: Email, verified: true, fullName: "Candidate User");
        var uow = new InMemoryUnitOfWork().Seed(candidate);

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(string.IsNullOrEmpty(res.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal("Candidate User", res.Value.FullName);
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
        Assert.NotNull(candidate.LastLoginAt);
        Assert.Single(uow.Repo<CandidateRefreshToken>().Items);
    }

    // UTCID02 — FullName=null → fallback "Candidate"
    [Fact]
    public async Task UTCID02_Null_full_name_falls_back()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true, fullName: null));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Candidate", res.Value.FullName);
    }

    // UTCID03 — email cần chuẩn hoá → vẫn đăng nhập được
    [Fact]
    public async Task UTCID03_Email_is_normalized()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true, fullName: "Candidate User"));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true })
            .Handle(Cmd(email: " CANDIDATE@EXAMPLE.COM "), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Candidate User", res.Value.FullName);
    }

    // UTCID04 — candidate không tồn tại → invalid_credentials, KHÔNG chạm hasher
    [Fact]
    public async Task UTCID04_Unknown_candidate_invalid_credentials()
    {
        var hasher = new FakePasswordHasher();

        var res = await Handler(new InMemoryUnitOfWork(), new FakeTokenService(), hasher).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
        Assert.Equal(0, hasher.VerifyCallCount);
    }

    // UTCID05 — PasswordHash=null → passwordless_google
    [Fact]
    public async Task UTCID05_Null_password_hash_is_passwordless_google()
    {
        var candidate = AuthData.Candidate(email: Email, verified: true);
        candidate.PasswordHash = null;
        var uow = new InMemoryUnitOfWork().Seed(candidate);
        var hasher = new FakePasswordHasher();

        var res = await Handler(uow, new FakeTokenService(), hasher).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.PasswordlessGoogle, res.ErrorCode);
        Assert.Equal(0, hasher.VerifyCallCount);
    }

    // UTCID06 — PasswordHash="" → passwordless_google
    [Fact]
    public async Task UTCID06_Empty_password_hash_is_passwordless_google()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true, passwordHash: ""));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.PasswordlessGoogle, res.ErrorCode);
    }

    // UTCID07 — mật khẩu sai → invalid_credentials
    [Fact]
    public async Task UTCID07_Wrong_password_invalid_credentials()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true));

        var res = await Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = false })
            .Handle(Cmd(password: "WrongPassword"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID08 — email chưa xác minh → email_not_verified, không cấp token
    [Fact]
    public async Task UTCID08_Unverified_email_blocked()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false));
        var token = new FakeTokenService();

        var res = await Handler(uow, token, new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.EmailNotVerified, res.ErrorCode);
        Assert.Equal("Tài khoản chưa được xác minh. Vui lòng kiểm tra email để kích hoạt.", res.Error);
        Assert.Equal(0, token.CandidateCount);
        Assert.Empty(uow.Repo<CandidateRefreshToken>().Items);
    }

    // UTCID09 — candidate lookup ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID09_Candidate_lookup_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateAccount>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID10 — password verifier ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID10_Verifier_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true));
        var hasher = new FakePasswordHasher { VerifyThrows = new Exception("Verify Error") };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), hasher).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Verify Error", ex.Message);
    }

    // UTCID11 — token service ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID11_Token_service_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true));
        var token = new FakeTokenService { CandidateThrows = new Exception("Token Error") };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, token, new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Token Error", ex.Message);
    }

    // UTCID12 — lưu refresh-token ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID12_Refresh_token_persistence_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true)).FailAddFor<CandidateRefreshToken>("Refresh Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new FakeTokenService(), new FakePasswordHasher { VerifyResult = true }).Handle(Cmd(), CancellationToken.None));

        Assert.Equal("Refresh Error", ex.Message);
    }
}
