using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Xem chi tiết đánh giá đã chia sẻ (<see cref="GetMyEvaluationQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetMyEvaluation" (UTCID01–09): thiếu phiên/hồ sơ/đánh giá/review, IDOR (Forbidden) + auto-link theo email,
/// cổng chia sẻ <c>HrReview.ShareEvaluation</c> tách khỏi NotFound, và lỗi query đánh giá.
/// </summary>
public class GetMyEvaluationQueryHandlerTests
{
    private static readonly Guid CandidateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private const string Email = "candidate@example.com";

    private static InterviewSession Session(Guid appId)
        => new() { Id = Guid.NewGuid(), ApplicationId = appId, RoundNumber = 1, RoundType = "technical", InterviewLanguage = "vi", SessionType = "real", Status = "completed" };

    private static ARI.Domain.Entities.Application App(Guid? owner, string email = Email)
        => new() { Id = Guid.NewGuid(), JobPostingId = Guid.NewGuid(), CandidateAccountId = owner, CandidateEmail = email, CandidateName = "A", Status = "interview" };

    private static Evaluation Eval(Guid sessionId, Guid appId) => new()
    {
        Id = Guid.NewGuid(), SessionId = sessionId, ApplicationId = appId, RoundNumber = 1, SessionType = "real",
        AiVerdict = "pass", OverallScore = 82m, CriterionScores = "{\"technical\":88}",
        QuestionAnalyses = "[{\"sequence_number\":1}]", LanguageAssessment = "{\"cefr_level\":\"B2\"}",
    };

    private static ARI.Domain.Entities.HrReview Review(Guid evalId, bool share)
        => new() { Id = Guid.NewGuid(), EvaluationId = evalId, ReviewedByUserId = Guid.NewGuid(), ShareEvaluation = share, FinalVerdict = "pass" };

    private static Task<Result<object>> Run(InMemoryUnitOfWork uow, Guid sessionId, Guid candidate = default, string? email = Email)
        => new GetMyEvaluationQueryHandler(uow).Handle(new GetMyEvaluationQuery(sessionId, candidate == default ? CandidateId : candidate, email), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Missing_session()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.NewGuid());
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy buổi phỏng vấn.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Missing_application()
    {
        var session = Session(Guid.NewGuid());   // ApplicationId trỏ hồ sơ không seed
        var res = await Run(new InMemoryUnitOfWork().Seed(session), session.Id);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy hồ sơ ứng tuyển liên quan.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Other_candidate_is_forbidden()
    {
        var app = App(Guid.NewGuid());            // đã gắn tài khoản khác → không auto-link
        var session = Session(app.Id);
        var eval = Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session).Seed(eval).Seed(Review(eval.Id, true));
        var res = await Run(uow, session.Id, candidate: Guid.NewGuid(), email: "attacker@example.io");
        Assert.True(res.IsFailure);
        Assert.Equal("Forbidden", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID04_Owner_null_email_matches_links_and_continues()
    {
        var app = App(owner: null);               // trùng email trong token → auto-link
        var session = Session(app.Id);
        var eval = Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session).Seed(eval).Seed(Review(eval.Id, true));
        var res = await Run(uow, session.Id);
        Assert.True(res.IsSuccess);
        Assert.Equal(CandidateId, app.CandidateAccountId);   // đã gắn tài khoản
    }

    [Fact]
    public async Task UTCID05_Missing_evaluation()
    {
        var app = App(CandidateId);
        var session = Session(app.Id);
        var res = await Run(new InMemoryUnitOfWork().Seed(app).Seed(session), session.Id);
        Assert.True(res.IsFailure);
        Assert.Equal("Báo cáo đánh giá chưa được khởi tạo.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID06_Missing_review()
    {
        var app = App(CandidateId);
        var session = Session(app.Id);
        var eval = Eval(session.Id, app.Id);
        var res = await Run(new InMemoryUnitOfWork().Seed(app).Seed(session).Seed(eval), session.Id);
        Assert.True(res.IsFailure);
        Assert.Equal("Kết quả đánh giá chi tiết chưa được chia sẻ cho vòng phỏng vấn này.", res.Error);
    }

    [Fact]
    public async Task UTCID07_Review_not_shared()
    {
        var app = App(CandidateId);
        var session = Session(app.Id);
        var eval = Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session).Seed(eval).Seed(Review(eval.Id, share: false));
        var res = await Run(uow, session.Id);
        Assert.True(res.IsFailure);
        Assert.Contains("chưa được chia sẻ", res.Error);
        Assert.NotEqual(CommonErrorCodes.NotFound, res.ErrorCode);   // cổng chia sẻ ≠ không tồn tại
    }

    [Fact]
    public async Task UTCID08_Shared_returns_full_report()
    {
        var app = App(CandidateId);
        var session = Session(app.Id);
        var eval = Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session).Seed(eval).Seed(Review(eval.Id, share: true));
        var res = await Run(uow, session.Id);
        Assert.True(res.IsSuccess);
        var json = JsonSerializer.Serialize(res.Value, res.Value!.GetType());
        Assert.Contains("\"AiVerdict\":\"pass\"", json);
        Assert.Contains("\"OverallScore\":82", json);
        Assert.Contains("technical", json);
        Assert.Contains("cefr_level", json);
    }

    [Fact]
    public async Task UTCID09_Evaluation_lookup_error()
    {
        var app = App(CandidateId);
        var session = Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session).FailFindFor<Evaluation>("Evaluation DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, session.Id));
        Assert.Equal("Evaluation DB Error", ex.Message);
    }
}
