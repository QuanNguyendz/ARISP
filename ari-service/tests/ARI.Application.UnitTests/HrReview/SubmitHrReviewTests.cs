using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Evaluations;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.HrReview;

/// <summary>
/// HR Review &amp; Confirm/Override (Phase 6, <see cref="InterviewService.SubmitHrReviewAsync"/>):
/// Confirm khi đồng ý AI (mọi nhân sự), Override khi đổi verdict (chỉ HR Admin/Super Admin + bắt buộc lý do),
/// cập nhật trạng thái hồ sơ, auto-progression sang vòng kế (ADR-017, chỉ với real), thông báo ứng viên
/// (realtime + notification chống trùng) và ghi audit log confirm/override.
/// </summary>
public class SubmitHrReviewTests
{
    private readonly Guid _hrId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    private Task<Result<bool>> Run(InMemoryUnitOfWork uow, RecordingNotificationService notif, ConfirmReviewRequest request, Guid? hrUserId = null)
        => InterviewServiceFactory.Create(uow, notif)
            .SubmitHrReviewAsync(hrUserId ?? _hrId, request, frontendBaseUrl: null, CancellationToken.None);

    /// <summary>Dựng bối cảnh đủ để review: job + application + evaluation + người review (mặc định HR Admin).</summary>
    private (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app, Evaluation eval)
        Seed(string aiVerdict = "pass", string sessionType = "real", int round = 1,
             string reviewerRole = AppRoles.HrAdmin)
    {
        var job = HrReviewData.Job();
        var app = HrReviewData.Application(job.Id, _accountId);
        var eval = HrReviewData.Evaluation(app.Id, aiVerdict, sessionType, round);
        var reviewer = HrReviewData.Reviewer(reviewerRole);
        reviewer.Id = _hrId;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval).Seed(reviewer);
        return (uow, job, app, eval);
    }

    // ---------- Confirm (đồng ý với AI) ----------

    [Fact]
    public async Task Confirm_pass_records_review_and_sets_application_pass()
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "pass");
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.False(review.IsOverride);
        Assert.Equal("pass", review.FinalVerdict);
        Assert.Equal("pass", app.Status);
        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("hr_confirm", audit.Action);
    }

    [Fact]
    public async Task Confirm_not_pass_sets_application_not_pass()
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "not_pass");
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, HrReviewData.Request(eval.Id, "not_pass"));

        Assert.True(res.IsSuccess);
        Assert.Equal("not_pass", app.Status);
        Assert.False(Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items).IsOverride);
    }

    [Fact]
    public async Task Recruiter_can_confirm_matching_verdict()
    {
        // Confirm KHÔNG phải override → không giới hạn vai trò (Recruiter được phép).
        var (uow, _, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.Recruiter);

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.Equal("pass", app.Status);
    }

    // ---------- Override (đổi verdict của AI) ----------

    [Fact]
    public async Task Override_by_hr_admin_with_reason_succeeds()
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "not_pass", reviewerRole: AppRoles.HrAdmin);
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, HrReviewData.Request(eval.Id, "pass", overrideReason: "Kỹ năng thực tế vượt điểm AI"));

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.True(review.IsOverride);
        Assert.Equal("Kỹ năng thực tế vượt điểm AI", review.OverrideReason);
        Assert.Equal("pass", app.Status);
        Assert.Equal("hr_override", Assert.Single(uow.Repo<AuditLog>().Items).Action);
    }

    [Fact]
    public async Task Override_by_super_admin_succeeds()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.SuperAdmin);

        var res = await Run(uow, new RecordingNotificationService(),
            HrReviewData.Request(eval.Id, "not_pass", overrideReason: "Phát hiện gian lận"));

        Assert.True(res.IsSuccess);
        Assert.True(Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items).IsOverride);
    }

    [Fact]
    public async Task Override_without_reason_fails_and_persists_nothing()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "not_pass", reviewerRole: AppRoles.HrAdmin);

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass", overrideReason: null));

        Assert.True(res.IsFailure);
        Assert.Contains("Override reason", res.Error);
        Assert.Empty(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.Empty(uow.Repo<AuditLog>().Items);
    }

    [Fact]
    public async Task Override_by_recruiter_is_forbidden()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "not_pass", reviewerRole: AppRoles.Recruiter);

        var res = await Run(uow, new RecordingNotificationService(),
            HrReviewData.Request(eval.Id, "pass", overrideReason: "Tôi thấy ổn"));

        Assert.True(res.IsFailure);
        Assert.Contains("HR Admin or Super Admin", res.Error);
        Assert.Empty(uow.Repo<Domain.Entities.HrReview>().Items);
    }

    // ---------- Not found ----------

    [Fact]
    public async Task Missing_evaluation_returns_failure()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(Guid.NewGuid(), "pass"));

        Assert.True(res.IsFailure);
        Assert.Contains("Evaluation report not found", res.Error);
    }

    [Fact]
    public async Task Missing_hr_user_returns_failure()
    {
        var (uow, _, _, eval) = Seed();
        uow.Repo<User>().Items.Clear(); // xóa người review đã seed

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsFailure);
        Assert.Contains("HR User not found", res.Error);
    }

    // ---------- Auto-progression sang vòng kế (ADR-017) ----------

    [Fact]
    public async Task Pass_real_with_next_round_config_progresses_and_creates_invite()
    {
        var (uow, job, app, eval) = Seed(aiVerdict: "pass", sessionType: "real", round: 1);
        uow.Seed(HrReviewData.RoundConfig(job.Id, round: 2)); // có cấu hình vòng 2

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        var invite = Assert.Single(uow.Repo<InterviewInvite>().Items);
        Assert.Equal(2, invite.RoundNumber);
        Assert.Equal(app.Id, invite.ApplicationId);
        Assert.Equal("interview", app.Status); // progression ghi đè "pass" → tiếp tục phỏng vấn
    }

    [Fact]
    public async Task Pass_real_without_next_round_config_does_not_progress()
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "pass", sessionType: "real", round: 1);
        // Không seed RoundConfig cho vòng 2 → vòng cuối.

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<InterviewInvite>().Items);
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Pass_practice_does_not_progress_even_with_config()
    {
        var (uow, job, app, eval) = Seed(aiVerdict: "pass", sessionType: "practice", round: 1);
        uow.Seed(HrReviewData.RoundConfig(job.Id, round: 2));

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<InterviewInvite>().Items); // practice không kích hoạt vòng kế
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Not_pass_never_progresses()
    {
        var (uow, job, app, eval) = Seed(aiVerdict: "not_pass", sessionType: "real", round: 1);
        uow.Seed(HrReviewData.RoundConfig(job.Id, round: 2));

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "not_pass"));

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<InterviewInvite>().Items);
        Assert.Equal("not_pass", app.Status);
    }

    // ---------- Thông báo ứng viên ----------

    [Fact]
    public async Task Notifies_candidate_realtime_and_creates_notification_record()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "pass");
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.Contains(notif.UserEvents, e => e.UserId == _accountId && e.EventType == "ReceiveApplicationStatusUpdate");
        Assert.Contains(notif.UserEvents, e => e.UserId == _accountId && e.EventType == "ReceiveUserNotification");
        var record = Assert.Single(uow.Repo<Domain.Entities.Notification>().Items);
        Assert.Equal($"hr_review:{eval.Id}", record.DedupKey);
        Assert.Equal("result", record.Type);
    }

    [Fact]
    public async Task Notification_is_deduplicated()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "pass");
        uow.Seed(new Domain.Entities.Notification
        {
            CandidateAccountId = _accountId,
            DedupKey = $"hr_review:{eval.Id}",
            Type = "result",
        });

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<Domain.Entities.Notification>().Items); // không thêm bản trùng
    }

    [Fact]
    public async Task No_candidate_account_skips_realtime_but_still_succeeds()
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "pass");
        app.CandidateAccountId = null; // hồ sơ nộp bằng email, không có tài khoản Job Board
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.Empty(notif.UserEvents);
        Assert.Empty(uow.Repo<Domain.Entities.Notification>().Items);
        Assert.Equal("pass", app.Status);
        Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items); // luồng chính vẫn hoàn tất
    }
}
