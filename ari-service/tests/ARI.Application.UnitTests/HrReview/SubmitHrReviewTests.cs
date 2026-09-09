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
    public async Task Recruiter_cannot_confirm_at_all()
    {
        // ADR-061: chủ tin VẬN HÀNH phễu (xếp lịch, cấp mã, gửi thư), KHÔNG quyết định tuyển.
        //
        // Trước đây service cho phép Recruiter "confirm" khi trùng verdict của AI, nhưng đường đó
        // CHƯA BAO GIỜ đi tới được qua HTTP: endpoint gác bằng policy HrManagement vốn đã loại
        // Recruiter. Bài test cũ vì thế chốt một hành vi chỉ tồn tại ở tầng service. Nay tầng
        // service từ chối luôn — trùng khớp với cổng thật, và là lớp phòng thủ thứ hai.
        var (uow, _, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.Recruiter);

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.Equal("interview", app.Status); // không đụng tới trạng thái hồ sơ
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

    // ---------- Người chốt là Hiring Manager (ADR-061) ----------

    /// <summary>Gắn một Hiring Manager chính vào tin và trả về id của người đó.</summary>
    private static Guid AssignHm(InMemoryUnitOfWork uow, Guid jobId)
    {
        var hmId = Guid.NewGuid();
        uow.Seed(new User { Id = hmId, Email = "hm@corp.io", Role = RoleNames.HiringManager, IsActive = true });
        uow.Seed(new JobHiringTeamMember
        {
            JobPostingId = jobId,
            UserId = hmId,
            RoleOnJob = JobTeamRoles.HiringManager,
            IsPrimary = true,
            AddedByUserId = Guid.NewGuid(),
        });
        return hmId;
    }

    [Fact]
    public async Task Hiring_manager_of_the_job_confirms_the_verdict()
    {
        var (uow, job, app, eval) = Seed(aiVerdict: "pass");
        var hmId = AssignHm(uow, job.Id);

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"), hrUserId: hmId);

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.Equal(hmId, review.ReviewedByUserId);
        Assert.Equal(RoleNames.HiringManager, review.ReviewerRole); // ảnh chụp vai trò lúc chốt
        Assert.False(review.IsHrFallback);
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Hiring_manager_may_override_the_ai_verdict()
    {
        // Trước ADR-061 chỉ quản trị viên được ghi đè, nên người hiểu công việc nhất lại không
        // sửa được kết luận sai của mô hình.
        var (uow, job, app, eval) = Seed(aiVerdict: "not_pass");
        var hmId = AssignHm(uow, job.Id);

        var res = await Run(uow, new RecordingNotificationService(),
            HrReviewData.Request(eval.Id, "pass", overrideReason: "Ứng viên làm đúng bài thực tế của nhóm"),
            hrUserId: hmId);

        Assert.True(res.IsSuccess);
        Assert.True(Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items).IsOverride);
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Admin_confirming_on_a_job_that_has_an_hm_requires_a_reason()
    {
        var (uow, job, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.HrAdmin);
        AssignHm(uow, job.Id);

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
        var (uow, job, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.HrAdmin);
        var hmId = AssignHm(uow, job.Id);
        var request = HrReviewData.Request(eval.Id, "pass");
        request.FallbackReason = "Hiring Manager nghỉ phép dài ngày, ứng viên cần trả lời gấp";

        var res = await Run(uow, new RecordingNotificationService(), request);

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.True(review.IsHrFallback);
        Assert.Equal(RoleNames.HrAdmin, review.ReviewerRole);
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "hr_review_fallback");
        Assert.Contains(uow.Repo<Domain.Entities.Notification>().Items, n => n.RecipientUserId == hmId);
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Another_jobs_hiring_manager_cannot_confirm_here()
    {
        var (uow, job, _, eval) = Seed(aiVerdict: "pass");
        AssignHm(uow, job.Id);
        var outsiderHm = Guid.NewGuid();
        uow.Seed(new User { Id = outsiderHm, Email = "other-hm@corp.io", Role = RoleNames.HiringManager, IsActive = true });

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"), hrUserId: outsiderHm);

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<Domain.Entities.HrReview>().Items);
    }

    [Fact]
    public async Task Admin_still_confirms_freely_when_the_job_has_no_hiring_manager()
    {
        // Đây là đường MẶC ĐỊNH cho toàn bộ dữ liệu cũ, không phải ngoại lệ: tin chưa gán Hiring
        // Manager thì quy trình chạy y hệt trước ADR-061.
        var (uow, _, app, eval) = Seed(aiVerdict: "pass", reviewerRole: AppRoles.HrAdmin);

        var res = await Run(uow, new RecordingNotificationService(), HrReviewData.Request(eval.Id, "pass"));

        Assert.True(res.IsSuccess);
        Assert.False(Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items).IsHrFallback);
        Assert.Equal("pass", app.Status);
    }

    [Fact]
    public async Task Suggested_salary_is_carried_onto_the_review_for_the_offer_stage()
    {
        var (uow, job, _, eval) = Seed(aiVerdict: "pass");
        var hmId = AssignHm(uow, job.Id);
        var request = HrReviewData.Request(eval.Id, "pass");
        request.SuggestedLevel = "Middle";
        request.SuggestedSalaryMin = 25_000_000m;
        request.SuggestedSalaryMax = 32_000_000m;
        request.Strengths = "Nền tảng .NET vững";

        var res = await Run(uow, new RecordingNotificationService(), request, hrUserId: hmId);

        Assert.True(res.IsSuccess);
        var review = Assert.Single(uow.Repo<Domain.Entities.HrReview>().Items);
        Assert.Equal("Middle", review.SuggestedLevel);
        Assert.Equal(25_000_000m, review.SuggestedSalaryMin);
        Assert.Equal("Nền tảng .NET vững", review.Strengths);
    }
}
