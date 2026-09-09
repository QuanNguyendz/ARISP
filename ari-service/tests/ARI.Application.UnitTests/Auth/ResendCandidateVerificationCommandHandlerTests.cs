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
/// Gửi lại email xác minh (<see cref="ResendCandidateVerificationCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "ResendCandidateVerification" (UTCID01–08): luôn Success (chống dò email); chỉ phát MagicLink
/// Audience=candidate_verify TTL 24h khi tài khoản tồn tại & CHƯA xác minh; chuẩn hoá email; lỗi từng phụ thuộc.
/// </summary>
public class ResendCandidateVerificationCommandHandlerTests
{
    private static ResendCandidateVerificationCommandHandler Handler(InMemoryUnitOfWork uow, RecordingEmailQueue email)
        => new(uow, AuthData.EmptyConfig(), email);

    private const string Email = "candidate@example.com";

    // UTCID01 — candidate chưa xác minh tồn tại → Success + MagicLink{candidate_verify, +24h, UsedAt=null} + email
    [Fact]
    public async Task UTCID01_Unverified_candidate_gets_verification_link()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false));
        var email = new RecordingEmailQueue();
        var before = DateTimeOffset.UtcNow;

        var res = await Handler(uow, email).Handle(new ResendCandidateVerificationCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var link = Assert.Single(uow.Repo<MagicLink>().Items);
        Assert.Equal(Email, link.Email);
        Assert.Equal(MagicLinkAudience.CandidateEmailVerify, link.Audience);
        Assert.Null(link.UsedAt);
        Assert.InRange(link.ExpiresAt, before.AddHours(24).AddMinutes(-1), before.AddHours(24).AddMinutes(1));
        Assert.Single(email.Items);
    }

    // UTCID02 — email cần chuẩn hoá → vẫn tìm ra + phát link
    [Fact]
    public async Task UTCID02_Email_is_normalized()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new ResendCandidateVerificationCommand(" CANDIDATE@EXAMPLE.COM "), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<MagicLink>().Items);
    }

    // UTCID03 — candidate không tồn tại → Success, không phát
    [Fact]
    public async Task UTCID03_Unknown_candidate_no_side_effects()
    {
        var uow = new InMemoryUnitOfWork(); var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new ResendCandidateVerificationCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
        Assert.Empty(email.Items);
    }

    // UTCID04 — candidate đã xác minh → Success, không phát
    [Fact]
    public async Task UTCID04_Already_verified_no_side_effects()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new ResendCandidateVerificationCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
    }

    // UTCID05 — candidate lookup ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID05_Candidate_lookup_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateAccount>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailQueue()).Handle(new ResendCandidateVerificationCommand(Email), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID06 — MagicLink AddAsync ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID06_MagicLink_add_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).FailAddFor<MagicLink>("Add Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailQueue()).Handle(new ResendCandidateVerificationCommand(Email), CancellationToken.None));

        Assert.Equal("Add Error", ex.Message);
    }

    // UTCID07 — SaveChangesAsync ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID07_Save_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).FailSaveOn(1, "Save Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailQueue()).Handle(new ResendCandidateVerificationCommand(Email), CancellationToken.None));

        Assert.Equal("Save Error", ex.Message);
    }

    // UTCID08 — email queue ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID08_Email_queue_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false));
        var email = new RecordingEmailQueue { EnqueueThrows = new Exception("Queue Error") };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, email).Handle(new ResendCandidateVerificationCommand(Email), CancellationToken.None));

        Assert.Equal("Queue Error", ex.Message);
    }
}
