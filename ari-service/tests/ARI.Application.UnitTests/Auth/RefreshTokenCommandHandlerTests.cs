using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Commands.RefreshCandidateToken;
using ARI.Application.Auth.Commands.RefreshStaffToken;
using ARI.Application.Common.Security;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Rotation refresh token CANDIDATE (<see cref="RefreshCandidateTokenCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "RefreshCandidateToken" (UTCID01–14): happy path (revoke cũ + phát cặp mới) + fallback FullName,
/// token không tồn tại/hết hạn/đã revoke/biên hết hạn, candidate không tồn tại, và lỗi phụ thuộc.
/// </summary>
public class RefreshCandidateTokenCommandHandlerTests
{
    private const string Raw = "valid-refresh-token";

    private static RefreshCandidateTokenCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token) => new(uow, token);

    private static CandidateRefreshToken Tok(Guid candId, string raw = Raw, DateTimeOffset? expiresAt = null, DateTimeOffset? revokedAt = null)
        => new() { CandidateAccountId = candId, TokenHash = TokenHashing.Sha256Base64(raw), ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(10), RevokedAt = revokedAt };

    // UTCID01 — token + candidate hợp lệ → Success, revoke cũ + phát mới
    [Fact]
    public async Task UTCID01_Valid_rotation()
    {
        var candidate = AuthData.Candidate(fullName: "Candidate User");
        var old = Tok(candidate.Id);
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(old);

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(string.IsNullOrEmpty(res.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal("Candidate User", res.Value.FullName);
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(2, uow.Repo<CandidateRefreshToken>().Items.Count);
    }

    // UTCID02 — candidate FullName=null → fallback "Candidate"
    [Fact]
    public async Task UTCID02_Null_full_name_falls_back()
    {
        var candidate = AuthData.Candidate(fullName: null);
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id));

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Candidate", res.Value.FullName);
    }

    // UTCID03 — token không tồn tại → invalid_credentials
    [Fact]
    public async Task UTCID03_Unknown_token()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakeTokenService())
            .Handle(new RefreshCandidateTokenCommand("unknown-token"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID04 — token hết hạn → invalid_credentials
    [Fact]
    public async Task UTCID04_Expired_token()
    {
        var candidate = AuthData.Candidate();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id, raw: "expired-token", expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand("expired-token"), CancellationToken.None);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID05 — token đã revoke → invalid_credentials
    [Fact]
    public async Task UTCID05_Revoked_token()
    {
        var candidate = AuthData.Candidate();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id, raw: "revoked-token", revokedAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand("revoked-token"), CancellationToken.None);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID06 — candidate mà token trỏ tới không tồn tại → "Candidate not found."
    [Fact]
    public async Task UTCID06_Orphan_token_candidate_missing()
    {
        var uow = new InMemoryUnitOfWork().Seed(Tok(Guid.NewGuid(), raw: "orphan-token"));

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand("orphan-token"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Candidate not found.", res.Error);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID07 — refresh-token lookup ném lỗi
    [Fact]
    public async Task UTCID07_Token_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateRefreshToken>("DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID08 — refresh-token Update ném lỗi
    [Fact]
    public async Task UTCID08_Token_update_error()
    {
        var candidate = AuthData.Candidate();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id)).FailUpdateFor<CandidateRefreshToken>("Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Update Error", ex.Message);
    }

    // UTCID09 — save (revoke) ném lỗi
    [Fact]
    public async Task UTCID09_Revoke_save_error()
    {
        var candidate = AuthData.Candidate();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id)).FailSaveOn(1, "Revoke Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Revoke Save Error", ex.Message);
    }

    // UTCID10 — candidate lookup ném lỗi
    [Fact]
    public async Task UTCID10_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Tok(Guid.NewGuid())).FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    // UTCID11 — token service ném lỗi
    [Fact]
    public async Task UTCID11_Token_service_error()
    {
        var candidate = AuthData.Candidate();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id));
        var token = new FakeTokenService { CandidateThrows = new Exception("Token Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, token).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Token Error", ex.Message);
    }

    // UTCID12 — refresh-token mới AddAsync ném lỗi
    [Fact]
    public async Task UTCID12_New_refresh_add_error()
    {
        var candidate = AuthData.Candidate();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id)).FailAddFor<CandidateRefreshToken>("Refresh Add Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Refresh Add Error", ex.Message);
    }

    // UTCID13 — save (phát refresh mới, lần 2) ném lỗi
    [Fact]
    public async Task UTCID13_Refresh_save_error()
    {
        var candidate = AuthData.Candidate();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id)).FailSaveOn(2, "Refresh Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Refresh Save Error", ex.Message);
    }

    // UTCID14 — token hết hạn đúng thời điểm hiện tại (ExpiresAt <= now) → invalid_credentials
    [Fact]
    public async Task UTCID14_Expires_now_boundary()
    {
        var candidate = AuthData.Candidate();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Tok(candidate.Id, raw: "expires-now-token", expiresAt: DateTimeOffset.UtcNow));

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshCandidateTokenCommand("expires-now-token"), CancellationToken.None);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }
}

/// <summary>
/// Rotation refresh token STAFF (<see cref="RefreshStaffTokenCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "RefreshStaffTokenCommandHandler" (UTCID01–15): happy path + fallback FullName, staff inactive VẪN thành công
/// (handler không kiểm IsActive), token không tồn tại/hết hạn/đã revoke/biên, user không tồn tại, và lỗi phụ thuộc.
/// </summary>
public class RefreshStaffTokenCommandHandlerTests
{
    private const string Raw = "valid-refresh-token";

    private static RefreshStaffTokenCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token) => new(uow, token);

    private static RefreshToken Tok(Guid userId, string raw = Raw, DateTimeOffset? expiresAt = null, DateTimeOffset? revokedAt = null)
        => new() { UserId = userId, TokenHash = TokenHashing.Sha256Base64(raw), ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(10), RevokedAt = revokedAt };

    // UTCID01 — token + staff hợp lệ → Success, revoke cũ + phát mới
    [Fact]
    public async Task UTCID01_Valid_rotation()
    {
        var user = AuthData.Staff(fullName: "Staff User");
        var old = Tok(user.Id);
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(old);

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(string.IsNullOrEmpty(res.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal("Staff User", res.Value.FullName);
        Assert.Equal(user.Role, res.Value.Role);
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(2, uow.Repo<RefreshToken>().Items.Count);
    }

    // UTCID02 — staff FullName=null → fallback ""
    [Fact]
    public async Task UTCID02_Null_full_name_falls_back_to_empty()
    {
        var user = AuthData.Staff(fullName: null);
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id));

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.FullName);
    }

    // UTCID03 — staff inactive → VẪN thành công (handler không kiểm IsActive)
    [Fact]
    public async Task UTCID03_Inactive_staff_still_succeeds()
    {
        var user = AuthData.Staff(active: false);
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id));

        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
    }

    // UTCID04 — token không tồn tại → invalid_credentials
    [Fact]
    public async Task UTCID04_Unknown_token()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakeTokenService())
            .Handle(new RefreshStaffTokenCommand("unknown-token"), CancellationToken.None);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID05 — token hết hạn → invalid_credentials
    [Fact]
    public async Task UTCID05_Expired_token()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id, raw: "expired-token", expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));
        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand("expired-token"), CancellationToken.None);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID06 — token đã revoke → invalid_credentials
    [Fact]
    public async Task UTCID06_Revoked_token()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id, raw: "revoked-token", revokedAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand("revoked-token"), CancellationToken.None);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID07 — user mà token trỏ tới không tồn tại → "User not found."
    [Fact]
    public async Task UTCID07_Orphan_token_user_missing()
    {
        var uow = new InMemoryUnitOfWork().Seed(Tok(Guid.NewGuid(), raw: "orphan-token"));
        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand("orphan-token"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("User not found.", res.Error);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    // UTCID08 — refresh-token lookup ném lỗi
    [Fact]
    public async Task UTCID08_Token_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<RefreshToken>("DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID09 — refresh-token Update ném lỗi
    [Fact]
    public async Task UTCID09_Token_update_error()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id)).FailUpdateFor<RefreshToken>("Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Update Error", ex.Message);
    }

    // UTCID10 — save (revoke) ném lỗi
    [Fact]
    public async Task UTCID10_Revoke_save_error()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id)).FailSaveOn(1, "Revoke Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Revoke Save Error", ex.Message);
    }

    // UTCID11 — user lookup ném lỗi
    [Fact]
    public async Task UTCID11_User_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Tok(Guid.NewGuid())).FailGetByIdFor<User>("User DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("User DB Error", ex.Message);
    }

    // UTCID12 — token service ném lỗi
    [Fact]
    public async Task UTCID12_Token_service_error()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id));
        var token = new FakeTokenService { StaffThrows = new Exception("Token Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, token).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Token Error", ex.Message);
    }

    // UTCID13 — refresh-token mới AddAsync ném lỗi
    [Fact]
    public async Task UTCID13_New_refresh_add_error()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id)).FailAddFor<RefreshToken>("Refresh Add Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Refresh Add Error", ex.Message);
    }

    // UTCID14 — save (phát refresh mới, lần 2) ném lỗi
    [Fact]
    public async Task UTCID14_Refresh_save_error()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id)).FailSaveOn(2, "Refresh Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand(Raw), CancellationToken.None));
        Assert.Equal("Refresh Save Error", ex.Message);
    }

    // UTCID15 — token hết hạn đúng thời điểm hiện tại (ExpiresAt <= now) → invalid_credentials
    [Fact]
    public async Task UTCID15_Expires_now_boundary()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(Tok(user.Id, raw: "expires-now-token", expiresAt: DateTimeOffset.UtcNow));
        var res = await Handler(uow, new FakeTokenService()).Handle(new RefreshStaffTokenCommand("expires-now-token"), CancellationToken.None);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }
}
