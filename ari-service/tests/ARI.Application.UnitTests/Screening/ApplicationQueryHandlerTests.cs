using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Queries;
using ARI.Application.Common;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.Screening;

/// <summary>
/// Wrapper CQRS đọc hồ sơ (UC-54): chi tiết (<see cref="GetApplicationByIdQueryHandler"/> — lỗi → mã NotFound,
/// resolve CvFileUrl) và danh sách (<see cref="GetApplicationsQueryHandler"/> — mine=creator vs toàn bộ, resolve URL).
/// </summary>
public class ApplicationQueryHandlerTests
{
    private static ApplicationService Svc(InMemoryUnitOfWork uow)
        => ApplicationServiceFactory.Create(
            uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    // ---------- GetApplicationByIdQueryHandler ----------

    [Fact]
    public async Task ById_missing_maps_to_not_found_code()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await new GetApplicationByIdQueryHandler(Svc(uow), new RecordingFileStorage())
            .Handle(new GetApplicationByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode); // handler gán mã NotFound cho lỗi service
    }

    [Fact]
    public async Task ById_resolves_cv_file_url()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, cvFileUrl: "cv/detail.pdf");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await new GetApplicationByIdQueryHandler(Svc(uow), new RecordingFileStorage())
            .Handle(new GetApplicationByIdQuery(app.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("/files/cv/detail.pdf", res.Value!.CvFileUrl);
    }

    // ---------- GetApplicationsQueryHandler ----------

    [Fact]
    public async Task List_mine_filters_to_creators_jobs()
    {
        var creatorId = Guid.NewGuid();
        var ownJob = ScreeningData.Job(owner: creatorId);
        var otherJob = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(ownJob, otherJob)
            .Seed(ScreeningData.App(ownJob.Id, name: "Của tôi"), ScreeningData.App(otherJob.Id, name: "Người khác"));

        var res = await new GetApplicationsQueryHandler(Svc(uow), new RecordingFileStorage())
            .Handle(new GetApplicationsQuery(creatorId), CancellationToken.None);

        Assert.Equal("Của tôi", Assert.Single(res.Value!).CandidateName);
    }

    [Fact]
    public async Task List_without_mine_returns_all()
    {
        var jobA = ScreeningData.Job(owner: Guid.NewGuid());
        var jobB = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(jobA, jobB)
            .Seed(ScreeningData.App(jobA.Id), ScreeningData.App(jobB.Id));

        var res = await new GetApplicationsQueryHandler(Svc(uow), new RecordingFileStorage())
            .Handle(new GetApplicationsQuery(null), CancellationToken.None);

        Assert.Equal(2, res.Value!.Count);
    }

    [Fact]
    public async Task List_resolves_cv_file_urls()
    {
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id, cvFileUrl: "cv/list.pdf"));

        var res = await new GetApplicationsQueryHandler(Svc(uow), new RecordingFileStorage())
            .Handle(new GetApplicationsQuery(null), CancellationToken.None);

        Assert.Equal("/files/cv/list.pdf", Assert.Single(res.Value!).CvFileUrl);
    }
}
