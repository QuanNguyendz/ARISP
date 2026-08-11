using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.Logout;
using ARI.Application.Common.Security;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đăng xuất — revoke refresh token (test-plan B28): LUÔN Success; null/khoảng trắng → no-op; token khớp
/// bảng staff hoặc candidate → đặt RevokedAt; token lạ → no-op.
/// </summary>
public class LogoutCommandHandlerTests
{
    private static LogoutCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    [Fact]
    public async Task Null_token_is_a_no_op_success()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new LogoutCommand(null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Staff_token_is_revoked()
    {
        const string raw = "raw-staff";
        var rt = new RefreshToken { UserId = Guid.NewGuid(), TokenHash = TokenHashing.Sha256Base64(raw), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10) };
        var uow = new InMemoryUnitOfWork().Seed(rt);

        var res = await Handler(uow).Handle(new LogoutCommand(raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(rt.RevokedAt);
    }

    [Fact]
    public async Task Candidate_token_is_revoked()
    {
        const string raw = "raw-cand";
        var rt = new CandidateRefreshToken { CandidateAccountId = Guid.NewGuid(), TokenHash = TokenHashing.Sha256Base64(raw), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10) };
        var uow = new InMemoryUnitOfWork().Seed(rt);

        var res = await Handler(uow).Handle(new LogoutCommand(raw), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(rt.RevokedAt);
    }

    [Fact]
    public async Task Unknown_token_is_a_no_op_success()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new LogoutCommand("does-not-exist"), CancellationToken.None);

        Assert.True(res.IsSuccess);
    }
}
