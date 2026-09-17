using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Commands;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Loại hồ sơ (<see cref="RejectApplicationCommandHandler"/>, ADR-061): là quyết định huỷ + gửi thư cho ứng viên,
/// nên cổng quyền quản lý tin (Owner) chạy TRƯỚC service; qua cổng rồi mới forward xuống <c>IApplicationService</c>
/// (kèm bản thư sửa tay + danh tính người bấm).
/// </summary>
public class RejectApplicationCommandHandlerTests
{
    private static readonly Guid AppId = Guid.NewGuid();
    private static readonly Guid JobId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static InMemoryUnitOfWork Seeded()
        => new InMemoryUnitOfWork()
            .Seed(new JobPosting { Id = JobId, CreatedByUserId = OwnerId, Title = "Backend Developer" })
            .Seed(new ARI.Domain.Entities.Application { Id = AppId, JobPostingId = JobId, Status = "cv_submitted" });

    private static RejectApplicationCommandHandler Handler(StubApplicationService svc, InMemoryUnitOfWork uow) => new(svc, uow);

    [Fact]
    public async Task UTCID01_Application_not_found_service_not_called()
    {
        var svc = new StubApplicationService();

        var res = await Handler(svc, new InMemoryUnitOfWork())
            .Handle(new RejectApplicationCommand(AppId, OwnerId, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Null(svc.LastRejectId);
    }

    [Fact]
    public async Task UTCID02_Team_member_forbidden_service_not_called()
    {
        var hmId = Guid.NewGuid();
        var uow = Seeded().Seed(new JobHiringTeamMember
        {
            JobPostingId = JobId, UserId = hmId, RoleOnJob = JobTeamRoles.HiringManager, IsPrimary = true,
        });
        var svc = new StubApplicationService();

        var res = await Handler(svc, uow)
            .Handle(new RejectApplicationCommand(AppId, hmId, AppRoles.HiringManager), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Null(svc.LastRejectId);
    }

    [Fact]
    public async Task UTCID03_Owner_forwards_id_actor_and_override()
    {
        var svc = new StubApplicationService { RejectResult = Result.Success(true) };
        var over = new ARI.Application.Emails.EmailOverride { Subject = "Cảm ơn bạn đã ứng tuyển" };

        var res = await Handler(svc, Seeded())
            .Handle(new RejectApplicationCommand(AppId, OwnerId, AppRoles.Recruiter, over), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(AppId, svc.LastRejectId);
        Assert.Equal(OwnerId, svc.LastRejectActor);
        Assert.Same(over, svc.LastRejectOverride);
    }

    [Fact]
    public async Task UTCID04_Service_failure_passthrough()
    {
        var svc = new StubApplicationService { RejectResult = Result<bool>.Failure("Không thể loại hồ sơ ở trạng thái này.") };

        var res = await Handler(svc, Seeded())
            .Handle(new RejectApplicationCommand(AppId, OwnerId, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Không thể loại hồ sơ ở trạng thái này.", res.Error);
    }

    [Fact]
    public async Task UTCID05_Service_throws_propagates()
    {
        var svc = new StubApplicationService { RejectThrows = new Exception("Service Error") };

        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(svc, Seeded())
            .Handle(new RejectApplicationCommand(AppId, OwnerId, AppRoles.Recruiter), CancellationToken.None));
        Assert.Equal("Service Error", ex.Message);
    }
}
