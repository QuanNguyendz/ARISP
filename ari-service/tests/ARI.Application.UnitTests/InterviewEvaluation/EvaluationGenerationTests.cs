using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Evaluations;
using ARI.Application.UnitTests.PracticeInterview;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.InterviewEvaluation;

/// <summary>
/// Sinh báo cáo đánh giá AI sau khi đóng phiên — trọng tâm buổi THẬT (test-plan B1).
///
/// ADR-073: lệnh đóng phiên (<c>EndSessionAsync</c>) chỉ ghi "chờ chấm" rồi đưa vào hàng; việc chấm do
/// <see cref="InterviewEvaluator"/> làm. Khoá các hành vi: AI KHÔNG ghi trạng thái hồ sơ (ADR-053), báo
/// <c>hr_admin</c> chỉ ở buổi thật (ADR-051), điểm gian lận theo trọng số + gộp theo loại (ADR-054), đánh giá ngôn
/// ngữ chỉ chấm khi có câu trả lời (ADR-051) — và mọi kết cục đều được GHI lại để không có báo cáo nào mất im lặng.
/// </summary>
public class EvaluationGenerationTests
{
    private static ARI.Application.Services.InterviewService Svc(
        InMemoryUnitOfWork uow, RecordingNotificationService notif, StubAiProvider ai)
        => InterviewServiceFactory.Create(uow, notif, ai, new RecordingTtsService());

    private static CheatDetectionSignal Signal(Guid sessionId, string type)
        => new() { SessionId = sessionId, SignalType = type };

    // ---------- ADR-073: đóng phiên chỉ ghi "chờ chấm", không gọi AI ----------

    [Fact]
    public async Task Ending_a_session_queues_it_instead_of_calling_the_ai()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var ai = new StubAiProvider();
        var queue = new RecordingEvaluationQueue();
        var svc = InterviewServiceFactory.Create(uow, new RecordingNotificationService(), ai, new RecordingTtsService(), queue);

        var res = await svc.EndSessionAsync(session.Id, "completed", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(EvaluationStatuses.Pending, session.EvaluationStatus);
        Assert.Equal(new[] { session.Id }, queue.Enqueued);
        // Lời chào kết thúc không còn phải chờ AI chấm, và lỗi AI không thể làm hỏng lệnh đóng phiên.
        Assert.Equal(0, ai.EvaluationCallCount);
        Assert.Empty(uow.Repo<Evaluation>().Items);
    }

    // ---------- ADR-053: AI không tự đổi trạng thái hồ sơ ----------

    [Fact]
    public async Task Real_not_pass_evaluation_does_not_change_application_status()
    {
        var job = PracticeData.Job();                                  // DetectedLanguage "vi"
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, round: 1, type: "real");
        var ai = new StubAiProvider();
        // Verdict và điểm tổng do BACKEND tính từ điểm tiêu chí (ADR-062).
        ai.Evaluation.CriterionScoresJson = "{\"technical\":40,\"communication\":40}";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);

        var outcome = await EvaluationKit.CompleteAndEvaluateAsync(uow, session, ai);

        Assert.Equal(EvaluationOutcome.Done, outcome);
        Assert.Equal(EvaluationStatuses.Done, session.EvaluationStatus);
        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal("real", eval.SessionType);
        Assert.Equal(1, eval.RoundNumber);
        Assert.Equal("not_pass", eval.AiVerdict);
        Assert.Equal(40m, eval.OverallScore!.Value);
        Assert.Equal(1, ai.EvaluationCallCount);
        // ADR-053: dù AI chấm "not_pass", hồ sơ vẫn "interview" cho tới khi HM chốt.
        Assert.Equal("interview", app.Status);
    }

    // ---------- ADR-051: chỉ buổi thật mới báo hr_admin ----------

    [Fact]
    public async Task Real_completed_notifies_hr_admin_with_evaluation_complete_event()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var notif = new RecordingNotificationService();

        await EvaluationKit.CompleteAndEvaluateAsync(uow, session, notif: notif);

        Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Contains(("hr_admin", "ReceiveSystemEvent"), notif.GroupEvents);
    }

    [Fact]
    public async Task Practice_completed_generates_evaluation_but_never_notifies_hr_admin()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "practice");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var notif = new RecordingNotificationService();

        await EvaluationKit.CompleteAndEvaluateAsync(uow, session, notif: notif);

        Assert.Single(uow.Repo<Evaluation>().Items);                       // buổi thử vẫn sinh đánh giá riêng
        Assert.DoesNotContain(("hr_admin", "ReceiveSystemEvent"), notif.GroupEvents); // nhưng KHÔNG báo HR
        Assert.Equal("interview", app.Status);                            // và không đụng trạng thái hồ sơ
    }

    // ---------- ADR-054: điểm gian lận theo trọng số + gộp theo loại ----------

    [Fact]
    public async Task Cheat_score_is_weighted_by_type_and_signals_are_grouped()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session)
            .Seed(Signal(session.Id, "fullscreen_exit"),      // 8
                  Signal(session.Id, "tab_hidden"),           // 12
                  Signal(session.Id, "tab_hidden"),           // 12
                  Signal(session.Id, "unknown_type"));        // 5 (loại lạ → trọng số mặc định)

        await EvaluationKit.CompleteAndEvaluateAsync(uow, session);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(37m, eval.CheatScore!.Value);                        // 8 + 12*2 + 5
        Assert.NotNull(eval.CheatSignals);
        Assert.NotEqual("[]", eval.CheatSignals);                         // trước đây ghi cứng "[]"
        Assert.Contains("\"type\":\"tab_hidden\"", eval.CheatSignals);
        Assert.Contains("\"severity\":\"high\"", eval.CheatSignals);      // tab_hidden = high
        Assert.Contains("\"description\":\"2 ", eval.CheatSignals);       // gộp 2 lần chuyển tab
        Assert.Contains("\"severity\":\"low\"", eval.CheatSignals);       // unknown_type → low
    }

    [Fact]
    public async Task Cheat_score_caps_at_100()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session)
            .Seed(Signal(session.Id, "page_unload"), Signal(session.Id, "page_unload"),   // 15 * 2
                  Signal(session.Id, "page_unload"), Signal(session.Id, "page_unload"),   // 15 * 2
                  Signal(session.Id, "page_unload"), Signal(session.Id, "page_unload"),   // 15 * 2 → 90
                  Signal(session.Id, "tab_hidden"), Signal(session.Id, "tab_hidden"));    // +24 = 114 → cap 100

        await EvaluationKit.CompleteAndEvaluateAsync(uow, session);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(100m, eval.CheatScore!.Value);
    }

    // ---------- Không có câu trả lời ----------

    /// <summary>
    /// Buổi THẬT không có câu trả lời nào vẫn phải có một bản ghi để Hiring Manager chốt — nếu không, hồ sơ kẹt ở
    /// "đang phỏng vấn" mãi. Bản ghi hệ thống: không gọi AI, không điểm, không đánh giá ngôn ngữ (ADR-051/073).
    /// </summary>
    [Fact]
    public async Task Real_session_without_answers_gets_a_system_report_for_the_hiring_manager()
    {
        var job = PracticeData.Job();                                     // DetectedLanguage "vi"
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var q = PracticeData.Question(session.Id, 1);                     // có câu hỏi nhưng KHÔNG có câu trả lời
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session).Seed(q);
        var ai = new StubAiProvider();
        var notif = new RecordingNotificationService();

        var outcome = await EvaluationKit.CompleteAndEvaluateAsync(uow, session, ai, notif, withAnswer: false);

        Assert.Equal(EvaluationOutcome.Done, outcome);
        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal("not_pass", eval.AiVerdict);
        Assert.Null(eval.OverallScore);                                  // thiếu dữ liệu không phải là 0 điểm
        Assert.Null(eval.LanguageAssessment);                            // không có dữ liệu → không chấm bừa
        Assert.Equal(0, ai.EvaluationCallCount);
        Assert.Contains(("hr_admin", "ReceiveSystemEvent"), notif.GroupEvents);
        Assert.Equal("interview", app.Status);                           // HM vẫn là người quyết định
    }

    [Fact]
    public async Task Practice_session_without_answers_is_marked_no_answers()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "practice");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var ai = new StubAiProvider();

        var outcome = await EvaluationKit.CompleteAndEvaluateAsync(uow, session, ai, withAnswer: false);

        Assert.Equal(EvaluationOutcome.NoAnswers, outcome);
        Assert.Equal(EvaluationStatuses.NoAnswers, session.EvaluationStatus);
        Assert.Empty(uow.Repo<Evaluation>().Items);
        Assert.Equal(0, ai.EvaluationCallCount);
    }

    [Fact]
    public async Task Language_assessment_is_populated_when_an_answer_exists()
    {
        var job = PracticeData.Job();                                     // DetectedLanguage "vi"
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var q = PracticeData.Question(session.Id, 1);
        var a = PracticeData.Answer(session.Id, q.Id, "Tôi có 3 năm kinh nghiệm C#.");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session).Seed(q).Seed(a);

        await EvaluationKit.CompleteAndEvaluateAsync(uow, session);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.NotNull(eval.LanguageAssessment);
        Assert.Contains("\"cefr_level\":\"B2\"", eval.LanguageAssessment);
        Assert.Contains("\"language\":\"vi\"", eval.LanguageAssessment);
    }

    /// <summary>Đánh giá ngôn ngữ là phần PHỤ: lỗi ở đó không được làm mất báo cáo đã chấm xong điểm tiêu chí.</summary>
    [Fact]
    public async Task Language_assessment_failure_does_not_lose_the_report()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var ai = new StubAiProvider { LanguageError = new InvalidOperationException("rag down") };

        var outcome = await EvaluationKit.CompleteAndEvaluateAsync(uow, session, ai);

        Assert.Equal(EvaluationOutcome.Done, outcome);
        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(80m, eval.OverallScore!.Value);
        Assert.Null(eval.LanguageAssessment);
    }

    // ---------- ADR-073: lỗi AI được ghi lại để thử lại, không mất im lặng ----------

    [Fact]
    public async Task Ai_failure_is_recorded_for_retry()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var ai = new StubAiProvider { EvaluationError = new TimeoutException("rag-service timeout") };

        var outcome = await EvaluationKit.CompleteAndEvaluateAsync(uow, session, ai);

        Assert.Equal(EvaluationOutcome.Failed, outcome);
        Assert.Equal(EvaluationStatuses.Failed, session.EvaluationStatus);
        Assert.Equal(1, session.EvaluationAttempts);
        Assert.Contains("timeout", session.EvaluationError);
        Assert.Empty(uow.Repo<Evaluation>().Items);

        // Lượt sau AI hồi lại → báo cáo được ghi, lượt thử đếm tiếp.
        ai.EvaluationError = null;
        var retry = await EvaluationKit.Evaluator(uow, ai).EvaluateSessionAsync(session.Id, CancellationToken.None);

        Assert.Equal(EvaluationOutcome.Done, retry);
        Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(2, session.EvaluationAttempts);
        Assert.Null(session.EvaluationError);
    }

    [Fact]
    public async Task Evaluating_twice_never_calls_the_ai_again()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var ai = new StubAiProvider();

        await EvaluationKit.CompleteAndEvaluateAsync(uow, session, ai);
        var second = await EvaluationKit.Evaluator(uow, ai).EvaluateSessionAsync(session.Id, CancellationToken.None);

        Assert.Equal(EvaluationOutcome.Done, second);
        Assert.Equal(1, ai.EvaluationCallCount);
        Assert.Single(uow.Repo<Evaluation>().Items);
    }

    [Fact]
    public async Task Session_that_is_not_completed_is_skipped()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");        // status "active"
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var ai = new StubAiProvider();

        var outcome = await EvaluationKit.Evaluator(uow, ai).EvaluateSessionAsync(session.Id, CancellationToken.None);

        Assert.Equal(EvaluationOutcome.Skipped, outcome);
        Assert.Equal(0, ai.EvaluationCallCount);
    }

    // ---------- ADR-073: lượt quét tìm lại việc dang dở ----------

    [Fact]
    public async Task Sweep_picks_pending_stuck_retryable_and_newly_unblocked_sessions()
    {
        var job = PracticeData.Job();
        var otherJob = PracticeData.Job(title: "Tin chưa có bộ tiêu chí");
        var app = PracticeData.App(job.Id, status: "interview");
        var otherApp = PracticeData.App(otherJob.Id, status: "interview");
        var now = DateTimeOffset.UtcNow;

        InterviewSession S(Guid appId, string? status, int attempts = 0, DateTimeOffset? updated = null)
        {
            var s = PracticeData.Session(appId, type: "real", status: "completed");
            s.EvaluationStatus = status;
            s.EvaluationAttempts = attempts;
            s.EvaluationUpdatedAt = updated ?? now;
            return s;
        }

        var pending = S(app.Id, EvaluationStatuses.Pending);
        var stuck = S(app.Id, EvaluationStatuses.Processing, 1, now.AddMinutes(-30));
        var busy = S(app.Id, EvaluationStatuses.Processing, 1, now.AddMinutes(-1));
        var retryable = S(app.Id, EvaluationStatuses.Failed, 1, now.AddMinutes(-5));
        var tooSoon = S(app.Id, EvaluationStatuses.Failed, 2, now.AddMinutes(-1));
        var exhausted = S(app.Id, EvaluationStatuses.Failed, EvaluationStatuses.MaxAttempts, now.AddHours(-1));
        var unblocked = S(app.Id, EvaluationStatuses.BlockedNoRubric);              // tin nay đã có bộ tiêu chí
        var stillBlocked = S(otherApp.Id, EvaluationStatuses.BlockedNoRubric);      // tin kia vẫn chưa
        var done = S(app.Id, EvaluationStatuses.Done);

        var uow = new InMemoryUnitOfWork().Seed(job, otherJob).Seed(app, otherApp).Seed(PracticeData.Rubric(job.Id))
            .Seed(pending, stuck, busy, retryable, tooSoon, exhausted, unblocked, stillBlocked, done);

        var due = await EvaluationKit.Evaluator(uow).FindDueSessionIdsAsync(100, CancellationToken.None);

        Assert.Equal(
            new[] { pending.Id, stuck.Id, retryable.Id, unblocked.Id }.OrderBy(x => x),
            due.OrderBy(x => x));
    }

    // ---------- Guards của EndSessionAsync ----------

    [Fact]
    public async Task Already_completed_session_is_idempotent_and_skips_generation()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real", status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif, new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<Evaluation>().Items);                      // không sinh đánh giá lần hai
        Assert.Empty(notif.GroupEvents);
        Assert.Null(session.EvaluationStatus);                           // không đưa lại vào hàng
    }

    [Fact]
    public async Task Non_completed_status_skips_evaluation()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(PracticeData.Rubric(job.Id)).Seed(app).Seed(session);

        var res = await Svc(uow, new(), new()).EndSessionAsync(session.Id, "abandoned", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("abandoned", session.Status);
        Assert.Null(session.EvaluationStatus);                           // chỉ "completed" mới chờ chấm
        Assert.Empty(uow.Repo<Evaluation>().Items);
    }

    [Fact]
    public async Task Missing_session_returns_failure()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new(), new())
            .EndSessionAsync(Guid.NewGuid(), "completed", CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy phiên phỏng vấn", res.Error);
    }
}
