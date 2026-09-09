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
/// Quên mật khẩu CANDIDATE (<see cref="CandidateForgotPasswordCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "CandidateForgotPassword" (UTCID01–08): luôn Success (chống dò email), phát MagicLink Audience=candidate
/// TTL 2h khi tài khoản tồn tại, chuẩn hoá email, FullName null vẫn chạy, và lỗi từng phụ thuộc.
/// </summary>
public class CandidateForgotPasswordCommandHandlerTests
{
    private static CandidateForgotPasswordCommandHandler Handler(InMemoryUnitOfWork uow, RecordingEmailQueue email)
        => new(uow, AuthData.EmptyConfig(), email);

    private const string Email = "candidate@example.com";

    // UTCID01 — candidate không tồn tại → Success, không phát MagicLink
    [Fact]
    public async Task UTCID01_Unknown_candidate_succeeds_without_side_effects()
    {
        var uow = new InMemoryUnitOfWork(); var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new CandidateForgotPasswordCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
        Assert.Empty(email.Items);
    }

    // UTCID02 — candidate tồn tại → Success + MagicLink{candidate, +2h, UsedAt=null} + email
    [Fact]
    public async Task UTCID02_Existing_candidate_gets_magic_link()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var email = new RecordingEmailQueue();
        var before = DateTimeOffset.UtcNow;

        var res = await Handler(uow, email).Handle(new CandidateForgotPasswordCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var link = Assert.Single(uow.Repo<MagicLink>().Items);
        Assert.Equal(Email, link.Email);
        Assert.Equal(MagicLinkAudience.Candidate, link.Audience);
        Assert.Null(link.UsedAt);
        Assert.InRange(link.ExpiresAt, before.AddHours(2).AddMinutes(-1), before.AddHours(2).AddMinutes(1));
        Assert.Single(email.Items);
    }

    // UTCID03 — email cần chuẩn hoá (" CANDIDATE@EXAMPLE.COM ") → vẫn tìm ra + phát link
    [Fact]
    public async Task UTCID03_Email_is_normalized()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new CandidateForgotPasswordCommand(" CANDIDATE@EXAMPLE.COM "), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<MagicLink>().Items);
    }

    // UTCID04 — candidate có FullName=null → vẫn Success + MagicLink (email fallback "Candidate")
    [Fact]
    public async Task UTCID04_Null_full_name_still_works()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, fullName: null));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new CandidateForgotPasswordCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<MagicLink>().Items);
    }

    // UTCID05 — candidate lookup ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID05_Candidate_lookup_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateAccount>("Candidate DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailQueue()).Handle(new CandidateForgotPasswordCommand(Email), CancellationToken.None));

        Assert.Equal("Candidate DB Error", ex.Message);
    }

    // UTCID06 — MagicLink AddAsync ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID06_MagicLink_add_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email)).FailAddFor<MagicLink>("Add Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailQueue()).Handle(new CandidateForgotPasswordCommand(Email), CancellationToken.None));

        Assert.Equal("Add Error", ex.Message);
    }

    // UTCID07 — SaveChangesAsync ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID07_Save_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email)).FailSaveOn(1, "Save Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailQueue()).Handle(new CandidateForgotPasswordCommand(Email), CancellationToken.None));

        Assert.Equal("Save Error", ex.Message);
    }

    // UTCID08 — email queue ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID08_Email_queue_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var email = new RecordingEmailQueue { EnqueueThrows = new Exception("Queue Error") };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, email).Handle(new CandidateForgotPasswordCommand(Email), CancellationToken.None));

        Assert.Equal("Queue Error", ex.Message);
    }
}

/// <summary>
/// Quên mật khẩu STAFF (<see cref="StaffForgotPasswordCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "StaffForgotPassword" (UTCID01–08): luôn Success (chống dò email); chỉ phát MagicLink Audience=staff TTL 2h
/// khi user active (kể cả SSO-only); không tồn tại / inactive / khác hoa-thường → không phát; lỗi phụ thuộc.
/// </summary>
public class StaffForgotPasswordCommandHandlerTests
{
    private static StaffForgotPasswordCommandHandler Handler(InMemoryUnitOfWork uow, RecordingEmailQueue email)
        => new(uow, AuthData.EmptyConfig(), email);

    private const string Email = "staff@example.com";

    // UTCID01 — staff active có mật khẩu → MagicLink{staff, +2h} + Success + email
    [Fact]
    public async Task UTCID01_Active_staff_gets_magic_link()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true));
        var email = new RecordingEmailQueue();
        var before = DateTimeOffset.UtcNow;

        var res = await Handler(uow, email).Handle(new StaffForgotPasswordCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var link = Assert.Single(uow.Repo<MagicLink>().Items);
        Assert.Equal(Email, link.Email);
        Assert.Equal(MagicLinkAudience.Staff, link.Audience);
        Assert.Null(link.UsedAt);
        Assert.InRange(link.ExpiresAt, before.AddHours(2).AddMinutes(-1), before.AddHours(2).AddMinutes(1));
        Assert.Single(email.Items);
    }

    // UTCID02 — staff active SSO-only (không mật khẩu) → vẫn phát MagicLink
    [Fact]
    public async Task UTCID02_Active_sso_only_staff_gets_magic_link()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true, passwordHash: null));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new StaffForgotPasswordCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<MagicLink>().Items);
    }

    // UTCID03 — staff active FullName=null → vẫn phát MagicLink
    [Fact]
    public async Task UTCID03_Active_staff_null_full_name()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true, fullName: null));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new StaffForgotPasswordCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<MagicLink>().Items);
    }

    // UTCID04 — staff không tồn tại → Success, không phát
    [Fact]
    public async Task UTCID04_Unknown_staff_no_side_effects()
    {
        var uow = new InMemoryUnitOfWork(); var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new StaffForgotPasswordCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
        Assert.Empty(email.Items);
    }

    // UTCID05 — staff inactive → Success, không phát
    [Fact]
    public async Task UTCID05_Inactive_staff_no_side_effects()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: false));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new StaffForgotPasswordCommand(Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
    }

    // UTCID06 — email khác nhau chỉ ở hoa-thường (handler so khớp chính xác) → không phát
    [Fact]
    public async Task UTCID06_Case_only_difference_does_not_match()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, email).Handle(new StaffForgotPasswordCommand("STAFF@EXAMPLE.COM"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<MagicLink>().Items);
    }

    // UTCID07 — user lookup ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID07_User_lookup_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailQueue()).Handle(new StaffForgotPasswordCommand(Email), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID08 — MagicLink AddAsync ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID08_MagicLink_add_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).FailAddFor<MagicLink>("Add Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailQueue()).Handle(new StaffForgotPasswordCommand(Email), CancellationToken.None));

        Assert.Equal("Add Error", ex.Message);
    }
}
