using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static ARI.Application.UnitTests.CvScoring.CvScoringKit;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// Chấm CV của hồ sơ ở nền (ADR-070): tìm hồ sơ thiếu điểm theo bộ tiêu chí hiện hành, chấm, gắn kết quả,
/// và nhắc HM khi tin chưa có bộ tiêu chí. Cùng với luật "điểm nào được hiện".
/// </summary>
public class CvApplicationScorerTests
{
    private static CvApplicationScorer Scorer(
        InMemoryUnitOfWork uow, FakeGeminiProvider gemini, RecordingFileStorage storage,
        RecordingNotificationService? notif = null, CvScoringInFlight? inFlight = null)
    {
        inFlight ??= new CvScoringInFlight();
        return new CvApplicationScorer(uow, Service(uow, gemini, storage: storage, inFlight: inFlight), storage,
            notif ?? new RecordingNotificationService(), inFlight, NullLogger<CvApplicationScorer>.Instance);
    }

    private static ARI.Domain.Entities.Application App(Guid jobId, string? cv = "cv/a.pdf", Guid? analysisId = null) => new()
    {
        JobPostingId = jobId,
        CandidateEmail = $"{Guid.NewGuid():N}@x.io",
        CvFileUrl = cv,
        CvJdAnalysisId = analysisId,
        Status = ApplicationStatuses.CvSubmitted,
        CandidateAccountId = Guid.NewGuid(),
    };

    [Fact]
    public async Task Stale_means_unscored_or_scored_with_an_old_rubric()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var current = new CvJdAnalysis { JobPostingId = job.Id, RubricDocumentId = rubric.Id };
        var legacy = new CvJdAnalysis { JobPostingId = job.Id, RubricDocumentId = null };
        var fresh = App(job.Id, analysisId: current.Id);
        var old = App(job.Id, analysisId: legacy.Id);
        var unscored = App(job.Id);
        var noCv = App(job.Id, cv: null);
        var image = App(job.Id, cv: "cv/a.png");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(current, legacy).Seed(fresh, old, unscored, noCv, image);

        var ids = await Scorer(uow, new FakeGeminiProvider(), new RecordingFileStorage())
            .FindStaleApplicationIdsAsync(null, 100, CancellationToken.None);

        Assert.Equal(new[] { old.Id, unscored.Id }.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public async Task Jobs_without_a_rubric_have_nothing_to_score()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(App(job.Id));

        var ids = await Scorer(uow, new FakeGeminiProvider(), new RecordingFileStorage())
            .FindStaleApplicationIdsAsync(null, 100, CancellationToken.None);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Scoring_links_the_result_and_notifies()
    {
        var job = Job();
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id)).Seed(app);
        var storage = new RecordingFileStorage { FileBytes = CvBytes() };
        var notif = new RecordingNotificationService();
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80), ("education", 60)) };

        await Scorer(uow, gemini, storage, notif).ScoreApplicationAsync(app.Id, CancellationToken.None);

        var analysis = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal(analysis.Id, app.CvJdAnalysisId);
        Assert.Equal(74, analysis.MatchScore); // 80×70 + 60×30 = 7400 ÷ 100
        Assert.Contains(notif.UserEvents, e => e.UserId == app.CandidateAccountId && e.EventType == "ReceiveUserNotification");
        Assert.Contains(notif.GroupEvents, e => e.Group == "hr_admin" && e.EventType == "ReceiveApplicationStatusUpdate");
    }

    [Fact]
    public async Task Already_current_application_is_skipped()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var current = new CvJdAnalysis { JobPostingId = job.Id, RubricDocumentId = rubric.Id };
        var app = App(job.Id, analysisId: current.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(current).Seed(app);
        var gemini = new FakeGeminiProvider();

        await Scorer(uow, gemini, new RecordingFileStorage { FileBytes = CvBytes() }).ScoreApplicationAsync(app.Id, CancellationToken.None);

        Assert.Equal(0, gemini.AnalyzeCallCount);
    }

    /// <summary>File CV mất → ghi nhận lỗi để lượt quét giãn nhịp, không gọi AI.</summary>
    [Fact]
    public async Task Missing_file_backs_off_without_calling_ai()
    {
        var job = Job();
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id)).Seed(app);
        var gemini = new FakeGeminiProvider();
        var inFlight = new CvScoringInFlight();
        var scorer = Scorer(uow, gemini, new RecordingFileStorage { FileBytes = null }, inFlight: inFlight);

        await scorer.ScoreApplicationAsync(app.Id, CancellationToken.None);
        var stale = await scorer.FindStaleApplicationIdsAsync(job.Id, 100, CancellationToken.None);

        Assert.Equal(0, gemini.AnalyzeCallCount);
        Assert.Null(app.CvJdAnalysisId);
        Assert.Empty(stale); // đang trong thời gian chờ thử lại
    }

    // ---------------- Chấm lỗi → "sẽ thử lại" ----------------

    /// <summary>
    /// Lỗi AI không đổi dòng nào trong DB nên trigger realtime không bắn: nếu không ghi nhận + đẩy tay, màn nhân
    /// sự đứng mãi ở "Đang chấm CV" dù lượt chấm đã hỏng.
    /// </summary>
    [Fact]
    public async Task Ai_failure_is_recorded_with_a_reason_and_announced_to_everyone_reading_the_application()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(app);
        var hm = HiringManagerSeed.Primary(uow, job.Id);
        var notif = new RecordingNotificationService();
        var inFlight = new CvScoringInFlight();
        var gemini = new FakeGeminiProvider { AnalyzeResult = ARI.Application.Common.Result.Failure<CvJdAnalysisResultDto>("Gemini 503 overloaded") };

        await Scorer(uow, gemini, new RecordingFileStorage { FileBytes = CvBytes() }, notif, inFlight)
            .ScoreApplicationAsync(app.Id, CancellationToken.None);

        var failure = inFlight.LastFailure(CvScoringInFlight.ApplicationKey(app.Id, rubric.Id));
        Assert.NotNull(failure);
        Assert.Equal(CvScoringErrors.AiUnavailable, failure!.Code);
        Assert.Equal(1, failure.Attempts);
        Assert.True(failure.RetryAfter > DateTimeOffset.UtcNow);
        Assert.Null(app.CvJdAnalysisId);

        Assert.Contains(notif.UserEvents, e => e.UserId == hm.Id && e.EventType == "ReceiveApplicationStatusUpdate");
        Assert.Contains(notif.UserEvents, e => e.UserId == job.CreatedByUserId && e.EventType == "ReceiveApplicationStatusUpdate");
        Assert.Contains(notif.GroupEvents, e => e.Group == "hr_admin" && e.EventType == "ReceiveApplicationStatusUpdate");
    }

    [Fact]
    public async Task Missing_cv_file_is_reported_as_unreadable()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(app);
        var notif = new RecordingNotificationService();
        var inFlight = new CvScoringInFlight();

        await Scorer(uow, new FakeGeminiProvider(), new RecordingFileStorage { FileBytes = null }, notif, inFlight)
            .ScoreApplicationAsync(app.Id, CancellationToken.None);

        Assert.Equal(CvScoringErrors.CvUnreadable, inFlight.LastFailure(CvScoringInFlight.ApplicationKey(app.Id, rubric.Id))?.Code);
        Assert.Contains(notif.GroupEvents, e => e.Group == "hr_admin" && e.EventType == "ReceiveApplicationStatusUpdate");
    }

    [Fact]
    public async Task A_later_success_clears_the_failure()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(app);
        var inFlight = new CvScoringInFlight();
        var key = CvScoringInFlight.ApplicationKey(app.Id, rubric.Id);
        inFlight.RecordFailure(key, "Lỗi AI: 503", CvScoringErrors.AiUnavailable);
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80), ("education", 60)) };

        await Scorer(uow, gemini, new RecordingFileStorage { FileBytes = CvBytes() }, inFlight: inFlight)
            .ScoreApplicationAsync(app.Id, CancellationToken.None);

        Assert.NotNull(app.CvJdAnalysisId);
        Assert.Null(inFlight.LastFailure(key));
    }

    [Fact]
    public void Repeated_failures_back_off_longer_and_keep_the_latest_reason()
    {
        var inFlight = new CvScoringInFlight();
        var first = inFlight.RecordFailure("k", "a", CvScoringErrors.CvUnreadable);
        var second = inFlight.RecordFailure("k", "b", CvScoringErrors.AiUnavailable);

        Assert.Equal(2, second.Attempts);
        Assert.Equal(CvScoringErrors.AiUnavailable, second.Code);
        Assert.True(second.RetryAfter - first.RetryAfter >= TimeSpan.FromMinutes(14)); // 15′ → 30′
    }

    [Fact]
    public async Task Hiring_manager_is_reminded_once_for_an_active_job_without_rubric()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(App(job.Id));
        var hm = HiringManagerSeed.Primary(uow, job.Id);
        var scorer = Scorer(uow, new FakeGeminiProvider(), new RecordingFileStorage());

        await scorer.NotifyMissingRubricAsync(CancellationToken.None);
        await scorer.NotifyMissingRubricAsync(CancellationToken.None);

        var n = Assert.Single(uow.Repo<Notification>().Items);
        Assert.Equal(hm.Id, n.RecipientUserId);
        Assert.Equal($"cv_rubric_missing:{job.Id}", n.DedupKey);
    }

    [Fact]
    public async Task No_reminder_when_the_job_has_a_rubric_or_no_applications()
    {
        var withRubric = Job();
        var noApps = Job();
        var uow = new InMemoryUnitOfWork().Seed(withRubric, noApps).Seed(DefaultRubric(withRubric.Id)).Seed(App(withRubric.Id));
        HiringManagerSeed.Primary(uow, withRubric.Id);
        HiringManagerSeed.Primary(uow, noApps.Id);

        await Scorer(uow, new FakeGeminiProvider(), new RecordingFileStorage()).NotifyMissingRubricAsync(CancellationToken.None);

        Assert.Empty(uow.Repo<Notification>().Items);
    }

    // ---------------- Luật hiển thị điểm ----------------

    [Fact]
    public void State_resolution_covers_every_case()
    {
        var live = Guid.NewGuid();
        var old = Guid.NewGuid();
        CvScoreState.AnalysisInfo Done(Guid? rubric, int score = 70) => new(CvAnalysisStatuses.Completed, rubric, score);

        Assert.Equal((CvScoreStates.NoCv, (int?)null), CvScoreState.Resolve(false, null, live));
        Assert.Equal((CvScoreStates.PendingRubric, (int?)null), CvScoreState.Resolve(true, null, null));
        Assert.Equal((CvScoreStates.PendingRubric, (int?)null), CvScoreState.Resolve(true, Done(null, 88), null)); // điểm AI cũ không hiện
        Assert.Equal((CvScoreStates.Queued, (int?)null), CvScoreState.Resolve(true, null, live));
        Assert.Equal((CvScoreStates.Scored, (int?)70), CvScoreState.Resolve(true, Done(live), live));
        Assert.Equal((CvScoreStates.Rescoring, (int?)70), CvScoreState.Resolve(true, Done(old), live));
        Assert.Equal((CvScoreStates.Queued, (int?)null), CvScoreState.Resolve(true, Done(null, 88), live));
        Assert.Equal((CvScoreStates.InvalidCv, (int?)null),
            CvScoreState.Resolve(true, new(CvAnalysisStatuses.InvalidCv, live, 0), live));
        Assert.Equal((CvScoreStates.InvalidCv, (int?)null), CvScoreState.Resolve(true, new("failed", null, 0), null));
    }

    [Fact]
    public void A_recorded_failure_turns_only_the_waiting_states_into_scoring_failed()
    {
        var live = Guid.NewGuid();
        var old = Guid.NewGuid();
        CvScoreState.AnalysisInfo Done(Guid? rubric, int score = 70) => new(CvAnalysisStatuses.Completed, rubric, score);
        var failure = new CvScoringInFlight.FailureState("Lỗi AI", CvScoringErrors.AiUnavailable, 2, DateTimeOffset.UtcNow.AddMinutes(30));

        Assert.Equal((CvScoreStates.ScoringFailed, (int?)null), CvScoreState.Resolve(true, null, live, failure));
        // Chấm lại theo bộ mới hỏng: vẫn giữ điểm thật theo bộ cũ.
        Assert.Equal((CvScoreStates.ScoringFailed, (int?)70), CvScoreState.Resolve(true, Done(old), live, failure));
        // Đã có điểm đúng bộ hiện hành: lỗi cũ không che điểm.
        Assert.Equal((CvScoreStates.Scored, (int?)70), CvScoreState.Resolve(true, Done(live), live, failure));
        Assert.Equal((CvScoreStates.PendingRubric, (int?)null), CvScoreState.Resolve(true, null, null, failure));
        Assert.Equal((CvScoreStates.NoCv, (int?)null), CvScoreState.Resolve(false, null, live, failure));
        Assert.Equal((CvScoreStates.Queued, (int?)null), CvScoreState.Resolve(true, null, live, null));
    }

    [Fact]
    public async Task Breakdown_explains_a_failed_scoring_with_reason_and_retry_time()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(app);
        var inFlight = new CvScoringInFlight();
        var failure = inFlight.RecordFailure(CvScoringInFlight.ApplicationKey(app.Id, rubric.Id), "Không đọc được", CvScoringErrors.CvUnreadable);

        var dto = await CvScoreBreakdownBuilder.BuildAsync(uow, app, null, CancellationToken.None, inFlight);

        Assert.Equal(CvScoreStates.ScoringFailed, dto.State);
        Assert.Null(dto.Total);
        Assert.Equal(CvScoringErrors.CvUnreadable, dto.FailureReason);
        Assert.Equal(1, dto.FailedAttempts);
        Assert.Equal(failure.RetryAfter, dto.RetryAt);
    }

    [Fact]
    public async Task A_failure_recorded_for_an_older_rubric_version_is_ignored()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(app);
        var inFlight = new CvScoringInFlight();
        inFlight.RecordFailure(CvScoringInFlight.ApplicationKey(app.Id, Guid.NewGuid()), "bộ cũ", CvScoringErrors.AiUnavailable);

        var dto = await CvScoreBreakdownBuilder.BuildAsync(uow, app, null, CancellationToken.None, inFlight);

        Assert.Equal(CvScoreStates.Queued, dto.State);
        Assert.Null(dto.RetryAt);
    }

    [Fact]
    public async Task Breakdown_shows_the_formula_with_real_numbers()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric);
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 85), ("education", null)) };
        var analysis = (await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;
        var app = App(job.Id, analysisId: analysis.Id);

        var dto = await CvScoreBreakdownBuilder.BuildAsync(uow, app, analysis, CancellationToken.None);

        Assert.Equal(CvScoreStates.Scored, dto.State);
        Assert.Equal(85, dto.Total);
        Assert.Equal(85m * 70m, dto.WeightedSum);
        Assert.Equal(70m, dto.TotalWeight);
        Assert.Equal(85m, dto.ExactTotal);
        var c = Assert.Single(dto.Criteria);
        Assert.Equal("Kinh nghiệm", c.Label);
        Assert.Equal(85m, c.Contribution);
        Assert.Equal("good", c.Band);
        Assert.Equal("bằng chứng experience", c.Evidence);
        var excluded = Assert.Single(dto.Excluded);
        Assert.Equal("education", excluded.Key);
        Assert.True(dto.IsCurrentRubric);
        Assert.Equal("Strong Hire", dto.Recommendation); // 85 ≥ 80 — suy từ điểm
    }

    [Fact]
    public async Task Breakdown_hides_legacy_ai_scores()
    {
        var job = Job();
        var legacy = new CvJdAnalysis
        {
            JobPostingId = job.Id, MatchScore = 91, Status = CvAnalysisStatuses.Completed, Summary = "AI tự cho",
            CriterionScores = "{}",
        };
        var app = App(job.Id, analysisId: legacy.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(legacy);

        var dto = await CvScoreBreakdownBuilder.BuildAsync(uow, app, legacy, CancellationToken.None);

        Assert.Equal(CvScoreStates.PendingRubric, dto.State);
        Assert.Null(dto.Total);
        Assert.Empty(dto.Criteria);
        Assert.Null(dto.Summary);
    }

    [Fact]
    public async Task Breakdown_explains_an_invalid_cv()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var invalid = new CvJdAnalysis
        {
            JobPostingId = job.Id, RubricDocumentId = rubric.Id, Status = CvAnalysisStatuses.InvalidCv,
            ErrorMessage = "File tải lên không phải là CV.",
        };
        var app = App(job.Id, analysisId: invalid.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(invalid);

        var dto = await CvScoreBreakdownBuilder.BuildAsync(uow, app, invalid, CancellationToken.None);

        Assert.Equal(CvScoreStates.InvalidCv, dto.State);
        Assert.Null(dto.Total);
        Assert.Contains("không phải là CV", dto.InvalidReason);
    }
}
