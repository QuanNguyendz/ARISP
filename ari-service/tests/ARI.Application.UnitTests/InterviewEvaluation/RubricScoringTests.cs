using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Playbooks;
using ARI.Application.UnitTests.PracticeInterview;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.InterviewEvaluation;

/// <summary>
/// Chấm điểm phỏng vấn theo bộ tiêu chí của doanh nghiệp (ADR-060).
///
/// Trước đây prompt ép cứng 8 khoá tiếng Anh rồi hỏi luôn <c>score</c> tổng — con số ấy không phải
/// trung bình có trọng số của gì cả, nên "chấm theo tiêu chí công ty" chỉ là hình thức. Nay AI chỉ
/// chấm TỪNG tiêu chí, backend cộng có trọng số và so với <c>InterviewPassScore</c>.
/// </summary>
public class RubricScoringTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow, StubAiProvider ai)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService(), ai, new RecordingTtsService());

    private static PlaybookDocument Rubric(Guid? jobId, int? round, params (string Key, string Name, decimal Weight)[] rows)
        => new()
        {
            Scope = round.HasValue ? PlaybookScope.ScopeRound
                  : jobId.HasValue ? PlaybookScope.ScopeJobPosting
                  : PlaybookScope.ScopeOrg,
            ScopeRefId = jobId,
            RoundNumber = round,
            DocumentType = ScoringRubric.TypeInterviewRubric,
            FileName = "rubric.xlsx",
            UploadedByUserId = Guid.NewGuid(),
            RubricJson = ScoringRubric.Serialize(
                rows.Select(r => new RubricCriterion { Key = r.Key, Name = r.Name, Weight = r.Weight }).ToList()),
        };

    /// <summary>Bộ mẫu 60/40 dùng lại ở phần lớn test bên dưới.</summary>
    private static PlaybookDocument SixtyForty(Guid jobId)
        => Rubric(jobId, null, ("technical", "Chuyên môn", 60), ("communication", "Giao tiếp", 40));

    private static StubAiProvider AiScoring(string criterionScoresJson, decimal aiScore = 95m, string verdict = "pass")
    {
        var ai = new StubAiProvider();
        ai.Evaluation.CriterionScoresJson = criterionScoresJson;
        ai.Evaluation.Score = aiScore;
        ai.Evaluation.Verdict = verdict;
        return ai;
    }

    // ---------- Điểm cuối = trung bình có trọng số, KHÔNG lấy số của model ----------

    [Fact]
    public async Task Overall_score_is_recomputed_from_criteria_ignoring_the_model_number()
    {
        var job = PracticeData.Job();
        job.InterviewPassScore = 70;
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(SixtyForty(job.Id));
        // Model tự khai 95 — con số đó phải bị bỏ qua.
        var ai = AiScoring("{\"technical\": 90, \"communication\": 50}", aiScore: 95m);

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(74m, eval.OverallScore!.Value);          // 90*0.6 + 50*0.4, không phải 95
    }

    [Fact]
    public async Task Company_criteria_reach_the_ai_provider()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, round: 2, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session)
            .Seed(Rubric(job.Id, 2, ("system_design", "Thiết kế hệ thống", 100)));
        var ai = AiScoring("{\"system_design\": 80}");

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var sent = Assert.Single(ai.LastEvaluationContext!.Criteria);
        Assert.Equal("system_design", sent.Key);
        Assert.Equal("Thiết kế hệ thống", sent.Name);
    }

    // ---------- Verdict theo ngưỡng cấu hình, không theo cảm tính model ----------

    [Theory]
    [InlineData(70, "pass")]      // 74 ≥ 70
    [InlineData(74, "pass")]      // đúng bằng ngưỡng vẫn là đạt
    [InlineData(80, "not_pass")]  // 74 < 80
    public async Task Verdict_follows_the_configured_threshold(int passScore, string expected)
    {
        var job = PracticeData.Job();
        job.InterviewPassScore = passScore;
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(SixtyForty(job.Id));
        // Model luôn nói "pass" — verdict phải do ngưỡng quyết định.
        var ai = AiScoring("{\"technical\": 90, \"communication\": 50}", verdict: "pass");

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(74m, eval.OverallScore!.Value);
        Assert.Equal(expected, eval.AiVerdict);
    }

    // ---------- Ảnh chụp nhãn + trọng số ----------

    [Fact]
    public async Task Saved_scores_snapshot_label_and_weight_for_later_explanation()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(SixtyForty(job.Id));
        var ai = AiScoring("{\"technical\": 90, \"communication\": 50}");

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        using var doc = JsonDocument.Parse(eval.CriterionScores!);
        var tech = doc.RootElement.GetProperty("technical");
        Assert.Equal(90m, tech.GetProperty("score").GetDecimal());
        Assert.Equal("Chuyên môn", tech.GetProperty("label").GetString());
        Assert.Equal(60m, tech.GetProperty("weight").GetDecimal());
    }

    /// <summary>Khoá lạ ngoài rubric bị loại khỏi ảnh chụp — bảng điểm chỉ hiện tiêu chí công ty khai.</summary>
    [Fact]
    public async Task Criteria_the_model_invented_are_dropped()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(SixtyForty(job.Id));
        var ai = AiScoring("{\"technical\": 80, \"communication\": 80, \"enthusiasm\": 100}");

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(80m, eval.OverallScore!.Value);              // "enthusiasm" không kéo điểm lên
        Assert.DoesNotContain("enthusiasm", eval.CriterionScores);
    }

    // ---------- Các trường hợp không có dữ liệu → giữ nguyên hành vi cũ ----------

    /// <summary>Chưa khai rubric: hệ thống chạy y như trước ADR-060 (điểm + verdict của AI).</summary>
    [Fact]
    public async Task Without_a_rubric_the_ai_score_and_verdict_are_kept()
    {
        var job = PracticeData.Job();
        job.InterviewPassScore = 90;                              // ngưỡng cao, nhưng không có rubric thì không áp
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        var ai = AiScoring("{\"technical\": 10}", aiScore: 82m, verdict: "pass");

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(82m, eval.OverallScore!.Value);
        Assert.Equal("pass", eval.AiVerdict);
        Assert.Empty(ai.LastEvaluationContext!.Criteria);
    }

    /// <summary>
    /// Có rubric mà AI không chấm nổi tiêu chí nào → giữ điểm AI, KHÔNG âm thầm cho 0.
    /// Cho 0 ở đây là đánh trượt ứng viên vì lỗi của model.
    /// </summary>
    [Fact]
    public async Task Rubric_present_but_no_criterion_scored_falls_back_to_ai_score()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(SixtyForty(job.Id));
        var ai = AiScoring("{}", aiScore: 66m, verdict: "not_pass");

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(66m, eval.OverallScore!.Value);
        Assert.Equal("not_pass", eval.AiVerdict);
    }

    /// <summary>Buổi THỬ cũng chấm theo đúng rubric — ứng viên luyện tập trên cùng thước đo.</summary>
    [Fact]
    public async Task Practice_session_uses_the_same_rubric()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "practice");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(SixtyForty(job.Id));
        var ai = AiScoring("{\"technical\": 90, \"communication\": 50}", aiScore: 95m);

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(74m, eval.OverallScore!.Value);
    }

    /// <summary>Rubric của tin khác không được áp sang tin này (ADR-025: phạm vi đọc từ playbook_documents).</summary>
    [Fact]
    public async Task Rubric_of_another_job_is_not_applied()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session)
            .Seed(SixtyForty(Guid.NewGuid()));                    // rubric của tin KHÁC
        var ai = AiScoring("{\"technical\": 90, \"communication\": 50}", aiScore: 95m);

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(95m, eval.OverallScore!.Value);              // giữ điểm AI vì không có rubric áp dụng
        Assert.Empty(ai.LastEvaluationContext!.Criteria);
    }

    /// <summary>Rubric đã xoá mềm phải hết tác dụng ngay (ADR-025).</summary>
    [Fact]
    public async Task Soft_deleted_rubric_stops_being_applied()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, status: "interview");
        var session = PracticeData.Session(app.Id, type: "real");
        var deleted = SixtyForty(job.Id);
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session).Seed(deleted);
        var ai = AiScoring("{\"technical\": 90, \"communication\": 50}", aiScore: 95m);

        await Svc(uow, ai).EndSessionAsync(session.Id, "completed", CancellationToken.None);

        var eval = Assert.Single(uow.Repo<Evaluation>().Items);
        Assert.Equal(95m, eval.OverallScore!.Value);
    }
}
