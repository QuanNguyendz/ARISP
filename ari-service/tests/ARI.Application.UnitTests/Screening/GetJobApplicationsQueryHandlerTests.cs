using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Jobs.Queries.GetJobApplications;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.Screening;

/// <summary>
/// Phân quyền + hoàn thiện URL cho danh sách ứng viên theo job (UC-53, <see cref="GetJobApplicationsQueryHandler"/>):
/// Recruiter chỉ xem tin của mình, HrAdmin/SuperAdmin xem mọi tin; CvFileUrl được resolve qua file storage.
/// </summary>
public class GetJobApplicationsQueryHandlerTests
{
    private static ApplicationService Svc(InMemoryUnitOfWork uow)
        => ApplicationServiceFactory.Create(
            uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    private static Task<Result<System.Collections.Generic.List<ARI.Application.DTOs.ApplicationResponse>>> Run(
        InMemoryUnitOfWork uow, RecordingFileStorage storage, Guid jobId, Guid userId, string? role)
        => new GetJobApplicationsQueryHandler(uow, Svc(uow), storage)
            .Handle(new GetJobApplicationsQuery(jobId, userId, role), CancellationToken.None);

    [Fact]
    public async Task Job_not_found_returns_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(), new RecordingFileStorage(), Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Recruiter_cannot_view_other_owners_job()
    {
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingFileStorage(), job.Id, Guid.NewGuid(), AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Recruiter_can_view_own_job()
    {
        var ownerId = Guid.NewGuid();
        var job = ScreeningData.Job(owner: ownerId);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id));

        var res = await Run(uow, new RecordingFileStorage(), job.Id, ownerId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Single(res.Value!);
    }

    [Fact]
    public async Task Hr_admin_can_view_any_job()
    {
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id));

        var res = await Run(uow, new RecordingFileStorage(), job.Id, Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Cv_file_url_is_resolved_via_storage()
    {
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id, cvFileUrl: "cv/profile.pdf"));

        var res = await Run(uow, new RecordingFileStorage(), job.Id, Guid.NewGuid(), AppRoles.SuperAdmin);

        Assert.Equal("/files/cv/profile.pdf", Assert.Single(res.Value!).CvFileUrl);
    }
}
