using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.UpdateJob;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Sửa tin tuyển dụng (UC-50, <see cref="UpdateJobCommandHandler"/>): chỉ chủ tin hoặc HrAdmin/SuperAdmin,
/// chặn khi đã archived, validate như tạo mới, chỉ tái tạo InterviewRoundConfig khi cấu hình vòng đổi,
/// và phát broadcast công khai khi tin đang active.
/// </summary>
public class UpdateJobCommandHandlerTests
{
    private static Task<Result<JobPostingResponse>> Run(
        InMemoryUnitOfWork uow, RecordingNotificationService notif, Guid id, CreateJobPostingRequest req, Guid userId, string? role)
        => new UpdateJobCommandHandler(uow, notif)
            .Handle(new UpdateJobCommand(id, req, userId, role), CancellationToken.None);

    [Fact]
    public async Task Job_not_found_fails()
    {
        var res = await Run(new InMemoryUnitOfWork(), new RecordingNotificationService(),
            Guid.NewGuid(), JobPostingData.Request(), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Non_owner_non_admin_is_forbidden()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingNotificationService(), job.Id, JobPostingData.Request(), Guid.NewGuid(), AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Owner_can_update()
    {
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId);
        var uow = new InMemoryUnitOfWork().Seed(job);
        var req = JobPostingData.Request(title: "Senior Backend Developer");

        var res = await Run(uow, new RecordingNotificationService(), job.Id, req, userId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("Senior Backend Developer", job.Title);
    }

    [Fact]
    public async Task Admin_can_update_others_job()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingNotificationService(), job.Id, JobPostingData.Request(), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Archived_job_cannot_be_updated()
    {
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "archived");
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingNotificationService(), job.Id, JobPostingData.Request(), userId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Contains("đã lưu trữ", res.Error);
    }

    [Fact]
    public async Task Invalid_request_fails_validation()
    {
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId);
        var uow = new InMemoryUnitOfWork().Seed(job);
        var req = JobPostingData.Request();
        req.Title = "";

        var res = await Run(uow, new RecordingNotificationService(), job.Id, req, userId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Contains("Title is required", res.Error);
    }

    [Fact]
    public async Task Rounds_are_recreated_when_count_changes()
    {
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId);
        var oldRound = JobPostingData.RoundEntity(job.Id, number: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldRound);
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, "screening"), JobPostingData.Round(2, "technical") };

        var res = await Run(uow, new RecordingNotificationService(), job.Id, req, userId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        var rounds = uow.Repo<InterviewRoundConfig>().Items;
        Assert.Equal(2, rounds.Count);
        Assert.DoesNotContain(rounds, r => r.Id == oldRound.Id); // vòng cũ bị xoá, tạo mới
    }

    [Fact]
    public async Task Unchanged_rounds_are_preserved()
    {
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId);
        var oldRound = JobPostingData.RoundEntity(job.Id, number: 1, type: "screening", language: "vi", codeTtl: 2, maxMinutes: 45);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldRound);
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, "screening", language: "vi", codeTtl: 2, maxMinutes: 45) };

        var res = await Run(uow, new RecordingNotificationService(), job.Id, req, userId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        var round = Assert.Single(uow.Repo<InterviewRoundConfig>().Items);
        Assert.Equal(oldRound.Id, round.Id); // giữ nguyên entity, không tái tạo
    }

    [Fact]
    public async Task Active_job_update_broadcasts_public_update()
    {
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "active");
        var uow = new InMemoryUnitOfWork().Seed(job);
        var notif = new RecordingNotificationService();

        var res = await Run(uow, notif, job.Id, JobPostingData.Request(), userId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Contains("ReceivePublicJobUpdate", notif.AllEvents);
    }

    [Fact]
    public async Task Notifies_updater()
    {
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId);
        var uow = new InMemoryUnitOfWork().Seed(job);
        var notif = new RecordingNotificationService();

        await Run(uow, notif, job.Id, JobPostingData.Request(), userId, AppRoles.Recruiter);

        Assert.Contains(notif.UserEvents, e => e.UserId == userId && e.EventType == "ReceiveJobPostingUpdate");
    }
}
