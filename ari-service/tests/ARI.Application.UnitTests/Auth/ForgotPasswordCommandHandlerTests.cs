using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.CandidateForgotPassword;
using ARI.Application.Auth.Commands.StaffForgotPassword;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Quên mật khẩu (test-plan B28): LUÔN Success (chống dò email); chỉ phát MagicLink (Audience đúng cổng,
/// TTL ~2h) + email khi tài khoản hợp lệ (staff phải active). Email không tồn tại → không tạo gì.
/// </summary>
public class StaffForgotPasswordCommandHandlerTests
{
    private static StaffForgotPasswordCommandHandler Handler(InMemoryUnitOfWork uow, RecordingEmailQueue email)
        => new(uow, AuthData.EmptyConfig(), email);

    [Fact]
    public async Task Active_staff_gets_staff_magic_link_and_email()
    {
        var user = AuthData.Staff(email: "hr@example.io", active: true);
        var uow = new InMemoryUnitOfWork().Seed(user);
        var email = new RecordingEmailQueue();
        var before = DateTimeOffset.UtcNow;

        var res = await Handler(uow, email).Handle(new StaffForgotPasswordCommand("hr@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var link = Assert.Single(uow.Repo<MagicLink>().Items);
        Assert.Equal(MagicLinkAudience.Staff, link.Audience);
        Assert.InRange(link.ExpiresAt, before.AddHours(2).AddMinutes(-1), before.AddHours(2).AddMinutes(1));
        Assert.Single(email.Items);
    }

    [Fact]
    public async Task Unknown_email_still_succeeds_without_side_effects()
    {
        var uow = new InMemoryUnitOfWork();
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new StaffForgotPasswordCommand("nobody@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);        // anti-enumeration
        Assert.Empty(uow.Repo<MagicLink>().Items);
        Assert.Empty(email.Items);
    }
}

/// <inheritdoc cref="StaffForgotPasswordCommandHandlerTests"/>
public class CandidateForgotPasswordCommandHandlerTests
{
    private static CandidateForgotPasswordCommandHandler Handler(InMemoryUnitOfWork uow, RecordingEmailQueue email)
        => new(uow, AuthData.EmptyConfig(), email);

    [Fact]
    public async Task Existing_candidate_gets_candidate_magic_link_and_email()
    {
        var candidate = AuthData.Candidate(email: "cand@example.io");
        var uow = new InMemoryUnitOfWork().Seed(candidate);
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new CandidateForgotPasswordCommand("cand@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(MagicLinkAudience.Candidate, Assert.Single(uow.Repo<MagicLink>().Items).Audience);
        Assert.Single(email.Items);
    }

    [Fact]
    public async Task Unknown_email_still_succeeds_without_side_effects()
    {
        var uow = new InMemoryUnitOfWork();
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new CandidateForgotPasswordCommand("nobody@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
        Assert.Empty(email.Items);
    }
}
