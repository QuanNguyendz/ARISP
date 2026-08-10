using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.PracticeInterview;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.InterviewEvaluation;

/// <summary>
/// Sinh báo cáo đánh giá AI khi đóng phiên (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>EndSessionAsync</c> → <c>GenerateEvaluationReportAsync</c>) — trọng tâm buổi THẬT (test-plan B1).
/// Khoá các hành vi: AI KHÔNG ghi trạng thái hồ sơ (ADR-053), báo <c>hr_admin</c> chỉ ở buổi thật (ADR-051),
/// điểm gian lận theo trọng số + gộp theo loại (ADR-054), đánh giá ngôn ngữ chỉ chấm khi có câu trả lời (ADR-051).
/// </summary>
public class EvaluationGenerationTests
{
    private static ARI.Application.Services.InterviewService Svc(
        InMemoryUnitOfWork uow, RecordingNotificationService notif, StubAiProvider ai)
        => InterviewServiceFactory.Create(uow, notif, ai, new RecordingTtsService());

    private static CheatDetectionSignal Signal(Guid sessionId, string type)
        => new() { SessionId = sessionId, SignalType = type };

    // ---------- ADR-053: AI không tự đổi trạng thái hồ sơ ----------

    [Fact]
    public async Task Real_not_pass_evaluation_does_not_change_application_status()
    {
        var job = PracticeData.Job();                                  // DetectedLanguage "vi"
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, round: 1, type: "real");
        var ai = new StubAiProvider();
        ai.Evaluation.Verdict = "not_pass";
        ai.Evaluation.Score = 40m;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);

        var res = await Svc(uow, new(), ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        Assert.True(res.IsSuccess);
        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal("real", eval.SessionType);
        Assert.Equal(1, eval.RoundNumber);
        Assert.Equal("not_pass", eval.AiVerdict);
        Assert.Equal(40m, eval.OverallScore!.Value);
        Assert.Equal(1, ai.EvaluationCallCount);
        // ADR-053: dù AI chấm "not_pass", hồ sơ vẫn "interview" cho tới khi HR xác nhận.
        Assert.Equal("interview", app.Status);
    }

    // ---------- ADR-051: chỉ buổi thật mới báo hr_admin ----------

    [Fact]
    public async Task Real_completed_notifies_hr_admin_with_evaluation_complete_event()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var q = PracticeData.Question(session.Id, 1);
        var a = PracticeData.Answer(session.Id, q.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(q).Seed(a);
        var notif = new RecordingNotificationService();

        await Svc(uow, notif, new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Contains(("hr_admin", "ReceiveSystemEvent"), notif.GroupEvents);
    }

    [Fact]
    public async Task Practice_completed_generates_evaluation_but_never_notifies_hr_admin()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "practice");
        var q = PracticeData.Question(session.Id, 1);
        var a = PracticeData.Answer(session.Id, q.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(q).Seed(a);
        var notif = new RecordingNotificationService();

        await Svc(uow, notif, new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

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
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session)
            .Seed(Signal(session.Id, "fullscreen_exit"),      // 8
                  Signal(session.Id, "tab_hidden"),           // 12
                  Signal(session.Id, "tab_hidden"),           // 12
                  Signal(session.Id, "unknown_type"));        // 5 (loại lạ → trọng số mặc định)

        await Svc(uow, new(), new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(37m, eval.CheatScore!.Value);                        // 8 + 12*2 + 5
        Assert.NotNull(eval.CheatSignals);
        Assert.NotEqual("[]", eval.CheatSignals);                         // trước đây ghi cứng "[]"
        Assert.Contains("\"type\":\"tab_hidden\"", eval.CheatSignals);
        Assert.Contains("\"severity\":\"high\"", eval.CheatSignals);      // tab_hidden = high
        // JsonSerializer escape non-ASCII ("2 lần" → "2 lần") → assert phần ASCII: count 2 chỉ có ở tab_hidden.
        Assert.Contains("\"description\":\"2 ", eval.CheatSignals);       // gộp 2 lần chuyển tab
        Assert.Contains("\"severity\":\"low\"", eval.CheatSignals);       // unknown_type → low
    }

    [Fact]
    public async Task Cheat_score_caps_at_100()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session)
            .Seed(Signal(session.Id, "page_unload"), Signal(session.Id, "page_unload"),   // 15 * 2
                  Signal(session.Id, "page_unload"), Signal(session.Id, "page_unload"),   // 15 * 2
                  Signal(session.Id, "page_unload"), Signal(session.Id, "page_unload"),   // 15 * 2 → 90
                  Signal(session.Id, "tab_hidden"), Signal(session.Id, "tab_hidden"));    // +24 = 114 → cap 100

        await Svc(uow, new(), new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(100m, eval.CheatScore!.Value);
    }

    // ---------- ADR-051: đánh giá ngôn ngữ chỉ chấm khi có câu trả lời ----------

    [Fact]
    public async Task Language_assessment_is_null_when_there_are_no_answers()
    {
        var job = PracticeData.Job();                                     // DetectedLanguage "vi"
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var q = PracticeData.Question(session.Id, 1);                     // có câu hỏi nhưng KHÔNG có câu trả lời
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(q);

        await Svc(uow, new(), new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Null(eval.LanguageAssessment);                            // không có dữ liệu → không chấm bừa
    }

    [Fact]
    public async Task Language_assessment_is_populated_when_an_answer_exists()
    {
        var job = PracticeData.Job();                                     // DetectedLanguage "vi"
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var q = PracticeData.Question(session.Id, 1);
        var a = PracticeData.Answer(session.Id, q.Id, "Tôi có 3 năm kinh nghiệm C#.");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(q).Seed(a);

        await Svc(uow, new(), new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.NotNull(eval.LanguageAssessment);
        Assert.Contains("\"cefr_level\":\"B2\"", eval.LanguageAssessment);
        Assert.Contains("\"language\":\"vi\"", eval.LanguageAssessment);
    }

    // ---------- Idempotency & guards của EndSessionAsync ----------

    [Fact]
    public async Task Already_completed_session_is_idempotent_and_skips_generation()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real", status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif, new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<Evaluation>().Items);                      // không sinh đánh giá lần hai
        Assert.Empty(notif.GroupEvents);
    }

    [Fact]
    public async Task Non_completed_status_skips_evaluation()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);

        var res = await Svc(uow, new(), new()).EndSessionAsync(session.Id, "abandoned", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("abandoned", session.Status);
        Assert.Empty(uow.Repo<Evaluation>().Items);                      // chỉ status "completed" mới sinh đánh giá
    }

    [Fact]
    public async Task Missing_session_returns_failure()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new(), new())
            .EndSessionAsync(Guid.NewGuid(), "completed", CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Session not found", res.Error);
    }
}
