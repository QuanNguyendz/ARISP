using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Quyết định vòng duyệt CV (ADR-048): mời phỏng vấn (<see cref="ApplicationService.SendInterviewInviteAsync"/>),
/// duyệt (<see cref="ApplicationService.AcceptApplicationAsync"/> → screening + tạo InterviewInvite + chuông,
/// KHÔNG gửi email — email gộp gửi 1 lần khi gán lịch) và từ chối
/// (<see cref="ApplicationService.RejectApplicationAsync"/> → cv_rejected + thư cảm ơn).
/// </summary>
public class CvDecisionTests
{
    private const string BaseUrl = "https://portal.test";
    private readonly Guid _accountId = Guid.NewGuid();

    private (InMemoryUnitOfWork uow, RecordingNotificationService notif, RecordingEmailService email, ARI.Domain.Entities.Application app, JobPosting job)
        Seed(string status = "cv_submitted", Guid? accountId = null, int ttlHours = 48)
    {
        var job = ApplicationData.Job(ttlHours: ttlHours);
        var app = ApplicationData.Application(job.Id, accountId, status: status);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);
        return (uow, new RecordingNotificationService(), new RecordingEmailService(), app, job);
    }

    private static ApplicationService Svc(InMemoryUnitOfWork uow, RecordingNotificationService notif, RecordingEmailService email)
        => ApplicationServiceFactory.Create(uow, notif, email, new RecordingRagIngestionService());

    // ---------- SendInterviewInviteAsync ----------

    [Fact]
    public async Task Send_invite_app_not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new RecordingNotificationService(), new RecordingEmailService())
            .SendInterviewInviteAsync(Guid.NewGuid(), BaseUrl, 1, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy hồ sơ", res.Error);
    }

    [Fact]
    public async Task Send_invite_creates_invite_promotes_to_screening_and_emails()
    {
        var (uow, notif, email, app, _) = Seed(status: "cv_submitted", ttlHours: 10);

        var res = await Svc(uow, notif, email).SendInterviewInviteAsync(app.Id, BaseUrl, 1, CancellationToken.None);

        Assert.True(res.IsSuccess);
        var invite = Assert.Single(uow.Repo<InterviewInvite>().Items);
        Assert.Equal(1, invite.RoundNumber);
        Assert.False(string.IsNullOrEmpty(invite.TokenHash));
        Assert.True(invite.ExpiresAt > DateTimeOffset.UtcNow.AddHours(9)); // TTL theo job (10h)
        Assert.Equal("screening", app.Status);
        Assert.Single(email.Sent);
    }

    [Fact]
    public async Task Send_invite_deletes_old_unused_invite_of_same_round()
    {
        var (uow, notif, email, app, _) = Seed();
        var old = new InterviewInvite { ApplicationId = app.Id, RoundNumber = 1, TokenHash = "OLD", ScheduledAt = null };
        uow.Seed(old);

        await Svc(uow, notif, email).SendInterviewInviteAsync(app.Id, BaseUrl, 1, CancellationToken.None);

        var invite = Assert.Single(uow.Repo<InterviewInvite>().Items); // cũ chưa dùng bị xoá, chỉ còn 1 mới
        Assert.NotEqual("OLD", invite.TokenHash);
    }

    [Fact]
    public async Task Send_invite_keeps_already_scheduled_invite()
    {
        var (uow, notif, email, app, _) = Seed();
        uow.Seed(new InterviewInvite { ApplicationId = app.Id, RoundNumber = 1, TokenHash = "SCHEDULED", ScheduledAt = DateTimeOffset.UtcNow });

        await Svc(uow, notif, email).SendInterviewInviteAsync(app.Id, BaseUrl, 1, CancellationToken.None);

        var round1 = uow.Repo<InterviewInvite>().Items.Where(i => i.RoundNumber == 1).ToList();
        Assert.Equal(2, round1.Count); // invite đã đặt lịch được giữ, thêm 1 invite mới
        Assert.Contains(round1, i => i.TokenHash == "SCHEDULED");
    }

    // ---------- AcceptApplicationAsync ----------

    [Fact]
    public async Task Accept_app_not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new RecordingNotificationService(), new RecordingEmailService())
            .AcceptApplicationAsync(Guid.NewGuid(), BaseUrl, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy hồ sơ", res.Error);
    }

    [Fact]
    public async Task Accept_wrong_status_fails()
    {
        var (uow, notif, email, app, _) = Seed(status: "interview");

        var res = await Svc(uow, notif, email).AcceptApplicationAsync(app.Id, BaseUrl, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Chỉ có thể duyệt", res.Error);
        Assert.Equal("interview", app.Status);
    }

    [Fact]
    public async Task Accept_promotes_creates_invite_and_notification()
    {
        var (uow, notif, email, app, _) = Seed(status: "cv_submitted", accountId: _accountId);

        var res = await Svc(uow, notif, email).AcceptApplicationAsync(app.Id, BaseUrl, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("screening", app.Status);
        Assert.Single(uow.Repo<InterviewInvite>().Items);
        Assert.Empty(email.Sent); // duyệt CV KHÔNG gửi email — email mời gộp gửi 1 lần khi gán lịch
        var record = Assert.Single(uow.Repo<Domain.Entities.Notification>().Items);
        Assert.Equal($"cv_accepted:{app.Id}", record.DedupKey);
        Assert.Contains(notif.UserEvents, e => e.UserId == _accountId && e.EventType == "ReceiveUserNotification");
    }

    [Fact]
    public async Task Accept_from_invited_status_succeeds()
    {
        var (uow, notif, email, app, _) = Seed(status: "invited");

        var res = await Svc(uow, notif, email).AcceptApplicationAsync(app.Id, BaseUrl, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("screening", app.Status);
    }

    // ---------- RejectApplicationAsync ----------

    [Fact]
    public async Task Reject_app_not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new RecordingNotificationService(), new RecordingEmailService())
            .RejectApplicationAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy hồ sơ", res.Error);
    }

    [Fact]
    public async Task Reject_wrong_status_fails()
    {
        var (uow, notif, email, app, _) = Seed(status: "cv_rejected");

        var res = await Svc(uow, notif, email).RejectApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Hồ sơ này đã ở trạng thái kết thúc", res.Error);
        Assert.Equal("cv_rejected", app.Status);
    }

    [Fact]
    public async Task Reject_sets_cv_rejected_emails_and_notifies()
    {
        var (uow, notif, email, app, _) = Seed(status: "cv_submitted", accountId: _accountId);

        var res = await Svc(uow, notif, email).RejectApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("cv_rejected", app.Status);
        Assert.Single(email.Sent);
        var record = Assert.Single(uow.Repo<Domain.Entities.Notification>().Items);
        Assert.Equal($"cv_rejected:{app.Id}", record.DedupKey);
        Assert.Contains(notif.UserEvents, e => e.UserId == _accountId && e.EventType == "ReceiveApplicationStatusUpdate");
    }

    [Fact]
    public async Task Reject_from_invited_status_succeeds()
    {
        var (uow, notif, email, app, _) = Seed(status: "invited");

        var res = await Svc(uow, notif, email).RejectApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("cv_rejected", app.Status);
    }
}
