using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Queries.GetJobApplications;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.Screening;

/// <summary>
/// Danh sách ứng viên theo job (<see cref="GetJobApplicationsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetJobApplications" (UTCID01–08): tồn tại tin, phân quyền chủ tin/HrAdmin, forward failure của service,
/// list rỗng, resolve CvFileUrl (chỉ khi có giá trị), và lỗi file storage.
/// </summary>
public class GetJobApplicationsQueryHandlerTests
{
    private static ApplicationService Svc(InMemoryUnitOfWork uow)
        => ApplicationServiceFactory.Create(uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    private static Task<Result<List<ApplicationResponse>>> Run(
        InMemoryUnitOfWork uow, RecordingFileStorage storage, Guid jobId, Guid userId, string role, InMemoryUnitOfWork? svcUow = null)
        => new GetJobApplicationsQueryHandler(uow, Svc(svcUow ?? uow), storage)
            .Handle(new GetJobApplicationsQuery(jobId, userId, role), CancellationToken.None);

    // UTCID01 — job không tồn tại → not_found
    [Fact]
    public async Task UTCID01_Job_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(), new RecordingFileStorage(), Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tin tuyển dụng.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID02 — recruiter không phải chủ tin → forbidden
    [Fact]
    public async Task UTCID02_Recruiter_not_owner()
    {
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);
        var res = await Run(uow, new RecordingFileStorage(), job.Id, Guid.NewGuid(), AppRoles.Recruiter);
        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không có quyền xem ứng viên của tin tuyển dụng này.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // UTCID03 — chủ tin xem → Success
    [Fact]
    public async Task UTCID03_Owner_gets_applications()
    {
        var owner = Guid.NewGuid();
        var job = ScreeningData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id));
        var res = await Run(uow, new RecordingFileStorage(), job.Id, owner, AppRoles.Recruiter);
        Assert.True(res.IsSuccess);
        Assert.Single(res.Value);
    }

    // UTCID04 — HrAdmin xem tin của người khác → Success
    [Fact]
    public async Task UTCID04_HrAdmin_any_job()
    {
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id));
        var res = await Run(uow, new RecordingFileStorage(), job.Id, Guid.NewGuid(), AppRoles.HrAdmin);
        Assert.True(res.IsSuccess);
        Assert.Single(res.Value);
    }

    // UTCID05 — ApplicationService trả failure → forward nguyên lỗi
    // (dựng service trên UoW rỗng: handler thấy job (qua uow chính) nhưng service không thấy → trả failure)
    [Fact]
    public async Task UTCID05_Service_failure_forwarded()
    {
        var owner = Guid.NewGuid();
        var job = ScreeningData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingFileStorage(), job.Id, owner, AppRoles.Recruiter, svcUow: new InMemoryUnitOfWork());

        Assert.True(res.IsFailure);
        Assert.Equal("Job posting not found.", res.Error);   // đúng lỗi service trả về
    }

    // UTCID06 — list ứng viên rỗng → Success([])
    [Fact]
    public async Task UTCID06_Empty_list()
    {
        var owner = Guid.NewGuid();
        var job = ScreeningData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);
        var res = await Run(uow, new RecordingFileStorage(), job.Id, owner, AppRoles.Recruiter);
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    // UTCID07 — CvFileUrl có giá trị → resolve; null giữ nguyên
    [Fact]
    public async Task UTCID07_Resolves_cv_urls()
    {
        var owner = Guid.NewGuid();
        var job = ScreeningData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(ScreeningData.App(job.Id, cvFileUrl: "cv/a.pdf"))
            .Seed(ScreeningData.App(job.Id, cvFileUrl: null));

        var res = await Run(uow, new RecordingFileStorage(), job.Id, owner, AppRoles.SuperAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.Count(x => x.CvFileUrl == "/files/cv/a.pdf"));
        Assert.Contains(res.Value, x => x.CvFileUrl == null);
    }

    // UTCID08 — file storage GetUrlAsync ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID08_Storage_url_throws()
    {
        var owner = Guid.NewGuid();
        var job = ScreeningData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id, cvFileUrl: "cv/a.pdf"));
        var storage = new RecordingFileStorage { GetUrlThrows = new Exception("Storage URL Error") };

        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, storage, job.Id, owner, AppRoles.SuperAdmin));
        Assert.Equal("Storage URL Error", ex.Message);
    }
}
