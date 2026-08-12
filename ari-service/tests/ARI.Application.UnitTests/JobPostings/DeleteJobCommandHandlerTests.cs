using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Jobs.Commands.DeleteJob;
using ARI.Application.UnitTests.JobBoard;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Xóa mềm tin tuyển dụng (<see cref="DeleteJobCommandHandler"/>): chặn tin lạ/không quyền,
/// chặn khi còn hồ sơ đang hoạt động, chủ tin/admin xóa mềm (archived) + báo realtime.
/// </summary>
public class DeleteJobCommandHandlerTests
{
    private static DeleteJobCommandHandler Handler(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => new(uow, notif);

    private static ARI.Domain.Entities.Application App(Guid jobId, string status)
        => new() { JobPostingId = jobId, CandidateName = "N", CandidateEmail = "c@x.io", Status = status };

    [Fact]
    public async Task Unknown_job_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new DeleteJobCommand(Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy tin tuyển dụng", res.Error);
    }

    [Fact]
    public async Task Non_owner_recruiter_is_forbidden()
    {
        var owner = Guid.NewGuid();
        var job = JobBoardData.PublicJob(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new DeleteJobCommand(job.Id, Guid.NewGuid(), AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không có quyền", res.Error);
        Assert.Null(job.DeletedAt);   // không đụng job
    }

    [Fact]
    public async Task Active_applications_block_delete()
    {
        var owner = Guid.NewGuid();
        var job = JobBoardData.PublicJob(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(App(job.Id, "cv_submitted"));

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new DeleteJobCommand(job.Id, owner, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đang có hồ sơ ứng tuyển đang hoạt động", res.Error);
        Assert.Null(job.DeletedAt);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Owner_soft_deletes_when_only_terminal_applications()
    {
        var owner = Guid.NewGuid();
        var job = JobBoardData.PublicJob(owner: owner);
        // Hồ sơ đã kết thúc (not_pass/withdrawn/pass) không chặn xóa.
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(App(job.Id, "not_pass"), App(job.Id, "withdrawn"), App(job.Id, "pass"));
        var notif = new RecordingNotificationService();

        var res = await Handler(uow, notif)
            .Handle(new DeleteJobCommand(job.Id, owner, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(job.DeletedAt);
        Assert.Equal("archived", job.Status);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Contains(notif.UserEvents, e => e.UserId == owner && e.EventType == "ReceiveJobPostingUpdate");
        Assert.Contains("ReceivePublicJobUpdate", notif.AllEvents);
    }

    [Fact]
    public async Task Admin_can_delete_any_job()
    {
        var job = JobBoardData.PublicJob(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new DeleteJobCommand(job.Id, Guid.NewGuid(), AppRoles.HrAdmin), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(job.DeletedAt);
    }
}
