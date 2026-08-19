using System;
using System.Collections.Generic;
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
/// Danh sách tin cho HR (<see cref="GetAdminJobsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetAdminJobs" (UTCID01–07): trả mọi tin / lọc theo người tạo, rỗng, sắp CreatedAt giảm dần,
/// đếm ứng viên, resolve tên người tạo (fallback email / null) + Skills null → rỗng, và lỗi repo.
/// </summary>
public class GetAdminJobsQueryHandlerTests
{
    private static Task<Result<List<JobPostingListItemResponse>>> Run(InMemoryUnitOfWork uow, Guid? mine)
        => new GetAdminJobsQueryHandler(uow).Handle(new GetAdminJobsQuery(mine), CancellationToken.None);

    // UTCID01 — MineUserId=null, nhiều người tạo → trả tất cả, sắp mới nhất
    [Fact]
    public async Task UTCID01_All_creators()
    {
        var now = DateTimeOffset.UtcNow;
        var a = JobPostingData.Job(Guid.NewGuid()); a.CreatedAt = now;
        var b = JobPostingData.Job(Guid.NewGuid()); b.CreatedAt = now.AddMinutes(-1);
        var uow = new InMemoryUnitOfWork().Seed(a, b);

        var res = await Run(uow, null);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Count);
        Assert.Equal(a.Id, res.Value[0].Id);
    }

    // UTCID02 — MineUserId=UserA → chỉ tin của UserA
    [Fact]
    public async Task UTCID02_Mine_filter()
    {
        var mine = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Job(mine), JobPostingData.Job(mine), JobPostingData.Job(Guid.NewGuid()));

        var res = await Run(uow, mine);

        Assert.Equal(2, res.Value.Count);
        Assert.All(res.Value, j => Assert.Equal(mine, j.CreatedByUserId));
    }

    // UTCID03 — không có tin → []
    [Fact]
    public async Task UTCID03_Empty()
    {
        var res = await Run(new InMemoryUnitOfWork(), null);
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    // UTCID04 — CreatedAt khác nhau → sắp giảm dần
    [Fact]
    public async Task UTCID04_Ordered_desc()
    {
        var older = JobPostingData.Job(Guid.NewGuid()); older.Title = "Older"; older.CreatedAt = DateTimeOffset.UtcNow.AddDays(-5);
        var newer = JobPostingData.Job(Guid.NewGuid()); newer.Title = "Newer"; newer.CreatedAt = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork().Seed(older, newer);

        var res = await Run(uow, null);

        Assert.Equal("Newer", res.Value.First().Title);
    }

    // UTCID05 — có ứng viên ở một số tin → ApplicantCount theo nhóm, tin không có = 0
    [Fact]
    public async Task UTCID05_Applicant_count()
    {
        var withApps = JobPostingData.Job(Guid.NewGuid());
        var without = JobPostingData.Job(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(withApps, without)
            .Seed(new ARI.Domain.Entities.Application { JobPostingId = withApps.Id, CandidateEmail = "a@x.io" },
                  new ARI.Domain.Entities.Application { JobPostingId = withApps.Id, CandidateEmail = "b@x.io" });

        var res = await Run(uow, null);

        Assert.Equal(2, res.Value.Single(j => j.Id == withApps.Id).ApplicantCount);
        Assert.Equal(0, res.Value.Single(j => j.Id == without.Id).ApplicantCount);
    }

    // UTCID06 — FullName trống → fallback email; người tạo không tồn tại → CreatedByName=null; Skills null → []
    [Fact]
    public async Task UTCID06_Creator_name_and_skills()
    {
        var creator = JobPostingData.Staff(Guid.NewGuid(), role: "recruiter"); creator.FullName = "";
        var jobWithCreator = JobPostingData.Job(creator.Id); jobWithCreator.Skills = null;
        var jobNoCreator = JobPostingData.Job(Guid.NewGuid()); jobNoCreator.Skills = null;
        var uow = new InMemoryUnitOfWork().Seed(creator).Seed(jobWithCreator, jobNoCreator);

        var res = await Run(uow, null);

        Assert.Equal("staff@example.io (Recruiter)", res.Value.Single(j => j.Id == jobWithCreator.Id).CreatedByName);
        Assert.Null(res.Value.Single(j => j.Id == jobNoCreator.Id).CreatedByName);
        Assert.All(res.Value, j => Assert.NotNull(j.Skills));
    }

    // UTCID07 — job repository ném lỗi
    [Fact]
    public async Task UTCID07_Job_repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<JobPosting>("Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, null));
        Assert.Equal("Job DB Error", ex.Message);
    }
}
