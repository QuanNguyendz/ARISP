using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Queries.GetJobById;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>
/// Chi tiết tin (UC-18 View Job Details, <see cref="GetJobByIdQueryHandler"/>): khách chỉ xem tin active+public;
/// staff xem cả draft (Recruiter chỉ với tin của chính mình); resolve URL file JD cho staff; kèm vòng + tên
/// người tạo.
/// </summary>
public class GetJobByIdQueryHandlerTests
{
    private static Task<Result<JobPostingResponse>> Run(
        InMemoryUnitOfWork uow, RecordingFileStorage storage, Guid id, bool isStaff, Guid? userId = null, string? role = null)
        => new GetJobByIdQueryHandler(uow, storage).Handle(new GetJobByIdQuery(id, isStaff, userId, role), CancellationToken.None);

    [Fact]
    public async Task Not_found_fails()
    {
        var res = await Run(new InMemoryUnitOfWork(), new RecordingFileStorage(), Guid.NewGuid(), isStaff: false);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Public_active_job_is_visible_to_anonymous()
    {
        var job = JobBoardData.PublicJob();
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingFileStorage(), job.Id, isStaff: false);

        Assert.True(res.IsSuccess);
        Assert.Equal(job.Id, res.Value.Id);
    }

    [Fact]
    public async Task Non_public_job_is_hidden_from_anonymous()
    {
        var job = JobBoardData.PublicJob(isPublic: false);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingFileStorage(), job.Id, isStaff: false);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Draft_job_is_hidden_from_anonymous()
    {
        var job = JobBoardData.PublicJob(status: "draft");
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingFileStorage(), job.Id, isStaff: false);

        Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task Staff_can_view_draft_job()
    {
        var job = JobBoardData.PublicJob(status: "draft", isPublic: false);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingFileStorage(), job.Id, isStaff: true, userId: Guid.NewGuid(), role: "hr_admin");

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Recruiter_sees_own_draft_but_not_others()
    {
        var ownerId = Guid.NewGuid();
        var ownJob = JobBoardData.PublicJob(status: "draft", isPublic: false, owner: ownerId);
        var otherJob = JobBoardData.PublicJob(status: "draft", isPublic: false, owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(ownJob).Seed(otherJob);

        var own = await Run(uow, new RecordingFileStorage(), ownJob.Id, isStaff: true, userId: ownerId, role: "recruiter");
        var other = await Run(uow, new RecordingFileStorage(), otherJob.Id, isStaff: true, userId: ownerId, role: "recruiter");

        Assert.True(own.IsSuccess);
        Assert.True(other.IsFailure); // tin của recruiter khác → coi như khách → draft bị ẩn
    }

    [Fact]
    public async Task Staff_gets_resolved_jd_file_url()
    {
        var job = JobBoardData.PublicJob();
        job.JdFileUrl = "jd/original.pdf";
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, new RecordingFileStorage(), job.Id, isStaff: true, userId: Guid.NewGuid(), role: "hr_admin");

        Assert.Equal("/files/jd/original.pdf", res.Value.JdFileUrl); // đã resolve qua GetUrlAsync
    }

    [Fact]
    public async Task Creator_name_and_rounds_are_included()
    {
        var creator = JobBoardData.Staff(Guid.NewGuid(), "Anna", "hr_admin");
        var job = JobBoardData.PublicJob(owner: creator.Id);
        var uow = new InMemoryUnitOfWork().Seed(creator).Seed(job)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 2 })
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1 });

        var res = await Run(uow, new RecordingFileStorage(), job.Id, isStaff: false);

        Assert.Equal("Anna", res.Value.CreatedByName);
        Assert.Equal(2, res.Value.RoundConfigs.Count);
        Assert.Equal(1, res.Value.RoundConfigs[0].RoundNumber); // sắp theo RoundNumber
    }
}
