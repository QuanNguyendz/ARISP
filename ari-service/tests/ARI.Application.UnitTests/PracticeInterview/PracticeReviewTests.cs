using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.PracticeInterview;

/// <summary>
/// Xem lại buổi phỏng vấn thử — không gian riêng của ứng viên (UC-43, PortalPracticeFeature, ADR-051):
/// chỉ ứng viên sở hữu xem (IDOR), từ chối phiên thật, KHÔNG lộ verdict Pass/Not Pass, ghép nhận xét AI
/// vào đúng lượt hỏi–đáp; danh sách chỉ gồm phiên "practice" sắp mới nhất trước.
/// </summary>
public class PracticeReviewTests
{
    private static object? Prop(object obj, string name) => obj.GetType().GetProperty(name)?.GetValue(obj);

    // ---------- GetMyPracticeReviewQuery (transcript + nhận xét) ----------

    private static Task<Result<object>> Review(InMemoryUnitOfWork uow, Guid sessionId, Guid accountId, string? email = null)
        => new GetMyPracticeReviewQueryHandler(uow).Handle(new GetMyPracticeReviewQuery(sessionId, accountId, email), CancellationToken.None);

    [Fact]
    public async Task Review_session_not_found()
    {
        var res = await Review(new InMemoryUnitOfWork(), Guid.NewGuid(), Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Review_rejects_real_session()
    {
        var accId = Guid.NewGuid();
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, accountId: accId);
        var session = PracticeData.Session(app.Id, type: "real"); // buổi THẬT
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);

        var res = await Review(uow, session.Id, accId);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode); // ẩn buổi thật khỏi endpoint này
    }

    [Fact]
    public async Task Review_forbidden_for_non_owner()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, accountId: Guid.NewGuid()); // thuộc ứng viên khác
        var session = PracticeData.Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);

        var res = await Review(uow, session.Id, Guid.NewGuid()); // account khác

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Review_returns_turns_and_hides_verdict()
    {
        var accId = Guid.NewGuid();
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, accountId: accId);
        var session = PracticeData.Session(app.Id, status: "completed");
        var q = PracticeData.Question(session.Id, 1, "Giới thiệu bản thân?");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session)
            .Seed(q).Seed(PracticeData.Answer(session.Id, q.Id, "Tôi là dev"))
            .Seed(PracticeData.Eval(session.Id, app.Id));

        var res = await Review(uow, session.Id, accId);

        Assert.True(res.IsSuccess);
        Assert.Null(res.Value!.GetType().GetProperty("AiVerdict")); // KHÔNG lộ verdict (ADR-051)
        var turns = (IList)Prop(res.Value, "Turns")!;
        Assert.Single(turns);
        Assert.NotNull(Prop(res.Value, "Evaluation"));
        Assert.Equal(false, Prop(res.Value, "EvaluationPending"));
    }

    [Fact]
    public async Task Review_evaluation_pending_when_completed_without_eval()
    {
        var accId = Guid.NewGuid();
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, accountId: accId);
        var session = PracticeData.Session(app.Id, status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session); // chưa có Evaluation

        var res = await Review(uow, session.Id, accId);

        Assert.True(res.IsSuccess);
        Assert.Null(Prop(res.Value!, "Evaluation"));
        Assert.Equal(true, Prop(res.Value, "EvaluationPending")); // FE hiện "đang chấm"
    }

    [Fact]
    public async Task Review_merges_ai_analysis_into_turn_by_sequence()
    {
        var accId = Guid.NewGuid();
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, accountId: accId);
        var session = PracticeData.Session(app.Id, status: "completed");
        var q = PracticeData.Question(session.Id, 1);
        var eval = PracticeData.Eval(session.Id, app.Id);
        eval.QuestionAnalyses = "[{\"SequenceNumber\":1,\"Score\":90,\"Analysis\":\"Phân tích tốt\",\"Feedback\":\"Rõ ràng\"}]";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session)
            .Seed(q).Seed(PracticeData.Answer(session.Id, q.Id)).Seed(eval);

        var res = await Review(uow, session.Id, accId);

        var turn = ((IList)Prop(res.Value!, "Turns")!)[0]!;
        Assert.Equal(90m, Convert.ToDecimal(Prop(turn, "Score")));
        Assert.Equal("Phân tích tốt", Prop(turn, "Analysis"));
        Assert.Equal("Rõ ràng", Prop(turn, "Feedback"));
    }

    [Fact]
    public async Task Review_auto_links_legacy_app_by_email()
    {
        var accId = Guid.NewGuid();
        var job = PracticeData.Job();
        // Hồ sơ cũ CHƯA gắn tài khoản nhưng trùng email trong token → auto-link rồi coi là chủ sở hữu.
        var app = PracticeData.App(job.Id, accountId: null, email: "cand@example.io");
        var session = PracticeData.Session(app.Id, status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);

        var res = await Review(uow, session.Id, accId, email: "cand@example.io");

        Assert.True(res.IsSuccess);
        Assert.Equal(accId, app.CandidateAccountId); // đã gắn tài khoản
    }

    // ---------- GetMyPracticeSessionsQuery (danh sách buổi thử) ----------

    private static Task<Result<object>> Sessions(InMemoryUnitOfWork uow, Guid accountId, Guid? appId = null)
        => new GetMyPracticeSessionsQueryHandler(uow).Handle(new GetMyPracticeSessionsQuery(appId, accountId, null), CancellationToken.None);

    [Fact]
    public async Task Sessions_empty_when_candidate_has_no_applications()
    {
        var res = await Sessions(new InMemoryUnitOfWork(), Guid.NewGuid());

        Assert.True(res.IsSuccess);
        Assert.Empty((IList)res.Value!);
    }

    [Fact]
    public async Task Sessions_lists_only_practice_newest_first()
    {
        var accId = Guid.NewGuid();
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, accountId: accId);
        var older = PracticeData.Session(app.Id, round: 1, startedAt: DateTimeOffset.UtcNow.AddHours(-2));
        var newer = PracticeData.Session(app.Id, round: 2, startedAt: DateTimeOffset.UtcNow);
        var real = PracticeData.Session(app.Id, round: 1, type: "real");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(older, newer, real);

        var res = await Sessions(uow, accId);

        var items = (IList)res.Value!;
        Assert.Equal(2, items.Count);                          // buổi thật bị loại
        Assert.Equal(2, Prop(items[0]!, "RoundNumber"));       // mới nhất trước
    }

    [Fact]
    public async Task Sessions_includes_score_and_turn_count()
    {
        var accId = Guid.NewGuid();
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id, accountId: accId);
        var session = PracticeData.Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session)
            .Seed(PracticeData.Question(session.Id, 1), PracticeData.Question(session.Id, 2))
            .Seed(PracticeData.Eval(session.Id, app.Id));

        var res = await Sessions(uow, accId);

        var item = ((IList)res.Value!)[0]!;
        Assert.Equal(true, Prop(item, "HasEvaluation"));
        Assert.Equal(2, Prop(item, "TurnCount"));
    }
}
