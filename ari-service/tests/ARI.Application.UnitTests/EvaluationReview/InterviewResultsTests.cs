using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Evaluations;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.EvaluationReview;

/// <summary>
/// ADR-069 — đường vào báo cáo AI sau buổi phỏng vấn thật. Mỗi vòng của hồ sơ nói rõ đang ở bước nào
/// (có ca · đang phỏng vấn · AI đang chấm · chờ HM chốt · đã chốt) và có video / transcript để xem hay không.
/// </summary>
public class InterviewResultsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app) Setup()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "screening" })
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 2, RoundType = "online_test" })
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 3, RoundType = "technical" });
        return (uow, job, app);
    }

    private static void Book(InMemoryUnitOfWork uow, Guid jobId, Guid appId, int round, DateTimeOffset start,
        string status = BookingStatus.Scheduled)
    {
        var slot = new AvailabilitySlot { JobPostingId = jobId, RoundNumber = round, StartTime = start, EndTime = start.AddMinutes(30) };
        uow.Seed(slot).Seed(new InterviewBooking
        {
            ApplicationId = appId, AvailabilitySlotId = slot.Id, RoundNumber = round, Status = status,
        });
    }

    private static Task<Result<System.Collections.Generic.List<InterviewResultRowDto>>> ForApp(
        InMemoryUnitOfWork uow, Guid appId, Guid? userId = null, string? role = null)
        => new GetApplicationInterviewResultsQueryHandler(uow).Handle(
            new GetApplicationInterviewResultsQuery(appId, userId ?? Guid.NewGuid(), role ?? AppRoles.HrAdmin),
            CancellationToken.None);

    [Fact]
    public async Task Future_slot_without_session_is_scheduled()
    {
        var (uow, job, app) = Setup();
        Book(uow, job.Id, app.Id, 1, Now.AddHours(3));

        var row = Assert.Single((await ForApp(uow, app.Id)).Value!);

        Assert.Equal(InterviewResultStates.Scheduled, row.State);
        Assert.Equal(1, row.RoundNumber);
        Assert.Equal("screening", row.RoundType);
        Assert.NotNull(row.SlotStartTime);
        Assert.Null(row.SessionId);
    }

    /// <summary>Quá giờ ca mà chưa có phiên — nói ra, chưa kết luận "vắng mặt" (bộ quét no-show còn ân hạn).</summary>
    [Fact]
    public async Task Past_slot_without_session_is_overdue()
    {
        var (uow, job, app) = Setup();
        Book(uow, job.Id, app.Id, 1, Now.AddHours(-3));

        var row = Assert.Single((await ForApp(uow, app.Id)).Value!);

        Assert.Equal(InterviewResultStates.Overdue, row.State);
    }

    /// <summary>
    /// Buổi vừa kết thúc chưa có báo cáo (AI còn viết). Danh sách đánh giá lúc này im lặng — người dùng tưởng
    /// buổi phỏng vấn không để lại gì; ở đây nó hiện "AI đang chấm".
    /// </summary>
    [Fact]
    public async Task Completed_session_without_report_is_evaluating()
    {
        var (uow, job, app) = Setup();
        Book(uow, job.Id, app.Id, 1, Now.AddHours(-1));
        uow.Seed(EvaluationData.Session(app.Id, round: 1, status: InterviewSessionStatuses.Completed));

        var row = Assert.Single((await ForApp(uow, app.Id)).Value!);

        Assert.Equal(InterviewResultStates.Evaluating, row.State);
        Assert.NotNull(row.SessionId);
        Assert.Null(row.EvaluationId);
    }

    [Fact]
    public async Task Report_with_video_and_transcript_awaits_the_hm()
    {
        var (uow, job, app) = Setup();
        Book(uow, job.Id, app.Id, 1, Now.AddHours(-1));
        var session = EvaluationData.Session(app.Id, round: 1, recordingUrl: "rec/k.webm", recordingExpiresAt: Now.AddDays(7));
        var eval = EvaluationData.Eval(app.Id, sessionId: session.Id, round: 1, verdict: "pass", score: 82m);
        var q1 = new Question { SessionId = session.Id, SequenceNumber = 1, QuestionText = "Q1" };
        var q2 = new Question { SessionId = session.Id, SequenceNumber = 2, QuestionText = "Q2" };
        uow.Seed(session).Seed(eval).Seed(q1).Seed(q2)
            .Seed(new Answer { QuestionId = q1.Id, SessionId = session.Id, Transcript = "Trả lời 1" })
            .Seed(new Answer { QuestionId = q2.Id, SessionId = session.Id, Transcript = "  " });   // câu bỏ trống

        var row = Assert.Single((await ForApp(uow, app.Id)).Value!);

        Assert.Equal(InterviewResultStates.PendingReview, row.State);
        Assert.Equal(eval.Id, row.EvaluationId);
        Assert.Equal(82m, row.OverallScore);
        Assert.True(row.HasRecording);
        Assert.Equal(1, row.TranscriptTurns);            // chỉ đếm lượt có lời trả lời thật
    }

    [Fact]
    public async Task Reviewed_report_carries_the_final_verdict()
    {
        var (uow, job, app) = Setup();
        var session = EvaluationData.Session(app.Id, round: 1);
        var eval = EvaluationData.Eval(app.Id, sessionId: session.Id, round: 1, verdict: "pass");
        var review = EvaluationData.Review(eval.Id, finalVerdict: "not_pass", isOverride: true);
        review.ReviewerRole = "hiring_manager";
        uow.Seed(session).Seed(eval).Seed(review);

        var row = Assert.Single((await ForApp(uow, app.Id)).Value!);

        Assert.Equal(InterviewResultStates.Reviewed, row.State);
        Assert.Equal("not_pass", row.FinalVerdict);
        Assert.Equal("hiring_manager", row.ReviewerRole);
    }

    /// <summary>
    /// Buổi lỗi rồi làm lại: phiên CÓ báo cáo phải thắng phiên mới hơn nhưng hỏng, không thì kết quả thật
    /// bị che bởi một phiên rỗng.
    /// </summary>
    [Fact]
    public async Task Session_with_a_report_wins_over_a_newer_aborted_one()
    {
        var (uow, _, app) = Setup();
        var good = EvaluationData.Session(app.Id, round: 3, createdAt: Now.AddHours(-2));
        var eval = EvaluationData.Eval(app.Id, sessionId: good.Id, round: 3);
        var aborted = EvaluationData.Session(app.Id, round: 3, status: InterviewSessionStatuses.Aborted, createdAt: Now.AddHours(-1));
        uow.Seed(good).Seed(eval).Seed(aborted);

        var row = Assert.Single((await ForApp(uow, app.Id)).Value!);

        Assert.Equal(good.Id, row.SessionId);
        Assert.Equal(eval.Id, row.EvaluationId);
    }

    /// <summary>Buổi thử là của riêng ứng viên (ADR-051); vòng trắc nghiệm có bảng điểm riêng.</summary>
    [Fact]
    public async Task Practice_sessions_and_online_test_rounds_are_left_out()
    {
        var (uow, job, app) = Setup();
        Book(uow, job.Id, app.Id, 2, Now.AddHours(-5));                                // vòng trắc nghiệm
        uow.Seed(EvaluationData.Session(app.Id, round: 1, type: "practice"));

        var rows = (await ForApp(uow, app.Id)).Value!;

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Declined_booking_does_not_produce_a_row()
    {
        var (uow, job, app) = Setup();
        Book(uow, job.Id, app.Id, 1, Now.AddHours(2), status: BookingStatus.Declined);

        Assert.Empty((await ForApp(uow, app.Id)).Value!);
    }

    [Fact]
    public async Task Outsider_is_refused()
    {
        var (uow, _, app) = Setup();

        var res = await ForApp(uow, app.Id, Guid.NewGuid(), AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ---------- Buổi vừa kết thúc (màn Phòng phỏng vấn của HM) ----------

    [Fact]
    public async Task Recent_list_shows_ended_sessions_of_the_callers_jobs_only()
    {
        var (uow, job, app) = Setup();
        var hm = HiringManagerSeed.Primary(uow, job.Id);

        var mine = EvaluationData.Session(app.Id, round: 1);
        mine.EndedAt = Now.AddMinutes(-20);
        var old = EvaluationData.Session(app.Id, round: 3);
        old.EndedAt = Now.AddDays(-3);                                               // ngoài cửa sổ 24 giờ
        var live = EvaluationData.Session(app.Id, round: 3, status: InterviewSessionStatuses.Active);

        var otherJob = EvaluationData.Job("Khác");
        var otherApp = EvaluationData.App(otherJob.Id, name: "Người khác");
        var others = EvaluationData.Session(otherApp.Id, round: 1);
        others.EndedAt = Now.AddMinutes(-10);

        uow.Seed(mine).Seed(old).Seed(live).Seed(otherJob).Seed(otherApp).Seed(others);

        var res = await new GetRecentInterviewResultsQueryHandler(uow).Handle(
            new GetRecentInterviewResultsQuery(hm.Id, AppRoles.HiringManager), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var row = Assert.Single(res.Value!);
        Assert.Equal(mine.Id, row.SessionId);
        Assert.Equal(InterviewResultStates.Evaluating, row.State);
        Assert.Equal(job.Title, row.JobTitle);
    }
}
