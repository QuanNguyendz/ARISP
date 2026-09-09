using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Queries;
using ARI.Application.Common;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.Screening;

/// <summary>
/// Wrapper CQRS đọc hồ sơ (UC-54): chi tiết (<see cref="GetApplicationByIdQueryHandler"/>) và danh
/// sách (<see cref="GetApplicationsQueryHandler"/>).
///
/// Phạm vi dữ liệu nay do SERVER quyết định theo vai trò, không phải do client khai qua
/// <c>?mine=true</c>: trước đây bỏ tham số đó đi là đọc được hồ sơ của toàn công ty.
/// </summary>
public class ApplicationQueryHandlerTests
{
    private static ApplicationService Svc(InMemoryUnitOfWork uow)
        => ApplicationServiceFactory.Create(
            uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    private static GetApplicationByIdQueryHandler ByIdHandler(InMemoryUnitOfWork uow)
        => new(Svc(uow), new RecordingFileStorage(), uow);

    private static GetApplicationsQueryHandler ListHandler(InMemoryUnitOfWork uow)
        => new(Svc(uow), new RecordingFileStorage(), uow);

    // ---------- GetApplicationByIdQueryHandler ----------

    [Fact]
    public async Task ById_missing_maps_to_not_found_code()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await ByIdHandler(uow).Handle(
            new GetApplicationByIdQuery(Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task ById_resolves_cv_file_url()
    {
        var owner = Guid.NewGuid();
        var job = ScreeningData.Job(owner: owner);
        var app = ScreeningData.App(job.Id, cvFileUrl: "cv/detail.pdf");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await ByIdHandler(uow).Handle(
            new GetApplicationByIdQuery(app.Id, owner, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("/files/cv/detail.pdf", res.Value!.CvFileUrl);
    }

    [Fact]
    public async Task ById_refuses_an_application_on_someone_elses_job()
    {
        // Đây là lỗ hổng thật trước ADR-061: endpoint chỉ gác bằng policy InternalStaff, nên bất kỳ
        // nhân sự nào biết id đều đọc được CV, số điện thoại và điểm phân tích của ứng viên
        // thuộc tin người khác.
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var app = ScreeningData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await ByIdHandler(uow).Handle(
            new GetApplicationByIdQuery(app.Id, Guid.NewGuid(), AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task ById_allows_an_admin_on_any_job()
    {
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var app = ScreeningData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await ByIdHandler(uow).Handle(
            new GetApplicationByIdQuery(app.Id, Guid.NewGuid(), AppRoles.HrAdmin), CancellationToken.None);

        Assert.True(res.IsSuccess);
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

        var res = await ListHandler(uow).Handle(
            new GetApplicationsQuery(creatorId, AppRoles.Recruiter, MineOnly: true), CancellationToken.None);

        Assert.Equal("Của tôi", Assert.Single(res.Value!).CandidateName);
    }

    [Fact]
    public async Task List_without_mine_still_scopes_a_recruiter_to_their_own_jobs()
    {
        // Bất biến quan trọng nhất của Phase 1: bỏ `?mine` KHÔNG còn mở ra dữ liệu công ty.
        var creatorId = Guid.NewGuid();
        var ownJob = ScreeningData.Job(owner: creatorId);
        var otherJob = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(ownJob, otherJob)
            .Seed(ScreeningData.App(ownJob.Id, name: "Của tôi"), ScreeningData.App(otherJob.Id, name: "Người khác"));

        var res = await ListHandler(uow).Handle(
            new GetApplicationsQuery(creatorId, AppRoles.Recruiter, MineOnly: false), CancellationToken.None);

        Assert.Equal("Của tôi", Assert.Single(res.Value!).CandidateName);
    }

    [Fact]
    public async Task List_returns_everything_for_an_admin()
    {
        var jobA = ScreeningData.Job(owner: Guid.NewGuid());
        var jobB = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(jobA, jobB)
            .Seed(ScreeningData.App(jobA.Id), ScreeningData.App(jobB.Id));

        var res = await ListHandler(uow).Handle(
            new GetApplicationsQuery(Guid.NewGuid(), AppRoles.HrAdmin, MineOnly: false), CancellationToken.None);

        Assert.Equal(2, res.Value!.Count);
    }

    [Fact]
    public async Task List_is_empty_for_a_hiring_manager_with_no_assigned_jobs()
    {
        // Vai trò mới không được kế thừa quyền đọc của InternalStaff: chưa được gán tin nào thì
        // không thấy hồ sơ nào.
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id));

        var res = await ListHandler(uow).Handle(
            new GetApplicationsQuery(Guid.NewGuid(), AppRoles.HiringManager, MineOnly: false), CancellationToken.None);

        Assert.Empty(res.Value!);
    }

    [Fact]
    public async Task List_resolves_cv_file_urls()
    {
        var job = ScreeningData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(ScreeningData.App(job.Id, cvFileUrl: "cv/list.pdf"));

        var res = await ListHandler(uow).Handle(
            new GetApplicationsQuery(Guid.NewGuid(), AppRoles.HrAdmin, MineOnly: false), CancellationToken.None);

        Assert.Equal("/files/cv/list.pdf", Assert.Single(res.Value!).CvFileUrl);
    }
}
