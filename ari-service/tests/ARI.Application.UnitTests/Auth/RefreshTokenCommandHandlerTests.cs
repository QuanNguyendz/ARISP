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
/// Rotation refresh token (test-plan B28): token không khớp → InvalidCredentials; hợp lệ → revoke token cũ +
/// phát cặp mới (JWT + refresh), Role đúng đối tượng (staff = user.Role, candidate = Candidate).
/// </summary>
public class RefreshStaffTokenCommandHandlerTests
{
    private static RefreshStaffTokenCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token) => new(uow, token);

    [Fact]
    public async Task Unknown_token_fails_invalid_credentials()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakeTokenService())
            .Handle(new RefreshStaffTokenCommand("raw-unknown"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    [Fact]
    public async Task Valid_token_revokes_old_and_issues_new_pair()
    {
        const string raw = "raw-refresh-staff";
        var user = AuthData.Staff(role: AppRoles.HrAdmin);
        var old = new RefreshToken { UserId = user.Id, TokenHash = TokenHashing.Sha256Base64(raw), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10) };
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(old);
        var token = new FakeTokenService { StaffToken = "new-staff-jwt" };

        var res = await Handler(uow, token).Handle(new RefreshStaffTokenCommand(raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("new-staff-jwt", res.Value.AccessToken);
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal(AppRoles.HrAdmin, res.Value.Role);
        Assert.NotNull(old.RevokedAt);                              // token cũ bị revoke
        Assert.Equal(2, uow.Repo<RefreshToken>().Items.Count);     // cũ (revoked) + mới
    }
}

/// <inheritdoc cref="RefreshStaffTokenCommandHandlerTests"/>
public class RefreshCandidateTokenCommandHandlerTests
{
    private static RefreshCandidateTokenCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token) => new(uow, token);

    [Fact]
    public async Task Unknown_token_fails_invalid_credentials()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakeTokenService())
            .Handle(new RefreshCandidateTokenCommand("raw-unknown"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, res.ErrorCode);
    }

    [Fact]
    public async Task Valid_token_revokes_old_and_issues_candidate_pair()
    {
        const string raw = "raw-refresh-candidate";
        var candidate = AuthData.Candidate();
        var old = new CandidateRefreshToken { CandidateAccountId = candidate.Id, TokenHash = TokenHashing.Sha256Base64(raw), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10) };
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(old);
        var token = new FakeTokenService { CandidateToken = "new-cand-jwt" };

        var res = await Handler(uow, token).Handle(new RefreshCandidateTokenCommand(raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("new-cand-jwt", res.Value.AccessToken);
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(2, uow.Repo<CandidateRefreshToken>().Items.Count);
    }
}
