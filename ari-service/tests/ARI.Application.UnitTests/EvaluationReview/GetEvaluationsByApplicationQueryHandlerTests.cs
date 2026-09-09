using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Evaluations;
using ARI.Application.Evaluations.Queries.GetEvaluationsByApplication;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.EvaluationReview;

/// <summary>
/// Đánh giá theo hồ sơ (UC-64, <see cref="GetEvaluationsByApplicationQueryHandler"/>): các vòng của một ứng viên,
/// ẩn buổi thử, kèm trạng thái HR review.
/// </summary>
public class GetEvaluationsByApplicationQueryHandlerTests
{
    /// <summary>Mặc định chạy dưới quyền quản trị viên; phạm vi dữ liệu có test riêng ở cuối file.</summary>
    private static Task<Result<List<EvaluationListItemResponse>>> Run(
        InMemoryUnitOfWork uow, Guid appId, Guid? userId = null, string? role = null)
        => new GetEvaluationsByApplicationQueryHandler(uow).Handle(
            new GetEvaluationsByApplicationQuery(appId, userId ?? Guid.NewGuid(), role ?? AppRoles.HrAdmin),
            CancellationToken.None);

    [Fact]
    public async Task Application_not_found_fails()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("Application not found", res.Error);
    }

    [Fact]
    public async Task Job_not_found_fails()
    {
        var app = EvaluationData.App(Guid.NewGuid()); // job không seed
        var uow = new InMemoryUnitOfWork().Seed(app);

        var res = await Run(uow, app.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("Job posting associated", res.Error);
    }

    [Fact]
    public async Task Excludes_practice_evaluations()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(EvaluationData.Eval(app.Id, round: 1, type: "real"))
            .Seed(EvaluationData.Eval(app.Id, round: 1, type: "practice"));

        var res = await Run(uow, app.Id);

        Assert.Equal("real", Assert.Single(res.Value!).SessionType);
    }

    [Fact]
    public async Task Includes_hr_review_status()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var eval = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval)
            .Seed(EvaluationData.Review(eval.Id, finalVerdict: "pass"));

        var item = Assert.Single((await Run(uow, app.Id)).Value!);

        Assert.Equal("completed", item.Status);
        Assert.Equal("pass", item.FinalVerdict);
    }
}
