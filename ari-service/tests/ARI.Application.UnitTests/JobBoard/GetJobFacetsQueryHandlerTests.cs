using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Queries.GetJobFacets;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>
/// Bộ lọc khả dụng cho Job Board (UC-17, <see cref="GetJobFacetsQueryHandler"/>): chỉ đếm trên tin active+public,
/// gom nhóm theo giá trị + số lượng, gộp nhãn trùng (intern/fresher → "Intern / Fresher").
/// </summary>
public class GetJobFacetsQueryHandlerTests
{
    private static Task<Result<JobFacetsResponse>> Run(InMemoryUnitOfWork uow)
        => new GetJobFacetsQueryHandler(uow).Handle(new GetJobFacetsQuery(), CancellationToken.None);

    [Fact]
    public async Task Only_active_public_jobs_are_counted()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(),
            JobBoardData.PublicJob(status: "draft"),
            JobBoardData.PublicJob(isPublic: false));

        var res = await Run(uow);

        Assert.Equal(1, res.Value.TotalJobs);
    }

    [Fact]
    public async Task Category_facet_reports_counts()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(category: "backend"),
            JobBoardData.PublicJob(category: "backend"),
            JobBoardData.PublicJob(category: "frontend"));

        var res = await Run(uow);

        Assert.Equal(2, res.Value.Categories.Single(f => f.Value == "backend").Count);
        Assert.Equal(1, res.Value.Categories.Single(f => f.Value == "frontend").Count);
    }

    [Fact]
    public async Task Skills_facet_aggregates_across_jobs()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(skills: new() { "C#", "Redis" }),
            JobBoardData.PublicJob(skills: new() { "C#" }));

        var res = await Run(uow);

        Assert.Equal(2, res.Value.Skills.Single(f => f.Label == "C#").Count);
        Assert.Equal(1, res.Value.Skills.Single(f => f.Label == "Redis").Count);
    }

    [Fact]
    public async Task Experience_levels_merge_by_display_label()
    {
        // 'intern' và 'fresher' cùng nhãn "Intern / Fresher" → gộp thành 1 facet count 2.
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(experienceLevel: "intern"),
            JobBoardData.PublicJob(experienceLevel: "fresher"));

        var res = await Run(uow);

        var facet = res.Value.ExperienceLevels.Single(f => f.Label == "Intern / Fresher");
        Assert.Equal(2, facet.Count);
    }

    [Fact]
    public async Task Empty_board_returns_zero_total()
    {
        var res = await Run(new InMemoryUnitOfWork());

        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value.TotalJobs);
    }
}
