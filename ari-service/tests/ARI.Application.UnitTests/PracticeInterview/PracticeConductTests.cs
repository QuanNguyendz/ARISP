using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.DTOs;
using ARI.Application.Options;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.PracticeInterview;

/// <summary>
/// Tiến hành buổi phỏng vấn thử (UC-42/43, <see cref="InterviewService"/>, ADR-050): mở phiên (gating lượt/vòng),
/// lưu câu trả lời, sinh câu hỏi kế + TTS, kết thúc (AI đóng phiên / trần thời lượng) và sinh đánh giá.
/// Buổi thử KHÔNG đụng pipeline thật: không đổi trạng thái hồ sơ, không báo HR (ADR-051).
/// </summary>
public class PracticeConductTests
{
    private static InterviewService Svc(
        InMemoryUnitOfWork uow, RecordingNotificationService notif, StubAiProvider ai, RecordingTtsService tts, InterviewOptions? opts = null)
        => InterviewServiceFactory.Create(uow, notif, ai, tts, opts);

    private static StartSessionRequest Start(Guid appId, int round = 1, string? uiLang = null)
        => new() { ApplicationId = appId, RoundNumber = round, SessionType = "practice", UiLanguage = uiLang };

    // ---------- StartSessionAsync (practice) ----------

    [Fact]
    public async Task Start_application_not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new(), new(), new())
            .StartSessionAsync(Start(Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Application not found", res.Error);
    }

    [Fact]
    public async Task Start_job_not_found_fails()
    {
        var app = PracticeData.App(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(app); // job không seed

        var res = await Svc(uow, new(), new(), new()).StartSessionAsync(Start(app.Id), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Job posting not found", res.Error);
    }

    [Fact]
    public async Task Start_creates_active_practice_session()
    {
        var job = PracticeData.Job(language: "en");
        var app = PracticeData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await Svc(uow, new(), new(), new()).StartSessionAsync(Start(app.Id, uiLang: "vi"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var session = Assert.Single(uow.Repo<InterviewSession>().Items);
        Assert.Equal("practice", session.SessionType);
        Assert.Equal("active", session.Status);
        Assert.Equal("en", session.InterviewLanguage);   // theo job
        Assert.Equal("vi", session.ReportLanguage);      // theo UI ứng viên (ADR-051)
        Assert.True(app.PracticeSessionUsed);
    }

    [Fact]
    public async Task Start_blocked_when_round_attempt_already_used()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(PracticeData.Session(app.Id, round: 1)); // đã có 1 phiên thử vòng 1

        var res = await Svc(uow, new(), new(), new()).StartSessionAsync(Start(app.Id, round: 1), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã dùng lượt", res.Error); // mặc định 1 lượt/vòng
    }

    [Fact]
    public async Task Start_unlimited_when_attempts_option_zero()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(PracticeData.Session(app.Id, round: 1));

        var res = await Svc(uow, new(), new(), new(), new InterviewOptions { PracticeAttemptsPerRound = 0 })
            .StartSessionAsync(Start(app.Id, round: 1), CancellationToken.None);

        Assert.True(res.IsSuccess); // 0 = không giới hạn (dev/test)
    }

    [Fact]
    public async Task Start_other_round_is_not_blocked()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(PracticeData.Session(app.Id, round: 1));

        var res = await Svc(uow, new(), new(), new()).StartSessionAsync(Start(app.Id, round: 2), CancellationToken.None);

        Assert.True(res.IsSuccess); // vòng 2 vẫn còn lượt
    }

    [Fact]
    public async Task Start_practice_does_not_seed_must_ask()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(PracticeData.MustAsk(job.Id));

        await Svc(uow, new(), new(), new()).StartSessionAsync(Start(app.Id), CancellationToken.None);

        Assert.Empty(uow.Repo<MustAskTracking>().Items); // must-ask chỉ dành cho buổi thật
    }

    // ---------- SaveAnswerAsync ----------

    [Fact]
    public async Task Save_answer_persists_transcript()
    {
        var session = PracticeData.Session(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(session);
        var qId = Guid.NewGuid();

        var res = await Svc(uow, new(), new(), new()).SaveAnswerAsync(session.Id, qId, "Tôi trả lời...", 4200, CancellationToken.None);

        Assert.True(res.IsSuccess);
        var answer = Assert.Single(uow.Repo<Answer>().Items);
        Assert.Equal("Tôi trả lời...", answer.Transcript);
        Assert.Equal(4200, answer.ResponseTimeMs);
    }

    [Fact]
    public async Task Save_answer_session_not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new(), new(), new())
            .SaveAnswerAsync(Guid.NewGuid(), Guid.NewGuid(), "x", null, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Session not found", res.Error);
    }

    [Fact]
    public async Task Save_answer_inactive_session_fails()
    {
        var session = PracticeData.Session(Guid.NewGuid(), status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(session);

        var res = await Svc(uow, new(), new(), new()).SaveAnswerAsync(session.Id, Guid.NewGuid(), "x", null, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("not active", res.Error);
    }

    // ---------- GenerateAndSendNextQuestionAsync ----------

    [Fact]
    public async Task Generate_question_saves_and_publishes_text_plus_audio()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        var notif = new RecordingNotificationService();
        var ai = new StubAiProvider { QuestionText = "Giới thiệu bản thân?" };
        var tts = new RecordingTtsService();

        var res = await Svc(uow, notif, ai, tts).GenerateAndSendNextQuestionAsync(session.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Giới thiệu bản thân?", res.Value);
        var q = Assert.Single(uow.Repo<Question>().Items);
        Assert.Equal(1, q.SequenceNumber);
        Assert.Contains(notif.SessionEvents, e => e.SessionId == session.Id && e.EventType == "ReceiveQuestion");
        Assert.Contains(notif.SessionEvents, e => e.SessionId == session.Id && e.EventType == "ReceiveQuestionAudio");
        Assert.Equal(1, tts.CallCount);
        Assert.Equal("active", session.Status); // chưa kết thúc
    }

    [Fact]
    public async Task Generate_fails_when_session_not_active()
    {
        var session = PracticeData.Session(Guid.NewGuid(), status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(session);

        var res = await Svc(uow, new(), new(), new()).GenerateAndSendNextQuestionAsync(session.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("not active", res.Error);
    }

    [Fact]
    public async Task Generate_force_closes_after_question_cap()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        for (var i = 1; i <= 12; i++) uow.Seed(PracticeData.Question(session.Id, i)); // đã đủ 12 câu
        var ai = new StubAiProvider { QuestionText = "Cảm ơn bạn đã tham gia." };

        var res = await Svc(uow, new(), ai, new()).GenerateAndSendNextQuestionAsync(session.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("completed", session.Status);                 // vượt cap → đóng phiên
        Assert.Equal("Cảm ơn bạn đã tham gia.", session.ClosingText);
        Assert.Equal(12, uow.Repo<Question>().Items.Count);        // không thêm câu hỏi mới
        Assert.Equal(1, ai.EvaluationCallCount);                   // sinh đánh giá khi đóng
    }

    [Fact]
    public async Task Generate_closes_on_end_interview_marker()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        var ai = new StubAiProvider { QuestionText = "Buổi phỏng vấn kết thúc tại đây. [END_INTERVIEW]" };

        var res = await Svc(uow, new(), ai, new()).GenerateAndSendNextQuestionAsync(session.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("completed", session.Status);
        Assert.Equal("Buổi phỏng vấn kết thúc tại đây.", session.ClosingText); // marker bị lược bỏ
        Assert.Empty(uow.Repo<Question>().Items);                              // không lưu câu "kết thúc" thành câu hỏi
    }

    // ---------- EndSessionAsync + GenerateEvaluationReport (practice) ----------

    [Fact]
    public async Task End_completed_generates_practice_evaluation_without_touching_application()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id);
        var q = PracticeData.Question(session.Id, 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(q).Seed(PracticeData.Answer(session.Id, q.Id));
        var notif = new RecordingNotificationService();
        var ai = new StubAiProvider();

        var res = await Svc(uow, notif, ai, new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("completed", session.Status);
        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal("practice", eval.SessionType);
        Assert.Equal("interview", app.Status);                                    // KHÔNG đổi trạng thái hồ sơ (ADR-051/053)
        Assert.DoesNotContain(notif.GroupEvents, e => e.Group == "hr_admin");      // KHÔNG báo HR
    }

    [Fact]
    public async Task End_is_idempotent_when_already_completed()
    {
        var session = PracticeData.Session(Guid.NewGuid(), status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(session);
        var ai = new StubAiProvider();

        var res = await Svc(uow, new(), ai, new()).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, ai.EvaluationCallCount);        // không chấm lần hai
        Assert.Empty(uow.Repo<Evaluation>().Items);
    }

    [Fact]
    public async Task End_non_completed_status_skips_evaluation()
    {
        var session = PracticeData.Session(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(session);
        var ai = new StubAiProvider();

        var res = await Svc(uow, new(), ai, new()).EndSessionAsync(session.Id, "abandoned", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("abandoned", session.Status);
        Assert.Equal(0, ai.EvaluationCallCount);
        Assert.Empty(uow.Repo<Evaluation>().Items);
    }

    // ---------- PracticeTimeoutCloseAsync ----------

    [Fact]
    public async Task Timeout_rejected_before_threshold()
    {
        var session = PracticeData.Session(Guid.NewGuid(), startedAt: DateTimeOffset.UtcNow); // vừa bắt đầu
        var uow = new InMemoryUnitOfWork().Seed(session);

        var res = await Svc(uow, new(), new(), new()).PracticeTimeoutCloseAsync(session.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Chưa hết thời gian", res.Error);
        Assert.Equal("active", session.Status);
    }

    [Fact]
    public async Task Timeout_closes_after_threshold()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id, startedAt: DateTimeOffset.UtcNow.AddMinutes(-21)); // quá trần 20'
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        var ai = new StubAiProvider();

        var res = await Svc(uow, new(), ai, new()).PracticeTimeoutCloseAsync(session.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("completed", session.Status);
        Assert.False(string.IsNullOrEmpty(session.ClosingText)); // AI nói câu kết
        Assert.Equal(1, ai.EvaluationCallCount);
    }

    [Fact]
    public async Task Timeout_idempotent_when_completed()
    {
        var session = PracticeData.Session(Guid.NewGuid(), status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(session);
        var ai = new StubAiProvider();

        var res = await Svc(uow, new(), ai, new()).PracticeTimeoutCloseAsync(session.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, ai.EvaluationCallCount);
    }
}
