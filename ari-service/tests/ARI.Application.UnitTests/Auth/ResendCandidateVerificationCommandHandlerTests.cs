using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.ResendCandidateVerification;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Gửi lại email xác minh (test-plan B28): LUÔN Success (chống dò email); chỉ phát MagicLink
/// (Audience candidate_verify) + email khi tài khoản tồn tại VÀ chưa xác minh.
/// </summary>
public class ResendCandidateVerificationCommandHandlerTests
{
    private static ResendCandidateVerificationCommandHandler Handler(InMemoryUnitOfWork uow, RecordingEmailQueue email)
        => new(uow, AuthData.EmptyConfig(), email);

    [Fact]
    public async Task Unverified_candidate_gets_verification_link_and_email()
    {
        var candidate = AuthData.Candidate(email: "cand@example.io", verified: false);
        var uow = new InMemoryUnitOfWork().Seed(candidate);
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new ResendCandidateVerificationCommand("cand@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(MagicLinkAudience.CandidateEmailVerify, Assert.Single(uow.Repo<MagicLink>().Items).Audience);
        Assert.Single(email.Items);
    }

    [Fact]
    public async Task Already_verified_candidate_gets_nothing()
    {
        var candidate = AuthData.Candidate(email: "cand@example.io", verified: true);
        var uow = new InMemoryUnitOfWork().Seed(candidate);
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new ResendCandidateVerificationCommand("cand@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
        Assert.Empty(email.Items);
    }

    [Fact]
    public async Task Unknown_email_still_succeeds_without_side_effects()
    {
        var uow = new InMemoryUnitOfWork();
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new ResendCandidateVerificationCommand("nobody@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
        Assert.Empty(email.Items);
    }
}
