using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.UnitTests.JobBoard;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Việc làm đã lưu (<c>SavedJobsFeature.cs</c>): danh sách chỉ tin còn mở, danh sách id thô,
/// lưu/bỏ lưu idempotent — tất cả gắn với <see cref="SavedJob.CandidateAccountId"/>.
/// </summary>
public class GetSavedJobsQueryHandlerTests
{
    private static SavedJob Saved(Guid cand, Guid jobId, DateTimeOffset savedAt)
        => new() { CandidateAccountId = cand, JobPostingId = jobId, CreatedAt = savedAt };

    [Fact]
    public async Task Returns_only_active_public_jobs_ordered_by_saved_at_desc()
    {
        var cand = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var job1 = JobBoardData.PublicJob(title: "Job 1");
        var job2 = JobBoardData.PublicJob(title: "Job 2");
        var jobClosed = JobBoardData.PublicJob(title: "Đã đóng", status: "closed");
        var uow = new InMemoryUnitOfWork()
            .Seed(job1, job2, jobClosed)
            .Seed(Saved(cand, job1.Id, now.AddMinutes(-2)),
                  Saved(cand, job2.Id, now.AddMinutes(-1)),
                  Saved(cand, jobClosed.Id, now));  // tin đã đóng → bị loại dù lưu mới nhất

        var res = await new GetSavedJobsQueryHandler(uow)
            .Handle(new GetSavedJobsQuery(cand), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Count);
        Assert.Equal("Job 2", res.Value[0].Title);   // lưu muộn hơn → lên đầu
        Assert.Equal("Job 1", res.Value[1].Title);
    }

    [Fact]
    public async Task Empty_when_no_saved_jobs()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new GetSavedJobsQueryHandler(uow)
            .Handle(new GetSavedJobsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }
}

/// <summary>Danh sách id tin đã lưu (<see cref="GetSavedJobIdsQueryHandler"/>): distinct, chỉ của ứng viên, không lọc trạng thái tin.</summary>
public class GetSavedJobIdsQueryHandlerTests
{
    [Fact]
    public async Task Returns_distinct_ids_of_candidate_only()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var jobA = Guid.NewGuid();
        var jobB = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(
            new SavedJob { CandidateAccountId = me, JobPostingId = jobA },
            new SavedJob { CandidateAccountId = me, JobPostingId = jobB },
            new SavedJob { CandidateAccountId = other, JobPostingId = jobA });

        var res = await new GetSavedJobIdsQueryHandler(uow)
            .Handle(new GetSavedJobIdsQuery(me), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Count);
        Assert.Contains(jobA, res.Value);
        Assert.Contains(jobB, res.Value);
    }

    [Fact]
    public async Task Empty_when_none_saved()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new GetSavedJobIdsQueryHandler(uow)
            .Handle(new GetSavedJobIdsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(res.Value);
    }
}

/// <summary>Lưu tin (<see cref="SaveJobCommandHandler"/>): idempotent + chặn tin không tồn tại.</summary>
public class SaveJobCommandHandlerTests
{
    [Fact]
    public async Task Saves_new_bookmark()
    {
        var cand = Guid.NewGuid();
        var job = JobBoardData.PublicJob();
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await new SaveJobCommandHandler(uow)
            .Handle(new SaveJobCommand(cand, job.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var saved = Assert.Single(uow.Repo<SavedJob>().Items);
        Assert.Equal(job.Id, saved.JobPostingId);
        Assert.Equal(cand, saved.CandidateAccountId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Idempotent_when_already_saved()
    {
        var cand = Guid.NewGuid();
        var job = JobBoardData.PublicJob();
        var uow = new InMemoryUnitOfWork()
            .Seed(job)
            .Seed(new SavedJob { CandidateAccountId = cand, JobPostingId = job.Id });

        var res = await new SaveJobCommandHandler(uow)
            .Handle(new SaveJobCommand(cand, job.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<SavedJob>().Items);   // không thêm bản trùng
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Unknown_job_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new SaveJobCommandHandler(uow)
            .Handle(new SaveJobCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("Không tìm thấy tin tuyển dụng", res.Error);
        Assert.Empty(uow.Repo<SavedJob>().Items);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}

/// <summary>Bỏ lưu tin (<see cref="UnsaveJobCommandHandler"/>): idempotent — xóa nếu có, không thì vẫn success.</summary>
public class UnsaveJobCommandHandlerTests
{
    [Fact]
    public async Task Removes_existing_bookmark()
    {
        var cand = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(new SavedJob { CandidateAccountId = cand, JobPostingId = jobId });

        var res = await new UnsaveJobCommandHandler(uow)
            .Handle(new UnsaveJobCommand(cand, jobId), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<SavedJob>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Idempotent_when_not_saved()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new UnsaveJobCommandHandler(uow)
            .Handle(new UnsaveJobCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}
