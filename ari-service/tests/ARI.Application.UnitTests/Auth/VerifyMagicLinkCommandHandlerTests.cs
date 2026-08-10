using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Commands.VerifyMagicLink;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đăng nhập passwordless ứng viên qua magic link (<see cref="VerifyMagicLinkCommandHandler"/>, test-plan B13):
/// tra cứu theo email đã normalize → mint JWT candidate. GHI NHẬN gap chủ ý: handler KHÔNG đối chiếu token
/// (không kiểm TTL/one-time) — chỉ cần email khớp là cấp JWT.
/// </summary>
public class VerifyMagicLinkCommandHandlerTests
{
    private static VerifyMagicLinkCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token)
        => new(uow, token);

    [Fact]
    public async Task Unknown_email_fails_not_found_without_minting_token()
    {
        var token = new FakeTokenService();
        var res = await Handler(new InMemoryUnitOfWork(), token)
            .Handle(new VerifyMagicLinkCommand("nobody@example.io", "t"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.NotFound, res.ErrorCode);
        Assert.Contains("Candidate account not found", res.Error);
        Assert.Equal(0, token.CandidateCount);
    }

    [Fact]
    public async Task Existing_candidate_gets_the_minted_jwt()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: "user@example.io"));
        var token = new FakeTokenService { CandidateToken = "jwt-123" };

        var res = await Handler(uow, token)
            .Handle(new VerifyMagicLinkCommand("user@example.io", "whatever"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("jwt-123", res.Value);
        Assert.Equal(1, token.CandidateCount);
    }

    [Fact]
    public async Task Token_is_not_validated_so_garbage_token_still_succeeds()
    {
        // Gap chủ ý: không kiểm token → token rác/hết hạn vẫn ra JWT khi email khớp.
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: "user@example.io"));
        var token = new FakeTokenService();

        var res = await Handler(uow, token)
            .Handle(new VerifyMagicLinkCommand("user@example.io", "expired-garbage-token"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, token.CandidateCount);
    }

    [Fact]
    public async Task Email_is_normalized_before_lookup()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: "User@X.com"));
        var token = new FakeTokenService();

        var res = await Handler(uow, token)
            .Handle(new VerifyMagicLinkCommand("  USER@X.com ", "t"), CancellationToken.None);

        Assert.True(res.IsSuccess); // trim + lower khớp candidate → không NotFound giả
    }
}
