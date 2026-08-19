using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Jobs.Commands.DeleteJob;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Xóa mềm tin tuyển dụng (<see cref="DeleteJobCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "DeleteJob" (UTCID01–07): tồn tại, phân quyền chủ tin/HrAdmin/SuperAdmin, chặn khi còn hồ sơ hoạt động,
/// xóa mềm (archived + DeletedAt) cho owner/HrAdmin/SuperAdmin, và lỗi save.
/// </summary>
public class DeleteJobCommandHandlerTests
{
    private static DeleteJobCommandHandler Handler(InMemoryUnitOfWork uow, RecordingNotificationService notif) => new(uow, notif);

    private static ARI.Domain.Entities.Application App(Guid jobId, string status)
        => new() { JobPostingId = jobId, CandidateName = "N", CandidateEmail = "c@x.io", Status = status };

    // UTCID01 — job không tồn tại → not_found
    [Fact]
    public async Task UTCID01_Job_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new RecordingNotificationService())
            .Handle(new DeleteJobCommand(Guid.NewGuid(), Guid.NewGuid(), AppRoles.Recruiter), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tin tuyển dụng.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID02 — không phải owner/HrAdmin/SuperAdmin → forbidden
    [Fact]
    public async Task UTCID02_Unauthorized()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new DeleteJobCommand(job.Id, Guid.NewGuid(), AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không có quyền xóa tin tuyển dụng này.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Null(job.DeletedAt);
    }

    // UTCID03 — có quyền nhưng còn hồ sơ đang hoạt động → chặn
    [Fact]
    public async Task UTCID03_Active_applications_block()
    {
        var owner = Guid.NewGuid();
        var job = JobPostingData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(App(job.Id, "cv_submitted"));

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new DeleteJobCommand(job.Id, owner, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("active applications", res.Error);
        Assert.Null(job.DeletedAt);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID04 — chủ tin xóa, không có hồ sơ hoạt động → Success, archived + DeletedAt
    [Fact]
    public async Task UTCID04_Owner_soft_deletes()
    {
        var owner = Guid.NewGuid();
        var job = JobPostingData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job);
        var notif = new RecordingNotificationService();

        var res = await Handler(uow, notif).Handle(new DeleteJobCommand(job.Id, owner, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(job.DeletedAt);
        Assert.Equal("archived", job.Status);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Contains(notif.UserEvents, e => e.UserId == owner && e.EventType == "ReceiveJobPostingUpdate");
    }

    // UTCID05 — HrAdmin xóa tin chỉ còn hồ sơ kết thúc (not_pass/withdrawn/pass) → Success
    [Fact]
    public async Task UTCID05_HrAdmin_deletes_terminal_only()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(App(job.Id, "not_pass"), App(job.Id, "withdrawn"), App(job.Id, "pass"));

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new DeleteJobCommand(job.Id, Guid.NewGuid(), AppRoles.HrAdmin), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(job.DeletedAt);
        Assert.Equal("archived", job.Status);
    }

    // UTCID06 — SuperAdmin xóa, không có hồ sơ hoạt động → Success
    [Fact]
    public async Task UTCID06_SuperAdmin_deletes()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new DeleteJobCommand(job.Id, Guid.NewGuid(), AppRoles.SuperAdmin), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(job.DeletedAt);
        Assert.Equal("archived", job.Status);
    }

    // UTCID07 — SaveChangesAsync ném lỗi
    [Fact]
    public async Task UTCID07_Save_error()
    {
        var owner = Guid.NewGuid();
        var job = JobPostingData.Job(owner: owner);
        var uow = new InMemoryUnitOfWork().Seed(job).FailSaveOn(1, "Save Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingNotificationService()).Handle(new DeleteJobCommand(job.Id, owner, AppRoles.Recruiter), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }
}
