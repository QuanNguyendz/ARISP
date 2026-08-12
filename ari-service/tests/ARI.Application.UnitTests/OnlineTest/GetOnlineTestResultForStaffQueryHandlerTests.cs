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
/// Kết quả bài thi của MỘT ứng viên cho staff (<see cref="GetOnlineTestResultForStaffQueryHandler"/>, test-plan B7):
/// chưa thi → Success null (khác NotFound), có nhiều lượt → lấy lượt MỚI NHẤT, điểm sàn lấy từ job,
/// và phân quyền (hồ sơ lạ → NotFound, không phải chủ tin → Forbidden).
/// </summary>
public class GetOnlineTestResultForStaffQueryHandlerTests
{
    private static Task<Result<OnlineTestResultDto?>> Run(InMemoryUnitOfWork uow, GetOnlineTestResultForStaffQuery q)
        => new GetOnlineTestResultForStaffQueryHandler(uow).Handle(q, CancellationToken.None);

    [Fact]
    public async Task No_submission_returns_success_with_null_value()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(owner: owner);
        var app = OnlineTestData.Application(job.Id, accountId: null);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await Run(uow, new GetOnlineTestResultForStaffQuery(app.Id, owner, AppRoles.Recruiter));

        Assert.True(res.IsSuccess);   // chưa thi ≠ lỗi
        Assert.Null(res.Value);
    }

    [Fact]
    public async Task Latest_submission_is_returned_with_job_pass_score()
    {
        var owner = Guid.NewGuid();
        var job = OnlineTestData.Job(passScore: 70, owner: owner);
        var app = OnlineTestData.Application(job.Id, accountId: null);
        var old = OnlineTestData.Submission(app.Id, score: 40m, passed: false, correct: 4, total: 10,
            at: DateTimeOffset.UtcNow.AddDays(-1));
        var latest = OnlineTestData.Submission(app.Id, score: 88m, passed: true, correct: 22, total: 25,
            at: DateTimeOffset.UtcNow);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(old, latest);

        var res = await Run(uow, new GetOnlineTestResultForStaffQuery(app.Id, owner, AppRoles.HrAdmin));

        Assert.True(res.IsSuccess);
        Assert.NotNull(res.Value);
        Assert.Equal(88m, res.Value!.Score);       // lượt mới nhất
        Assert.True(res.Value.IsPassed);
        Assert.Equal(70, res.Value.PassScore);      // từ job, không phải từ submission
        Assert.Equal(22, res.Value.CorrectCount);
        Assert.Equal(25, res.Value.TotalQuestions);
    }

    [Fact]
    public async Task Unknown_application_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Run(uow, new GetOnlineTestResultForStaffQuery(Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden()
    {
        var job = OnlineTestData.Job(owner: Guid.NewGuid());
        var app = OnlineTestData.Application(job.Id, accountId: null);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(OnlineTestData.Submission(app.Id, score: 90m, passed: true));

        var res = await Run(uow, new GetOnlineTestResultForStaffQuery(app.Id, Guid.NewGuid(), AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }
}
