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
/// Chốt kết quả phỏng vấn (Phase 6, <see cref="InterviewService.SubmitHrReviewAsync"/>): người chốt là
/// Hiring Manager chính của tin (ADR-061); quản trị viên chỉ chốt THAY — luôn kèm lý do (ADR-068 bỏ nhánh
/// "tin chưa gán HM thì chốt tự do"). Override (đổi verdict) bắt buộc lý do; cập nhật trạng thái hồ sơ,
/// auto-progression sang vòng kế (ADR-017, chỉ với real), thông báo ứng viên (realtime + notification
/// chống trùng), giao việc soạn thư mời khi qua vòng cuối, và ghi audit log.
/// </summary>
public class SubmitHrReviewTests
{
    private const string FallbackReason = "Hiring Manager nghỉ phép dài ngày, ứng viên cần trả lời gấp";

    private readonly Guid _hrId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    /// <summary>Hiring Manager chính của tin — ADR-068: mọi tin đều có đúng một người.</summary>
    private Guid _hmId;

    private Task<Result<bool>> Run(InMemoryUnitOfWork uow, RecordingNotificationService notif, ConfirmReviewRequest request, Guid? hrUserId = null)
        => InterviewServiceFactory.Create(uow, notif)
            .SubmitHrReviewAsync(hrUserId ?? _hrId, request, frontendBaseUrl: null, CancellationToken.None);

    /// <summary>
    /// Dựng bối cảnh đủ để chốt: job (có HM chính) + application + evaluation + người chốt.
    /// Mặc định người chốt CHÍNH LÀ Hiring Manager của tin; vai trò khác thì tin có một HM riêng.
    /// </summary>
    private (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app, Evaluation eval)
        Seed(string aiVerdict = "pass", string sessionType = "real", int round = 1,
             string reviewerRole = AppRoles.HiringManager, bool withHiringManager = true)
    {
        var job = HrReviewData.Job();
        var app = HrReviewData.Application(job.Id, _accountId);
        var eval = HrReviewData.Evaluation(app.Id, aiVerdict, sessionType, round);
        var reviewer = HrReviewData.Reviewer(reviewerRole);
        reviewer.Id = _hrId;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval).Seed(reviewer);

        if (withHiringManager)
        {
            if (RoleNames.Is(reviewerRole, RoleNames.HiringManager))
            {
                _hmId = _hrId;
            }
            else
            {
                _hmId = Guid.NewGuid();
                uow.Seed(new User { Id = _hmId, Email = "hm@corp.io", Role = RoleNames.HiringManager, IsActive = true });
            }

            uow.Seed(new JobHiringTeamMember
            {
                JobPostingId = job.Id, UserId = _hmId, RoleOnJob = JobTeamRoles.HiringManager,
                IsPrimary = true, AddedByUserId = job.CreatedByUserId,
            });
        }

        return (uow, job, app, eval);
    }

    private static ConfirmReviewRequest WithFallback(ConfirmReviewRequest request)
    {
        request.FallbackReason = FallbackReason;
        return request;
    }

    /// <summary>Thông báo gửi ỨNG VIÊN (khác thông báo giao việc cho nhân sự).</summary>
    private Domain.Entities.Notification[] CandidateNotices(InMemoryUnitOfWork uow)
        => uow.Repo<Domain.Entities.Notification>().Items.Where(n => n.CandidateAccountId == _accountId).ToArray();

    // ---------- Hiring Manager chốt (đồng ý với AI) ----------

    [Fact]
    public async Task Confirm_pass_records_review_and_sets_application_pass()
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "pass");
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.False(review.IsOverride);
        Assert.False(review.IsHrFallback);
        Assert.Equal("pass", review.FinalVerdict);
        Assert.Equal(RoleNames.HiringManager, review.ReviewerRole); // ảnh chụp vai trò lúc chốt
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
    public async Task Recruiter_cannot_confirm_at_all()
    {
        // ADR-061: chủ tin VẬN HÀNH phễu (xếp lịch, cấp mã, gửi thư), KHÔNG quyết định tuyển.
        var (uow, _, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.Recruiter);

        var res = await Run(uow, new RecordingNotificationService(), WithFallback(HrReviewData.Request(eval.Id, "pass")));

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.Equal("interview", app.Status); // không đụng tới trạng thái hồ sơ
    }

    // ---------- Override (đổi verdict của AI) ----------

    [Fact]
    public async Task Hiring_manager_may_override_the_ai_verdict()
    {
        // Trước ADR-061 chỉ quản trị viên được ghi đè, nên người hiểu công việc nhất lại không
        // sửa được kết luận sai của mô hình.
        var (uow, _, app, eval) = Seed(aiVerdict: "not_pass");

        var res = await Run(uow, new RecordingNotificationService(),
            HrReviewData.Request(eval.Id, "pass", overrideReason: "Ứng viên làm đúng bài thực tế của nhóm"));

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.True(review.IsOverride);
        Assert.Equal("hr_override", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Override_without_reason_fails_and_persists_nothing()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "not_pass");

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass", overrideReason: null));

        Assert.True(res.IsFailure);
        Assert.Contains("Override reason", res.Error);
        Assert.Empty(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.Empty(uow.Repo<AuditLog>().Items);
    }

    [Theory]
    [InlineData(AppRoles.HrAdmin)]
    [InlineData(AppRoles.SuperAdmin)]
    public async Task Admin_override_on_behalf_of_the_hm_needs_both_reasons(string adminRole)
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "not_pass", reviewerRole: adminRole);

        var res = await Run(uow, new RecordingNotificationService(),
            WithFallback(HrReviewData.Request(eval.Id, "pass", overrideReason: "Kỹ năng thực tế vượt điểm AI")));

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.True(review.IsOverride);
        Assert.True(review.IsHrFallback);
        Assert.Equal("Kỹ năng thực tế vượt điểm AI", review.OverrideReason);
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "hr_override");
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "hr_review_fallback");
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Override_by_recruiter_is_forbidden()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "not_pass", reviewerRole: AppRoles.Recruiter);

        var res = await Run(uow, new RecordingNotificationService(),
            WithFallback(HrReviewData.Request(eval.Id, "pass", overrideReason: "Tôi thấy ổn")));

        Assert.True(res.IsFailure);
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
        // ADR-053: còn vòng sau → trạng thái là "interview" ngay từ đầu (trước đây đặt "pass"
        // rồi mới bị auto-progression ghi đè).
        Assert.Equal("interview", app.Status);
        // Chưa qua vòng cuối → chưa giao việc soạn thư mời cho ai.
        Assert.DoesNotContain(uow.Repo<Domain.Entities.Notification>().Items, n => n.DedupKey!.StartsWith("offer_needed:"));
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
    public async Task Passing_the_final_round_tells_the_owner_and_the_hm_to_draft_the_offer()
    {
        // ADR-063: HM chính hoặc chủ tin soạn thư mời. Trước đây không nhân sự nào được báo ở bước này —
        // ứng viên nhận thư "thư mời sẽ được gửi tới Anh/Chị" còn phía công ty không ai có việc trong tay.
        var (uow, job, app, eval) = Seed(aiVerdict: "pass", sessionType: "real", round: 1);
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        var tasks = uow.Repo<Domain.Entities.Notification>().Items
            .Where(n => n.DedupKey!.StartsWith($"offer_needed:{app.Id}:")).ToList();
        Assert.Contains(tasks, n => n.RecipientUserId == job.CreatedByUserId);
        Assert.Contains(tasks, n => n.RecipientUserId == _hmId && n.Link == $"/hm/candidates/{app.Id}");
        Assert.Contains(notif.UserEvents, e => e.UserId == _hmId && e.EventType == "ReceiveUserNotification");
    }

    [Fact]
    public async Task Pass_practice_does_not_progress_even_with_config()
    {
        var (uow, job, app, eval) = Seed(aiVerdict: "pass", sessionType: "practice", round: 1);
        uow.Seed(HrReviewData.RoundConfig(job.Id, round: 2));

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<InterviewInvite>().Items); // practice không kích hoạt vòng kế
        // ADR-051: buổi thử KHÔNG chạm pipeline tuyển dụng — trạng thái hồ sơ giữ nguyên như trước
        // khi review (trước đây review một buổi thử vẫn đẩy hồ sơ sang "pass").
        Assert.Equal("interview", app.Status);
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
        var record = Assert.Single(CandidateNotices(uow));
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
        Assert.Single(CandidateNotices(uow)); // không thêm bản trùng
    }

    [Fact]
    public async Task No_candidate_account_skips_realtime_but_still_succeeds()
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "pass");
        app.CandidateAccountId = null; // hồ sơ nộp bằng email, không có tài khoản Job Board
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.DoesNotContain(notif.UserEvents, e => e.EventType == "ReceiveApplicationStatusUpdate");
        Assert.DoesNotContain(uow.Repo<Domain.Entities.Notification>().Items, n => n.CandidateAccountId != null);
        Assert.Equal("pass", app.Status);
        Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items); // luồng chính vẫn hoàn tất
    }

    // ---------- Quản trị viên chốt THAY Hiring Manager ----------

    [Fact]
    public async Task Admin_confirming_without_a_reason_is_refused()
    {
        var (uow, _, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.HrAdmin);

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.Equal("interview", app.Status);
    }

    [Fact]
    public async Task Admin_fallback_with_reason_is_recorded_and_notifies_the_bypassed_hm()
    {
        // Một quyết định tuyển đi qua đầu người phụ trách mà họ không biết là thất bại quản trị,
        // dù lý do có chính đáng.
        var (uow, _, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.HrAdmin);

        var res = await Run(uow, new RecordingNotificationService(), WithFallback(HrReviewData.Request(eval.Id, "pass")));

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.True(review.IsHrFallback);
        Assert.Equal(RoleNames.HrAdmin, review.ReviewerRole);
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "hr_review_fallback");
        Assert.Contains(uow.Repo<Domain.Entities.Notification>().Items,
            n => n.RecipientUserId == _hmId && n.DedupKey == $"hr_review_fallback:{eval.Id}");
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task A_job_without_a_hiring_manager_still_needs_an_admin_reason()
    {
        // ADR-068: trước đây tin chưa gán HM thì quản trị viên chốt tự do, không lý do — "đường mặc định
        // cho dữ liệu cũ". Nay mọi tin đều có HM, nên thiếu HM cũng là chốt THAY: có lý do, có dấu vết.
        var (uow, _, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.HrAdmin, withHiringManager: false);

        var refused = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));
        Assert.True(refused.IsFailure);
        Assert.Equal("interview", app.Status);

        var res = await Run(uow, new RecordingNotificationService(), WithFallback(HrReviewData.Request(eval.Id, "pass")));

        Assert.True(res.IsSuccess);
        Assert.True(Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items).IsHrFallback);
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "hr_review_fallback");
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Another_jobs_hiring_manager_cannot_confirm_here()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.HrAdmin);
        var outsiderHm = Guid.NewGuid();
        uow.Seed(new User { Id = outsiderHm, Email = "other-hm@corp.io", Role = RoleNames.HiringManager, IsActive = true });

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"), hrUserId: outsiderHm);

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<Domain.Entities.HrReview>().Items);
    }

    [Fact]
    public async Task Suggested_salary_is_carried_onto_the_review_for_the_offer_stage()
    {
        var (uow, _, _, eval) = Seed(aiVerdict: "pass");
        var request = HrReviewData.Request(eval.Id, "pass");
        request.SuggestedLevel = "Middle";
        request.SuggestedSalaryMin = 25_000_000m;
        request.SuggestedSalaryMax = 32_000_000m;
        request.Strengths = "Nền tảng .NET vững";

        var res = await Run(uow, new RecordingNotificationService(), request);

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.Equal("Middle", review.SuggestedLevel);
        Assert.Equal(25_000_000m, review.SuggestedSalaryMin);
        Assert.Equal("Nền tảng .NET vững", review.Strengths);
    }
}
