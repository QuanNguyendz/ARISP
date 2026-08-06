using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Jobs.Queries.GetJobs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>
/// Job Board công khai (UC-16 View / UC-17 Search &amp; Filter, <see cref="GetJobsQueryHandler"/>): chỉ tin
/// active + public + chưa hết hạn; lọc theo search/category/experience/location/language; phân trang + tổng;
/// sắp theo lương / urgent-first / độ phù hợp CV.
/// </summary>
public class GetJobsQueryHandlerTests
{
    private static GetJobsQuery Q(
        string? search = null, string? categories = null, string? employmentTypes = null,
        string? experienceLevels = null, string? workModes = null, string? locations = null,
        string? skills = null, string? languages = null, string? sortBy = null,
        int? minSalary = null, int? maxSalary = null, bool? negotiable = null,
        int page = 1, int pageSize = 8, Guid? currentUserId = null)
        => new(search, categories, employmentTypes, experienceLevels, workModes, locations,
               skills, languages, sortBy, minSalary, maxSalary, negotiable, page, pageSize, currentUserId);

    private static Task<Result<JobsPageDto>> Run(InMemoryUnitOfWork uow, GetJobsQuery q)
        => new GetJobsQueryHandler(uow).Handle(q, CancellationToken.None);

    [Fact]
    public async Task Only_active_public_non_expired_jobs_are_returned()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "OK"),                                        // hợp lệ
            JobBoardData.PublicJob(title: "Draft", status: "draft"),                    // chưa active
            JobBoardData.PublicJob(title: "Private", isPublic: false),                  // không public
            JobBoardData.PublicJob(title: "Expired", deadline: DateTimeOffset.UtcNow.AddDays(-1))); // hết hạn

        var res = await Run(uow, Q());

        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal("OK", Assert.Single(res.Value.Items).Title);
    }

    [Fact]
    public async Task Search_matches_title()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Backend Developer"),
            JobBoardData.PublicJob(title: "Frontend Designer"));

        var res = await Run(uow, Q(search: "backend"));

        Assert.Equal("Backend Developer", Assert.Single(res.Value.Items).Title);
    }

    [Fact]
    public async Task Search_matches_skill()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "A", skills: new() { "Kubernetes" }),
            JobBoardData.PublicJob(title: "B", skills: new() { "React" }));

        var res = await Run(uow, Q(search: "kubernetes"));

        Assert.Equal("A", Assert.Single(res.Value.Items).Title);
    }

    [Fact]
    public async Task Category_filter_narrows_results()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "BE", category: "backend"),
            JobBoardData.PublicJob(title: "FE", category: "frontend"));

        var res = await Run(uow, Q(categories: "frontend"));

        Assert.Equal("FE", Assert.Single(res.Value.Items).Title);
    }

    [Fact]
    public async Task Experience_level_filter_narrows_results()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Sr", experienceLevel: "senior"),
            JobBoardData.PublicJob(title: "Jr", experienceLevel: "junior"));

        var res = await Run(uow, Q(experienceLevels: "junior"));

        Assert.Equal("Jr", Assert.Single(res.Value.Items).Title);
    }

    [Fact]
    public async Task Location_filter_is_case_insensitive()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "HN", location: "Hà Nội"),
            JobBoardData.PublicJob(title: "SG", location: "HCM"));

        var res = await Run(uow, Q(locations: "hcm"));

        Assert.Equal("SG", Assert.Single(res.Value.Items).Title);
    }

    [Fact]
    public async Task Language_filter_narrows_results()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "VI", language: "vi"),
            JobBoardData.PublicJob(title: "EN", language: "en"));

        var res = await Run(uow, Q(languages: "en"));

        Assert.Equal("EN", Assert.Single(res.Value.Items).Title);
    }

    [Fact]
    public async Task Pagination_limits_items_and_reports_total()
    {
        var uow = new InMemoryUnitOfWork();
        for (int i = 0; i < 5; i++) uow.Seed(JobBoardData.PublicJob(title: $"Job {i}"));

        var res = await Run(uow, Q(page: 1, pageSize: 2));

        Assert.Equal(2, res.Value.Items.Count);
        Assert.Equal(5, res.Value.TotalCount);
    }

    [Fact]
    public async Task Second_page_returns_different_items()
    {
        var uow = new InMemoryUnitOfWork();
        // PublishedAt giảm dần để thứ tự "mới nhất" xác định.
        for (int i = 0; i < 4; i++)
            uow.Seed(JobBoardData.PublicJob(title: $"Job {i}", publishedAt: DateTimeOffset.UtcNow.AddDays(-i)));

        var page1 = await Run(uow, Q(page: 1, pageSize: 2));
        var page2 = await Run(uow, Q(page: 2, pageSize: 2));

        var p1Titles = page1.Value.Items.Select(i => i.Title).ToHashSet();
        Assert.All(page2.Value.Items, i => Assert.DoesNotContain(i.Title, p1Titles));
    }

    [Fact]
    public async Task Salary_desc_sort_orders_by_highest_max_salary()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Low", salaryMin: 10, salaryMax: 20),
            JobBoardData.PublicJob(title: "High", salaryMin: 50, salaryMax: 80));

        var res = await Run(uow, Q(sortBy: "salary_desc"));

        Assert.Equal("High", res.Value.Items.First().Title);
    }

    [Fact]
    public async Task Urgent_jobs_come_first_by_default()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Normal", isUrgent: false, publishedAt: DateTimeOffset.UtcNow),
            JobBoardData.PublicJob(title: "Urgent", isUrgent: true, publishedAt: DateTimeOffset.UtcNow.AddDays(-5)));

        var res = await Run(uow, Q());

        Assert.Equal("Urgent", res.Value.Items.First().Title);
    }

    [Fact]
    public async Task Relevance_sort_orders_by_matching_skills()
    {
        var candidateId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(JobBoardData.Candidate(candidateId, skillsJson: "[\"C#\",\"Redis\"]"))
            .Seed(JobBoardData.PublicJob(title: "Match", skills: new() { "C#", "Redis" }))
            .Seed(JobBoardData.PublicJob(title: "NoMatch", skills: new() { "Go" }));

        var res = await Run(uow, Q(sortBy: "relevance", currentUserId: candidateId));

        Assert.Equal("Match", res.Value.Items.First().Title);
    }

    [Fact]
    public async Task Relevance_without_candidate_skills_falls_back_to_newest()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "A", publishedAt: DateTimeOffset.UtcNow.AddDays(-1)),
            JobBoardData.PublicJob(title: "B", publishedAt: DateTimeOffset.UtcNow));

        var res = await Run(uow, Q(sortBy: "relevance", currentUserId: null));

        Assert.True(res.IsSuccess);
        Assert.Equal("B", res.Value.Items.First().Title); // rơi về mới nhất, không lỗi
    }
}
