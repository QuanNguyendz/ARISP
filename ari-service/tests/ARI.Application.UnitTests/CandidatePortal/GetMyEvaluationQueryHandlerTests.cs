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
/// Xem chi tiết đánh giá đã chia sẻ (<see cref="GetMyEvaluationQueryHandler"/>, test-plan B12): bảo vệ IDOR
/// (chỉ chủ hồ sơ mới xem, không auto-link khi hồ sơ đã gắn tài khoản), cổng chia sẻ
/// (<c>HrReview.ShareEvaluation</c>) tách biệt với NotFound, và các nhánh thiếu phiên/hồ sơ/đánh giá.
/// </summary>
public class GetMyEvaluationQueryHandlerTests
{
    private static InterviewSession Session(Guid appId) =>
        new() { ApplicationId = appId, RoundNumber = 1, SessionType = "real", Status = "completed" };

    private static ARI.Domain.Entities.Application App(Guid? accId, string email = "owner@example.io") =>
        new() { JobPostingId = Guid.NewGuid(), CandidateAccountId = accId, CandidateEmail = email, CandidateName = "A", Status = "interview" };

    private static Evaluation Eval(Guid sessionId, Guid appId) => new()
    {
        SessionId = sessionId,
        ApplicationId = appId,
        RoundNumber = 1,
        SessionType = "real",
        AiVerdict = "pass",
        OverallScore = 82m,
        CriterionScores = "{\"technical\":88}",
        QuestionAnalyses = "[{\"sequence_number\":1}]",
        LanguageAssessment = "{\"cefr_level\":\"B2\"}",
        Reasoning = "Trả lời tốt",
        RecommendedNextStep = "Mời vòng sau",
    };

    private static ARI.Domain.Entities.HrReview Review(Guid evalId, bool share) =>
        new() { EvaluationId = evalId, ShareEvaluation = share, FinalVerdict = "pass" };

    private static Task<Result<object>> Run(InMemoryUnitOfWork uow, GetMyEvaluationQuery q) =>
        new GetMyEvaluationQueryHandler(uow).Handle(q, CancellationToken.None);

    [Fact]
    public async Task Non_owner_is_forbidden_and_evaluation_is_not_leaked()
    {
        var ownerAcc = Guid.NewGuid();
        var app = App(ownerAcc);
        var session = Session(app.Id);
        var eval = Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session).Seed(eval).Seed(Review(eval.Id, share: true));

        // Kẻ khác: account + email đều lệch, hồ sơ đã gắn tài khoản nên KHÔNG auto-link.
        var res = await Run(uow, new GetMyEvaluationQuery(session.Id, Guid.NewGuid(), "attacker@example.io"));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Equal("Forbidden", res.Error);
    }

    [Fact]
    public async Task Owner_with_unshared_evaluation_is_rejected_but_not_as_not_found()
    {
        var acc = Guid.NewGuid();
        var app = App(acc);
        var session = Session(app.Id);
        var eval = Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session).Seed(eval).Seed(Review(eval.Id, share: false));

        var res = await Run(uow, new GetMyEvaluationQuery(session.Id, acc, "owner@example.io"));

        Assert.True(res.IsFailure);
        Assert.Contains("chưa được chia sẻ", res.Error);
        Assert.NotEqual(CommonErrorCodes.NotFound, res.ErrorCode); // cổng chia sẻ ≠ không tồn tại
    }

    [Fact]
    public async Task Owner_with_shared_evaluation_gets_the_full_report()
    {
        var acc = Guid.NewGuid();
        var app = App(acc);
        var session = Session(app.Id);
        var eval = Eval(session.Id, app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session).Seed(eval).Seed(Review(eval.Id, share: true));

        var res = await Run(uow, new GetMyEvaluationQuery(session.Id, acc, "owner@example.io"));

        Assert.True(res.IsSuccess);
        // Value là anonymous object → serialize theo kiểu runtime rồi soi các trường then chốt.
        var json = JsonSerializer.Serialize(res.Value, res.Value!.GetType());
        Assert.Contains("\"AiVerdict\":\"pass\"", json);
        Assert.Contains("\"OverallScore\":82", json);
        Assert.Contains("technical", json);        // CriterionScores
        Assert.Contains("sequence_number", json);  // QuestionAnalyses
        Assert.Contains("cefr_level", json);        // LanguageAssessment
    }

    [Fact]
    public async Task Missing_session_returns_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(),
            new GetMyEvaluationQuery(Guid.NewGuid(), Guid.NewGuid(), "owner@example.io"));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Contains("Không tìm thấy buổi phỏng vấn", res.Error);
    }

    [Fact]
    public async Task Missing_application_returns_not_found()
    {
        var session = Session(Guid.NewGuid()); // ApplicationId trỏ hồ sơ không seed
        var uow = new InMemoryUnitOfWork().Seed(session);

        var res = await Run(uow, new GetMyEvaluationQuery(session.Id, Guid.NewGuid(), "owner@example.io"));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Owner_without_any_evaluation_returns_not_found()
    {
        var acc = Guid.NewGuid();
        var app = App(acc);
        var session = Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session); // chưa có Evaluation

        var res = await Run(uow, new GetMyEvaluationQuery(session.Id, acc, "owner@example.io"));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Contains("chưa được khởi tạo", res.Error);
    }
}
