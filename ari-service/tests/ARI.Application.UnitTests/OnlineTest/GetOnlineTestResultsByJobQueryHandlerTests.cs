using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.OnlineTest;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Bảng tổng hợp điểm theo job cho staff (<see cref="GetOnlineTestResultsByJobQueryHandler"/>, test-plan B8):
/// phân quyền chủ tin, thống kê (đậu/rớt/trung bình/cao/thấp) + số câu ngân hàng, và sắp xếp điểm giảm dần.
/// </summary>
public class GetOnlineTestResultsByJobQueryHandlerTests
{
    private static Task<Result<OnlineTestJobResultsDto>> Run(InMemoryUnitOfWork uow, GetOnlineTestResultsByJobQuery q)
        => new GetOnlineTestResultsByJobQueryHandler(uow).Handle(q, CancellationToken.None);

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new GetOnlineTestResultsByJobQuery(job.Id, Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("không có quyền xem điểm", res.Error);
    }

    [Fact]
    public async Task Aggregates_stats_and_sorts_rows_by_score_desc()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(passScore: 70, owner: owner);
        var app1 = OnlineTestData.Application(job.Id, accountId: null, email: "a@x.io");
        var app2 = OnlineTestData.Application(job.Id, accountId: null, email: "b@x.io");
        var app3 = OnlineTestData.Application(job.Id, accountId: null, email: "c@x.io");
        var uow = new InMemoryUnitOfWork()
            .Seed(job)
            .Seed(OnlineTestData.Single(job.Id, 0), OnlineTestData.Single(job.Id, 1), OnlineTestData.Single(job.Id, 2)) // 3 câu ngân hàng
            .Seed(app1, app2, app3)
            .Seed(OnlineTestData.Submission(app1.Id, score: 90m, passed: true),
                  OnlineTestData.Submission(app2.Id, score: 50m, passed: false),
                  OnlineTestData.Submission(app3.Id, score: 70m, passed: true));

        var res = await Run(uow, new GetOnlineTestResultsByJobQuery(job.Id, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        var dto = res.Value;
        Assert.Equal(3, dto.TotalQuestions);   // đếm ngân hàng
        Assert.Equal(3, dto.SubmissionCount);
        Assert.Equal(2, dto.PassedCount);       // 90, 70
        Assert.Equal(1, dto.NotPassedCount);    // 50
        Assert.Equal(70.0m, dto.AverageScore);  // (90+50+70)/3
        Assert.Equal(90m, dto.HighestScore);
        Assert.Equal(50m, dto.LowestScore);
        Assert.Equal(3, dto.Rows.Count);
        Assert.Equal(90m, dto.Rows[0].Score);   // sort điểm giảm dần
        Assert.Equal(70m, dto.Rows[1].Score);
        Assert.Equal(50m, dto.Rows[2].Score);
    }

    [Fact]
    public async Task No_submissions_yields_zero_stats_but_keeps_bank_count()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var app1 = OnlineTestData.Application(job.Id, accountId: null, email: "a@x.io");
        var app2 = OnlineTestData.Application(job.Id, accountId: null, email: "b@x.io");
        var uow = new InMemoryUnitOfWork()
            .Seed(job)
            .Seed(OnlineTestData.Single(job.Id, 0), OnlineTestData.Single(job.Id, 1)) // 2 câu ngân hàng
            .Seed(app1, app2);

        var res = await Run(uow, new GetOnlineTestResultsByJobQuery(job.Id, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);
        var dto = res.Value;
        Assert.Equal(2, dto.TotalQuestions);
        Assert.Equal(0, dto.SubmissionCount);
        Assert.Equal(0, dto.PassedCount);
        Assert.Equal(0, dto.NotPassedCount);
        Assert.Equal(0m, dto.AverageScore);
        Assert.Equal(0m, dto.HighestScore);
        Assert.Equal(0m, dto.LowestScore);
        Assert.Empty(dto.Rows);
    }
}
