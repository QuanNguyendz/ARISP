using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.VerifyCandidateEmail;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Xác minh email ứng viên qua link (<see cref="VerifyCandidateEmailCommandHandler"/>, test-plan B13):
/// không candidate → lỗi; đã xác minh → idempotent; magic link hợp lệ → bật EmailVerified + đánh dấu UsedAt
/// (one-time); link hết hạn/đã dùng/sai audience/sai hash → từ chối, giữ EmailVerified=false.
/// </summary>
public class VerifyCandidateEmailCommandHandlerTests
{
    private static VerifyCandidateEmailCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    [Fact]
    public async Task Unknown_candidate_is_rejected()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new VerifyCandidateEmailCommand("nobody@example.io", "tok"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Liên kết xác minh không hợp lệ", res.Error);
    }

    [Fact]
    public async Task Already_verified_is_idempotent()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: "u@example.io", verified: true));

        var res = await Handler(uow)
            .Handle(new VerifyCandidateEmailCommand("u@example.io", "tok"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Contains("đã được xác minh trước đó", res.Value);
    }

    [Fact]
    public async Task Valid_link_verifies_email_and_burns_the_link()
    {
        var candidate = AuthData.Candidate(email: "u@example.io", verified: false);
        var link = AuthData.VerifyLink("u@example.io", token: "tok");
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(link);

        var res = await Handler(uow)
            .Handle(new VerifyCandidateEmailCommand("u@example.io", "tok"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Contains("thành công", res.Value);
        Assert.True(candidate.EmailVerified);
        Assert.NotNull(link.UsedAt);              // one-time: đã đánh dấu dùng
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("used")]
    [InlineData("wrong_audience")]
    [InlineData("wrong_hash")]
    public async Task Invalid_link_is_rejected_and_keeps_email_unverified(string flaw)
    {
        var candidate = AuthData.Candidate(email: "u@example.io", verified: false);
        var link = flaw switch
        {
            "expired" => AuthData.VerifyLink("u@example.io", token: "tok", expiresAt: DateTimeOffset.UtcNow.AddHours(-1)),
            "used" => AuthData.VerifyLink("u@example.io", token: "tok", usedAt: DateTimeOffset.UtcNow.AddMinutes(-5)),
            "wrong_audience" => AuthData.VerifyLink("u@example.io", token: "tok", audience: MagicLinkAudience.Candidate),
            _ => AuthData.VerifyLink("u@example.io", token: "other-hash"),
        };
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(link);

        var res = await Handler(uow)
            .Handle(new VerifyCandidateEmailCommand("u@example.io", "tok"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không hợp lệ hoặc đã hết hạn", res.Error);
        Assert.False(candidate.EmailVerified);
    }
}
