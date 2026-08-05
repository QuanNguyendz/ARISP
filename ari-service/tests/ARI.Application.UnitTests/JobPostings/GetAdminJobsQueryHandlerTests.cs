using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Queries.GetAdminJobs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Danh sách tin cho HR (UC-77 View Pending Jobs, <see cref="GetAdminJobsQueryHandler"/>): trả mọi trạng thái
/// (gồm draft/pending), lọc theo người tạo khi có MineUserId, đếm ứng viên + gắn tên người tạo, sắp mới nhất.
/// </summary>
public class GetAdminJobsQueryHandlerTests
{
    private static Task<Result<List<JobPostingListItemResponse>>> Run(InMemoryUnitOfWork uow, Guid? mine)
        => new GetAdminJobsQueryHandler(uow).Handle(new GetAdminJobsQuery(mine), CancellationToken.None);

    [Fact]
    public async Task Returns_all_jobs_for_admin()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobPostingData.Job(Guid.NewGuid()), JobPostingData.Job(Guid.NewGuid()), JobPostingData.Job(Guid.NewGuid()));

        var res = await Run(uow, null);

        Assert.True(res.IsSuccess);
        Assert.Equal(3, res.Value.Count);
    }

    [Fact]
    public async Task Mine_filter_returns_only_own_jobs()
    {
        var mine = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(
            JobPostingData.Job(mine), JobPostingData.Job(mine), JobPostingData.Job(Guid.NewGuid()));

        var res = await Run(uow, mine);

        Assert.Equal(2, res.Value.Count);
        Assert.All(res.Value, j => Assert.Equal(mine, j.CreatedByUserId));
    }

    [Fact]
    public async Task Includes_pending_and_draft_statuses()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobPostingData.Job(Guid.NewGuid(), status: "pending"),
            JobPostingData.Job(Guid.NewGuid(), status: "draft"),
            JobPostingData.Job(Guid.NewGuid(), status: "active"));

        var res = await Run(uow, null);

        Assert.Contains(res.Value, j => j.Status == "pending");
        Assert.Contains(res.Value, j => j.Status == "draft");
    }

    [Fact]
    public async Task Applicant_count_is_populated()
    {
        var job = JobPostingData.Job(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(new ARI.Domain.Entities.Application { JobPostingId = job.Id, CandidateEmail = "a@x.io" })
            .Seed(new ARI.Domain.Entities.Application { JobPostingId = job.Id, CandidateEmail = "b@x.io" });

        var res = await Run(uow, null);

        Assert.Equal(2, Assert.Single(res.Value).ApplicantCount);
    }

    [Fact]
    public async Task Creator_name_and_role_are_resolved()
    {
        var creator = JobPostingData.Staff(Guid.NewGuid(), role: "recruiter");
        creator.FullName = "Anna";
        var job = JobPostingData.Job(creator.Id);
        var uow = new InMemoryUnitOfWork().Seed(creator).Seed(job);

        var res = await Run(uow, null);

        Assert.Equal("Anna (Recruiter)", Assert.Single(res.Value).CreatedByName);
    }

    [Fact]
    public async Task Ordered_by_created_at_descending()
    {
        var older = JobPostingData.Job(Guid.NewGuid());
        older.Title = "Older";
        older.CreatedAt = DateTimeOffset.UtcNow.AddDays(-5);
        var newer = JobPostingData.Job(Guid.NewGuid());
        newer.Title = "Newer";
        newer.CreatedAt = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork().Seed(older, newer);

        var res = await Run(uow, null);

        Assert.Equal("Newer", res.Value.First().Title);
    }

    [Fact]
    public async Task No_jobs_returns_empty_list()
    {
        var res = await Run(new InMemoryUnitOfWork(), null);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }
}
