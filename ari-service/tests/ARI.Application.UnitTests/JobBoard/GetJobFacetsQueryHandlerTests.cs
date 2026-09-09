using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Queries.GetJobFacets;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>
/// Bộ lọc khả dụng cho Job Board (<see cref="GetJobFacetsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetJobFacets" (UTCID01–06): chỉ đếm tin active+public+chưa hết hạn, gom nhóm không phân biệt hoa-thường
/// + loại giá trị rỗng, gộp intern/fresher, sắp theo count giảm dần, và lỗi repo.
/// </summary>
public class GetJobFacetsQueryHandlerTests
{
    private static Task<Result<JobFacetsResponse>> Run(InMemoryUnitOfWork uow)
        => new GetJobFacetsQueryHandler(uow).Handle(new GetJobFacetsQuery(), CancellationToken.None);

    // UTCID01 — không có tin đủ điều kiện → TotalJobs=0, facet rỗng
    [Fact]
    public async Task UTCID01_No_eligible_job()
    {
        var res = await Run(new InMemoryUnitOfWork());
        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value.TotalJobs);
        Assert.Empty(res.Value.Categories);
        Assert.Empty(res.Value.Skills);
    }

    // UTCID02 — tin active public không/còn hạn → TotalJobs = số tin đủ điều kiện
    [Fact]
    public async Task UTCID02_Active_public_jobs_counted()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(deadline: null),
            JobBoardData.PublicJob(deadline: DateTimeOffset.UtcNow.AddDays(5)));

        var res = await Run(uow);

        Assert.Equal(2, res.Value.TotalJobs);
    }

    // UTCID03 — inactive/private/expired không đóng góp facet
    [Fact]
    public async Task UTCID03_Ineligible_excluded()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(category: "backend"),
            JobBoardData.PublicJob(status: "draft", category: "frontend"),
            JobBoardData.PublicJob(isPublic: false, category: "devops"),
            JobBoardData.PublicJob(deadline: DateTimeOffset.UtcNow.AddDays(-1), category: "qa"));

        var res = await Run(uow);

        Assert.Equal(1, res.Value.TotalJobs);
        Assert.Equal("backend", Assert.Single(res.Value.Categories).Value);
    }

    // UTCID04 — location khác hoa-thường + rỗng → gom không phân biệt hoa-thường, loại rỗng
    [Fact]
    public async Task UTCID04_Case_insensitive_grouping_excludes_blanks()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(location: "Hà Nội"),
            JobBoardData.PublicJob(location: "hà nội"),
            JobBoardData.PublicJob(location: "  "));

        var res = await Run(uow);

        Assert.Equal(3, res.Value.TotalJobs);
        var loc = Assert.Single(res.Value.Locations);   // gom 2 case + loại blank
        Assert.Equal(2, loc.Count);
    }

    // UTCID05 — intern+fresher gộp; skills sắp theo count giảm dần
    [Fact]
    public async Task UTCID05_Merge_and_ordering()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(experienceLevel: "intern", skills: new() { "C#" }),
            JobBoardData.PublicJob(experienceLevel: "fresher", skills: new() { "C#" }),
            JobBoardData.PublicJob(experienceLevel: "senior", skills: new() { "C#", "Redis" }));

        var res = await Run(uow);

        Assert.Equal(2, res.Value.ExperienceLevels.Single(f => f.Label == "Intern / Fresher").Count);
        Assert.Equal("C#", res.Value.Skills.First().Label);   // count 3 > Redis count 1
        Assert.Equal(3, res.Value.Skills.First().Count);
    }

    // UTCID06 — job repository ném lỗi
    [Fact]
    public async Task UTCID06_Job_repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<JobPosting>("Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Job DB Error", ex.Message);
    }
}
