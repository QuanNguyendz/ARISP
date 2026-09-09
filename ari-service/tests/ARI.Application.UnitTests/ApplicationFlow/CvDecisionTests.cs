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
/// Quyết định vòng duyệt CV (ADR-048/059): mở vòng (<see cref="ApplicationService.OpenRoundForSchedulingAsync"/>
/// → screening + đánh dấu vòng đang hoạt động, KHÔNG gửi email — thư mời kèm giờ hẹn do bước xếp lịch gửi),
/// duyệt (<see cref="ApplicationService.AcceptApplicationAsync"/>) và từ chối
/// (<see cref="ApplicationService.RejectApplicationAsync"/> → cv_rejected + thư cảm ơn).
/// </summary>
public class CvDecisionTests
{
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

    // ---------- OpenRoundForSchedulingAsync ----------

    [Fact]
    public async Task Open_round_app_not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new RecordingNotificationService(), new RecordingEmailService())
            .OpenRoundForSchedulingAsync(Guid.NewGuid(), 1, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy hồ sơ", res.Error);
    }

    [Fact]
    public async Task Open_round_creates_invite_and_promotes_to_screening_without_email()
    {
        var (uow, notif, email, app, _) = Seed(status: "cv_submitted", ttlHours: 10);

        var res = await Svc(uow, notif, email).OpenRoundForSchedulingAsync(app.Id, 1, CancellationToken.None);

        Assert.True(res.IsSuccess);
        var invite = Assert.Single(uow.Repo<InterviewInvite>().Items);
        Assert.Equal(1, invite.RoundNumber);
        Assert.False(string.IsNullOrEmpty(invite.TokenHash));
        Assert.True(invite.ExpiresAt > DateTimeOffset.UtcNow.AddHours(9)); // TTL theo job (10h)
        Assert.Equal("screening", app.Status);
        Assert.Empty(email.Sent); // ADR-059: mở vòng không gửi thư; thư mời kèm giờ hẹn do bước xếp lịch gửi
    }

    [Fact]
    public async Task Open_round_deletes_old_unused_invite_of_same_round()
    {
        var (uow, notif, email, app, _) = Seed();
        var old = new InterviewInvite { ApplicationId = app.Id, RoundNumber = 1, TokenHash = "OLD", ScheduledAt = null };
        uow.Seed(old);

        await Svc(uow, notif, email).OpenRoundForSchedulingAsync(app.Id, 1, CancellationToken.None);

        var invite = Assert.Single(uow.Repo<InterviewInvite>().Items); // cũ chưa dùng bị xoá, chỉ còn 1 mới
        Assert.NotEqual("OLD", invite.TokenHash);
    }

    [Fact]
    public async Task Open_round_keeps_already_scheduled_invite()
    {
        var (uow, notif, email, app, _) = Seed();
        uow.Seed(new InterviewInvite { ApplicationId = app.Id, RoundNumber = 1, TokenHash = "SCHEDULED", ScheduledAt = DateTimeOffset.UtcNow });

        await Svc(uow, notif, email).OpenRoundForSchedulingAsync(app.Id, 1, CancellationToken.None);

        var round1 = uow.Repo<InterviewInvite>().Items.Where(i => i.RoundNumber == 1).ToList();
        Assert.Equal(2, round1.Count); // invite đã đặt lịch được giữ, thêm 1 invite mới
        Assert.Contains(round1, i => i.TokenHash == "SCHEDULED");
    }

    // ---------- AcceptApplicationAsync ----------

    [Fact]
    public async Task Accept_app_not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new RecordingNotificationService(), new RecordingEmailService())
            .AcceptApplicationAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy hồ sơ", res.Error);
    }

    [Fact]
    public async Task Accept_wrong_status_fails()
    {
        var (uow, notif, email, app, _) = Seed(status: "interview");

        var res = await Svc(uow, notif, email).AcceptApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Chỉ có thể duyệt", res.Error);
        Assert.Equal("interview", app.Status);
    }

    [Fact]
    public async Task Accept_promotes_and_opens_the_round_without_telling_the_candidate()
    {
        // ADR-067: bước này là chuyển động NỘI BỘ (Recruiter duyệt → HM duyệt → về hàng chờ xếp lịch).
        // Ứng viên chỉ được báo MỘT lần, khi đã có giờ hẹn cụ thể — trước đó họ nhận tin vui rồi ngồi
        // im không biết bao lâu, và nếu Hiring Manager từ chối sau đó thì tin vui ấy thành sai.
        var (uow, notif, email, app, _) = Seed(status: "cv_submitted", accountId: _accountId);

        var res = await Svc(uow, notif, email).AcceptApplicationAsync(app.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("screening", app.Status);
        Assert.Single(uow.Repo<InterviewInvite>().Items);
        Assert.Empty(email.Sent);
        Assert.Empty(uow.Repo<Domain.Entities.Notification>().Items);
        Assert.DoesNotContain(notif.UserEvents, e => e.UserId == _accountId);
    }

    [Fact]
    public async Task Accept_from_invited_status_succeeds()
    {
        var (uow, notif, email, app, _) = Seed(status: "invited");

        var res = await Svc(uow, notif, email).AcceptApplicationAsync(app.Id, CancellationToken.None);

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
        // Thư cảm ơn nay đi qua CandidateEmailSender (INotificationService.SendThreadedEmailAsync)
        // — cùng lối với thư mời phỏng vấn, để mọi thư gửi ứng viên đều được ghi EmailLog ở một
        // chỗ duy nhất thay vì mỗi call site tự gửi tự nhớ (ADR-061, Phase 4).
        Assert.Single(notif.Emails);
        Assert.Single(uow.Repo<Domain.Entities.EmailLog>().Items);
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
