using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.Playbooks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static ARI.Application.UnitTests.CvScoring.CvScoringKit;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// ADR-075: HM đổi CÔNG THỨC (trọng số, ngưỡng, điểm tối thiểu, trọng số ý kiểm) thì điểm được TÍNH LẠI từ câu trả
/// lời cũ của AI — không gọi AI, không đọc lại file CV. Đổi điều AI được hỏi (thêm tiêu chí, sửa lời, thêm điều kiện
/// bắt buộc) thì mới phải hỏi AI lại.
/// </summary>
public class CvFormulaRescoringTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    /// <summary>Bộ tiêu chí đang sống (bản đọc lại từ DB), đã sửa theo <paramref name="edit"/>.</summary>
    private static List<RubricCriterion> LiveCriteria(PlaybookDocument live, Action<List<RubricCriterion>> edit)
    {
        var criteria = CvRubricStore.Criteria(live);
        edit(criteria);
        return criteria;
    }

    private static async Task<(InMemoryUnitOfWork Uow, JobPosting Job, FakeGeminiProvider Gemini, CvScoringService Svc, CvJdAnalysis First)>
        ScoredOnceAsync(Result<CvJdAnalysisResultDto>? ai = null)
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));   // experience 70 · education 30
        var gemini = new FakeGeminiProvider { AnalyzeResult = ai ?? FakeGeminiProvider.Scored(("experience", 80), ("education", 40)) };
        var svc = Service(uow, gemini);
        var first = await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf");
        Assert.True(first.IsSuccess, first.Error);
        return (uow, job, gemini, svc, first.Value!);
    }

    private static async Task<CvRubricSaveResult> SaveAsync(
        InMemoryUnitOfWork uow, Guid jobId, List<RubricCriterion> criteria, CvScoringPolicy? policy = null)
    {
        var saved = await RubricService(uow).SaveForJobAsync(jobId, criteria, policy, Actor, CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error);
        return saved.Value!;
    }

    [Fact]
    public async Task Weight_change_is_recomputed_without_calling_the_ai()
    {
        var (uow, job, gemini, svc, first) = await ScoredOnceAsync();
        Assert.Equal(68, first.MatchScore);

        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        var saved = await SaveAsync(uow, job.Id, LiveCriteria(live, c => { c[0].Weight = 50; c[1].Weight = 50; }));
        Assert.True(saved.FormulaOnly);

        var second = await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(1, gemini.AnalyzeCallCount);                   // không lượt gọi AI nào mới
        var derived = second.Value!;
        Assert.Equal(saved.Document.Id, derived.RubricDocumentId);
        Assert.Equal(first.Id, derived.DerivedFromAnalysisId);
        Assert.Equal(60, derived.MatchScore);                        // (80×50 + 40×50) ÷ 100
        Assert.Equal(CvRecommendations.Caution, derived.OverallRecommendation);
        Assert.Equal(0, derived.PromptTokens + derived.CompletionTokens);
        Assert.Equal(first.Summary, derived.Summary);
        Assert.NotNull(derived.ScoringPolicy);
        Assert.Equal(2, uow.Repo<CvJdAnalysis>().Items.Count);      // bản cũ giữ nguyên làm lịch sử
    }

    [Fact]
    public async Task Tier_only_change_changes_the_recommendation_without_calling_the_ai()
    {
        var (uow, job, gemini, svc, _) = await ScoredOnceAsync();
        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        var saved = await SaveAsync(uow, job.Id, CvRubricStore.Criteria(live),
            new CvScoringPolicy { Tiers = new CvTierCuts { StrongHireFrom = 90, HireFrom = 75, CautionFrom = 60 } });
        Assert.True(saved.FormulaOnly);

        var second = (await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;

        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.Equal(68, second.MatchScore);
        Assert.Equal(CvRecommendations.Caution, second.OverallRecommendation);   // 68 < 75
    }

    [Fact]
    public async Task Band_cut_change_is_recomputed_from_the_position_inside_the_band()
    {
        var (uow, job, gemini, svc, _) = await ScoredOnceAsync();
        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        await SaveAsync(uow, job.Id, CvRubricStore.Criteria(live),
            new CvScoringPolicy { Bands = new CvBandCuts { ExcellentFrom = 90, GoodFrom = 60, FairFrom = 40 } });

        var second = (await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;

        Assert.Equal(1, gemini.AnalyzeCallCount);
        // experience: 80 trong dải Tốt 70–89 (vị trí 10/19) → dải Tốt mới 60–89 → 60 + 29 × 10/19 = 75,26 → 75
        // education: 40 ở đáy dải Đạt một phần → vẫn 40. Tổng (75×70 + 40×30) ÷ 100 = 64,5 → 65.
        Assert.Equal(65, second.MatchScore);
    }

    [Fact]
    public async Task Removing_a_criterion_is_recomputed()
    {
        var (uow, job, gemini, svc, _) = await ScoredOnceAsync();
        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        var saved = await SaveAsync(uow, job.Id, LiveCriteria(live, c => { c.RemoveAt(1); c[0].Weight = 100; }));
        Assert.True(saved.FormulaOnly);

        var second = (await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;

        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.Equal(80, second.MatchScore);
    }

    [Fact]
    public async Task Adding_a_knockout_asks_the_ai_again()
    {
        var (uow, job, gemini, svc, _) = await ScoredOnceAsync();
        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        var saved = await SaveAsync(uow, job.Id, LiveCriteria(live, c => c.Add(new RubricCriterion
        {
            Key = "jlpt", Name = "JLPT N2", Weight = 0, Kind = RubricCriterionKinds.Knockout,
        })));
        Assert.False(saved.FormulaOnly);

        await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.Equal(2, gemini.AnalyzeCallCount);
        Assert.Contains("jlpt", gemini.LastRequest!.CriterionKeys);
        Assert.Contains("ĐIỀU KIỆN BẮT BUỘC", gemini.LastRequest.RubricInstruction);
    }

    [Fact]
    public async Task Editing_what_the_ai_reads_asks_the_ai_again()
    {
        var (uow, job, gemini, svc, _) = await ScoredOnceAsync();
        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        await SaveAsync(uow, job.Id, LiveCriteria(live, c => c[0].Description = "chuẩn chấm mới"));

        await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.Equal(2, gemini.AnalyzeCallCount);
    }

    [Fact]
    public async Task Invalid_cv_carries_over_without_the_ai()
    {
        var notCv = Result.Success(new CvJdAnalysisResultDto { IsValidCv = false, Provider = "Gemini", RawResponse = "{}" });
        var (uow, job, gemini, svc, first) = await ScoredOnceAsync(notCv);
        Assert.Equal(CvAnalysisStatuses.InvalidCv, first.Status);

        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        await SaveAsync(uow, job.Id, LiveCriteria(live, c => { c[0].Weight = 40; c[1].Weight = 60; }));
        var second = (await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;

        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.Equal(CvAnalysisStatuses.InvalidCv, second.Status);
        Assert.Equal(first.Id, second.DerivedFromAnalysisId);
    }

    [Fact]
    public async Task Derived_chain_points_to_the_original_ai_call()
    {
        var (uow, job, gemini, svc, first) = await ScoredOnceAsync();

        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        await SaveAsync(uow, job.Id, LiveCriteria(live, c => { c[0].Weight = 50; c[1].Weight = 50; }));
        var second = (await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;

        live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        await SaveAsync(uow, job.Id, LiveCriteria(live, c => { c[0].Weight = 90; c[1].Weight = 10; }));
        var third = (await svc.ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;

        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.Equal(first.Id, second.DerivedFromAnalysisId);
        Assert.Equal(first.Id, third.DerivedFromAnalysisId);
        Assert.Equal(76, third.MatchScore);                          // (80×90 + 40×10) ÷ 100
    }

    /// <summary>Bản chấm thời AI tự cho điểm tổng (không gắn bộ tiêu chí) không bao giờ làm nguồn tính lại.</summary>
    [Fact]
    public async Task Pre_rubric_analysis_is_never_a_donor()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id)).Seed(new CvJdAnalysis
        {
            JobPostingId = job.Id,
            CvHash = CvScoringService.ComputeHash(CvBytes()),
            RubricDocumentId = null,
            MatchScore = 90,
            CriterionScores = "{\"experience\": 90, \"education\": 90}",
        });
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80), ("education", 40)) };

        var res = await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.Equal(68, res.Value!.MatchScore);
        Assert.Null(res.Value.DerivedFromAnalysisId);
    }

    [Fact]
    public async Task Knockout_answer_is_scored_and_snapshotted()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var criteria = CvRubricStore.Criteria(rubric);
        criteria.Add(new RubricCriterion { Key = "jlpt", Name = "JLPT N2", Weight = 0, Kind = RubricCriterionKinds.Knockout });
        rubric.RubricJson = ScoringRubric.Serialize(criteria);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric);
        var ai = FakeGeminiProvider.Scored(("experience", 90), ("education", 90));
        ai.Value!.Criteria.Add(new CvCriterionAiResult { Key = "jlpt", Met = false, Reasoning = "CV không nhắc JLPT" });
        var gemini = new FakeGeminiProvider { AnalyzeResult = ai };

        var res = (await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;

        Assert.Equal(90, res.MatchScore);                            // điểm vẫn tính, vẫn hiện
        Assert.Equal(CvGateStatuses.Fail, res.GateStatus);
        Assert.Equal(CvRecommendations.Reject, res.OverallRecommendation);
        var view = CvScoreSnapshot.Parse(res.CriterionScores).Single(v => v.Key == "jlpt");
        Assert.True(view.IsKnockout);
        Assert.False(view.Met);
        Assert.Equal(CvGateOutcomes.Fail, view.Gate);
    }

    // ---------------- Hàng đợi: đường nhanh không đọc file ----------------

    private static ARI.Domain.Entities.Application App(Guid jobId) => new()
    {
        JobPostingId = jobId,
        CandidateEmail = $"{Guid.NewGuid():N}@x.io",
        CandidateName = "Nguyễn Văn A",
        CvFileUrl = "cv/a.pdf",
        Status = ApplicationStatuses.CvSubmitted,
    };

    private static CvApplicationScorer Scorer(InMemoryUnitOfWork uow, FakeGeminiProvider gemini, RecordingFileStorage storage)
    {
        var inFlight = new CvScoringInFlight();
        return new CvApplicationScorer(uow, Service(uow, gemini, storage: storage, inFlight: inFlight), storage,
            new RecordingNotificationService(), inFlight, NullLogger<CvApplicationScorer>.Instance);
    }

    [Fact]
    public async Task Queue_recomputes_a_formula_change_without_reading_the_cv_file()
    {
        var job = Job();
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id)).Seed(app);
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80), ("education", 40)) };
        var storage = new RecordingFileStorage { FileBytes = CvBytes() };
        var scorer = Scorer(uow, gemini, storage);
        await scorer.ScoreApplicationAsync(app.Id, CancellationToken.None);
        var firstId = app.CvJdAnalysisId;
        Assert.NotNull(firstId);

        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        await SaveAsync(uow, job.Id, LiveCriteria(live, c => { c[0].Weight = 50; c[1].Weight = 50; }));
        Assert.Contains(app.Id, await scorer.FindStaleApplicationIdsAsync(job.Id, 100, CancellationToken.None));

        storage.FileBytes = null;                                    // file không đọc được — đường nhanh không cần tới
        await scorer.ScoreApplicationAsync(app.Id, CancellationToken.None);

        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.NotEqual(firstId, app.CvJdAnalysisId);
        var attached = uow.Repo<CvJdAnalysis>().Items.Single(a => a.Id == app.CvJdAnalysisId);
        Assert.Equal(firstId, attached.DerivedFromAnalysisId);
        Assert.Equal(60, attached.MatchScore);
        Assert.Empty(await scorer.FindStaleApplicationIdsAsync(job.Id, 100, CancellationToken.None));
    }

    // ---------------- Xem trước tác động ----------------

    private static async Task<(InMemoryUnitOfWork Uow, JobPosting Job, FakeGeminiProvider Gemini, Guid HmId)> JobWithScoredApplicationAsync()
    {
        var job = Job();
        var app = App(job.Id);
        var hmId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id)).Seed(app);
        HiringManagerSeed.Primary(uow, job.Id, hmId);
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80), ("education", 40)) };
        await Scorer(uow, gemini, new RecordingFileStorage { FileBytes = CvBytes() }).ScoreApplicationAsync(app.Id, CancellationToken.None);
        return (uow, job, gemini, hmId);
    }

    [Fact]
    public async Task Preview_computes_the_new_scores_in_memory()
    {
        var (uow, job, gemini, hmId) = await JobWithScoredApplicationAsync();
        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        var draft = CvRubricEditing.ToInput(LiveCriteria(live, c => { c[0].Weight = 50; c[1].Weight = 50; c[0].MinScore = 85; }));
        var rowsBefore = uow.Repo<CvJdAnalysis>().Items.Count;

        var res = await new PreviewJobCvRubricQueryHandler(uow).Handle(
            new PreviewJobCvRubricQuery(job.Id, draft, null, hmId, RoleNames.HiringManager), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        var p = res.Value!;
        Assert.Equal(1, p.RecomputeCount);
        Assert.Equal(0, p.AiRescoreCount);
        Assert.Equal(1, p.GateFailCount);                            // kinh nghiệm 80 < 85
        var item = Assert.Single(p.Items);
        Assert.Equal(68, item.OldScore);
        Assert.Equal(60, item.NewScore);
        Assert.Equal(CvRecommendations.Reject, item.NewRecommendation);
        Assert.Equal(1, gemini.AnalyzeCallCount);                    // không gọi AI
        Assert.Equal(rowsBefore, uow.Repo<CvJdAnalysis>().Items.Count);   // không ghi gì
    }

    [Fact]
    public async Task Preview_counts_applications_that_would_need_the_ai()
    {
        var (uow, job, _, hmId) = await JobWithScoredApplicationAsync();
        var live = (await CvRubricStore.LiveAsync(uow, job.Id))!;
        var draft = CvRubricEditing.ToInput(LiveCriteria(live, c => c[0].Description = "chuẩn mới"));

        var p = (await new PreviewJobCvRubricQueryHandler(uow).Handle(
            new PreviewJobCvRubricQuery(job.Id, draft, null, hmId, RoleNames.HiringManager), CancellationToken.None)).Value!;

        Assert.Equal(0, p.RecomputeCount);
        Assert.Equal(1, p.AiRescoreCount);
        Assert.True(Assert.Single(p.Items).RequiresAi);
    }

    [Fact]
    public async Task Recruiter_cannot_preview()
    {
        var (uow, job, _, _) = await JobWithScoredApplicationAsync();

        var res = await new PreviewJobCvRubricQueryHandler(uow).Handle(
            new PreviewJobCvRubricQuery(job.Id, SampleRubric(), null, job.CreatedByUserId, RoleNames.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ---------------- Giải thích điểm ----------------

    [Fact]
    public async Task Breakdown_explains_gates_and_the_formula_used()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var criteria = CvRubricStore.Criteria(rubric);
        criteria[0].MinScore = 85;
        criteria.Add(new RubricCriterion { Key = "jlpt", Name = "JLPT N2", Weight = 0, Kind = RubricCriterionKinds.Knockout });
        rubric.RubricJson = ScoringRubric.Serialize(criteria);
        rubric.ScoringPolicyJson = CvScoringPolicy.ToStorage(new CvScoringPolicy { Tiers = new CvTierCuts { StrongHireFrom = 90, HireFrom = 60, CautionFrom = 40 } });
        var app = App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric).Seed(app);
        var ai = FakeGeminiProvider.Scored(("experience", 80), ("education", 40));
        ai.Value!.Criteria.Add(new CvCriterionAiResult { Key = "jlpt", Met = true, Evidence = "JLPT N2 (2021)" });
        var gemini = new FakeGeminiProvider { AnalyzeResult = ai };
        await Scorer(uow, gemini, new RecordingFileStorage { FileBytes = CvBytes() }).ScoreApplicationAsync(app.Id, CancellationToken.None);
        var analysis = uow.Repo<CvJdAnalysis>().Items.Single();

        var dto = await CvScoreBreakdownBuilder.BuildAsync(uow, app, analysis, CancellationToken.None);

        Assert.Equal(68, dto.Total);
        Assert.Equal(CvRecommendations.Reject, dto.Recommendation);         // kinh nghiệm 80 < 85
        Assert.Equal(CvRecommendations.Hire, dto.ScoreRecommendation);      // theo điểm: 68 ≥ 60
        Assert.Equal(CvGateStatuses.Fail, dto.GateStatus);
        Assert.Equal(2, dto.Criteria.Count);                                // điều kiện bắt buộc không nằm trong phép tính
        Assert.Empty(dto.Excluded);
        Assert.Equal(new[] { "min_score", "knockout" }, dto.Gates.Select(g => g.Type));
        Assert.Equal(CvGateOutcomes.Fail, dto.Gates[0].Outcome);
        Assert.Equal(CvGateOutcomes.Pass, dto.Gates[1].Outcome);
        Assert.Equal("JLPT N2 (2021)", dto.Gates[1].Evidence);
        Assert.Equal(90, dto.Policy!.Tiers.StrongHireFrom);
        Assert.False(dto.Derived);
    }
}
