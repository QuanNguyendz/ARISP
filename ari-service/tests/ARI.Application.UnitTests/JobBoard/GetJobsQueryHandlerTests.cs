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
/// Job Board công khai (<see cref="GetJobsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetJobs" (UTCID01–14): chỉ tin active/public/chưa hết hạn, clamp page/size, lọc search/CSV/salary,
/// sắp theo lương (quy đổi USD×25000) / relevance (theo kỹ năng CV) / mới nhất, và lỗi repo.
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

    // UTCID01 — tin đủ điều kiện; sắp PublishedAt/CreatedAt giảm dần, dùng Page=1/PageSize=8
    [Fact]
    public async Task UTCID01_Eligible_jobs_default()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Newer", publishedAt: DateTimeOffset.UtcNow),
            JobBoardData.PublicJob(title: "Older", publishedAt: DateTimeOffset.UtcNow.AddDays(-2)));

        var res = await Run(uow, Q());

        Assert.Equal(2, res.Value.TotalCount);
        Assert.Equal("Newer", res.Value.Items.First().Title);
    }

    // UTCID02 — Page<1, PageSize<1 → clamp Page=1, PageSize=8
    [Fact]
    public async Task UTCID02_Page_and_size_clamped()
    {
        var uow = new InMemoryUnitOfWork();
        for (int i = 0; i < 10; i++) uow.Seed(JobBoardData.PublicJob(title: $"Job {i}"));

        var res = await Run(uow, Q(page: 0, pageSize: 0));

        Assert.Equal(8, res.Value.Items.Count);   // clamp PageSize=8
        Assert.Equal(10, res.Value.TotalCount);
    }

    // UTCID03 — inactive/private/expired không được trả
    [Fact]
    public async Task UTCID03_Only_eligible_returned()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "OK"),
            JobBoardData.PublicJob(title: "Draft", status: "draft"),
            JobBoardData.PublicJob(title: "Private", isPublic: false),
            JobBoardData.PublicJob(title: "Expired", deadline: DateTimeOffset.UtcNow.AddDays(-1)));

        var res = await Run(uow, Q());

        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal("OK", Assert.Single(res.Value.Items).Title);
    }

    // UTCID04 — không có tin khớp query → rỗng
    [Fact]
    public async Task UTCID04_No_match()
    {
        var uow = new InMemoryUnitOfWork().Seed(JobBoardData.PublicJob(title: "Backend"));
        var res = await Run(uow, Q(search: "khong-ton-tai"));
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value.Items);
        Assert.Equal(0, res.Value.TotalCount);
    }

    // UTCID05 — search khớp title/department/skill (trim + không phân biệt hoa-thường)
    [Fact]
    public async Task UTCID05_Search()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Backend Developer"),
            JobBoardData.PublicJob(title: "Frontend Designer", skills: new() { "React" }));

        var res = await Run(uow, Q(search: "  BACKEND  "));

        Assert.Equal("Backend Developer", Assert.Single(res.Value.Items).Title);
    }

    // UTCID06 — nhiều bộ lọc CSV → chỉ tin khớp tất cả
    [Fact]
    public async Task UTCID06_Csv_filters()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Match", category: "backend", employmentType: "full_time", experienceLevel: "senior",
                workMode: "remote", location: "Hà Nội", language: "vi", skills: new() { "C#" }),
            JobBoardData.PublicJob(title: "WrongCategory", category: "frontend"));

        var res = await Run(uow, Q(categories: "backend", employmentTypes: "full_time", experienceLevels: "senior",
            workModes: "remote", locations: "Hà Nội", skills: "C#", languages: "vi"));

        Assert.Equal("Match", Assert.Single(res.Value.Items).Title);
    }

    // UTCID07 — SalaryIsNegotiable=true → gồm tin thoả thuận + tin lương 0
    [Fact]
    public async Task UTCID07_Negotiable_filter()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Negotiable", negotiable: true),
            JobBoardData.PublicJob(title: "ZeroSalary", salaryMin: 0, salaryMax: 0),
            JobBoardData.PublicJob(title: "Paid", salaryMin: 10000000, salaryMax: 20000000));

        var res = await Run(uow, Q(negotiable: true));

        var titles = res.Value.Items.Select(i => i.Title).ToHashSet();
        Assert.Contains("Negotiable", titles);
        Assert.Contains("ZeroSalary", titles);
        Assert.DoesNotContain("Paid", titles);
    }

    // UTCID08 — Min/Max salary → loại thoả thuận; USD quy đổi ×25000
    [Fact]
    public async Task UTCID08_Salary_range_with_usd()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "VndInRange", salaryMin: 25000000, salaryMax: 40000000),
            JobBoardData.PublicJob(title: "UsdInRange", currency: "USD", salaryMin: 1000, salaryMax: 2000), // 25M–50M
            JobBoardData.PublicJob(title: "Negotiable", negotiable: true));

        var res = await Run(uow, Q(minSalary: 20000000, maxSalary: 50000000));

        var titles = res.Value.Items.Select(i => i.Title).ToHashSet();
        Assert.Contains("VndInRange", titles);
        Assert.Contains("UsdInRange", titles);
        Assert.DoesNotContain("Negotiable", titles);
    }

    // UTCID09 — SortBy=salary_desc → lương giảm dần (quy đổi VND); thoả thuận/0 cuối
    [Fact]
    public async Task UTCID09_Salary_desc()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Low", salaryMin: 10000000, salaryMax: 20000000),
            JobBoardData.PublicJob(title: "High", salaryMin: 50000000, salaryMax: 80000000),
            JobBoardData.PublicJob(title: "Negotiable", negotiable: true));

        var res = await Run(uow, Q(sortBy: "salary_desc"));

        Assert.Equal("High", res.Value.Items.First().Title);
        Assert.Equal("Negotiable", res.Value.Items.Last().Title);
    }

    // UTCID10 — SortBy=salary_asc → lương tăng dần; thoả thuận/0 cuối
    [Fact]
    public async Task UTCID10_Salary_asc()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "Low", salaryMin: 10000000, salaryMax: 20000000),
            JobBoardData.PublicJob(title: "High", salaryMin: 50000000, salaryMax: 80000000),
            JobBoardData.PublicJob(title: "Negotiable", negotiable: true));

        var res = await Run(uow, Q(sortBy: "salary_asc"));

        Assert.Equal("Low", res.Value.Items.First().Title);
        Assert.Equal("Negotiable", res.Value.Items.Last().Title);
    }

    // UTCID11 — SortBy=relevance + ứng viên có kỹ năng → sắp theo số kỹ năng trùng giảm dần
    [Fact]
    public async Task UTCID11_Relevance()
    {
        var candidateId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(JobBoardData.Candidate(candidateId, skillsJson: "[\"C#\",\"Redis\"]"))
            .Seed(JobBoardData.PublicJob(title: "Match", skills: new() { "C#", "Redis" }),
                  JobBoardData.PublicJob(title: "NoMatch", skills: new() { "Go" }));

        var res = await Run(uow, Q(sortBy: "relevance", currentUserId: candidateId));

        Assert.Equal("Match", res.Value.Items.First().Title);
    }

    // UTCID12 — SortBy=relevance nhưng không có user/kỹ năng → rơi về mới nhất
    [Fact]
    public async Task UTCID12_Relevance_fallback_no_user()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            JobBoardData.PublicJob(title: "A", publishedAt: DateTimeOffset.UtcNow.AddDays(-1)),
            JobBoardData.PublicJob(title: "B", publishedAt: DateTimeOffset.UtcNow));

        var res = await Run(uow, Q(sortBy: "relevance", currentUserId: null));

        Assert.True(res.IsSuccess);
        Assert.Equal("B", res.Value.Items.First().Title);
    }

    // UTCID13 — SkillsJson của ứng viên hỏng → coi như rỗng, rơi về mới nhất
    [Fact]
    public async Task UTCID13_Malformed_skills_json()
    {
        var candidateId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(JobBoardData.Candidate(candidateId, skillsJson: "{not-json}"))
            .Seed(JobBoardData.PublicJob(title: "A", publishedAt: DateTimeOffset.UtcNow.AddDays(-1)),
                  JobBoardData.PublicJob(title: "B", publishedAt: DateTimeOffset.UtcNow));

        var res = await Run(uow, Q(sortBy: "relevance", currentUserId: candidateId));

        Assert.True(res.IsSuccess);
        Assert.Equal("B", res.Value.Items.First().Title);
    }

    // UTCID14 — job repository ném lỗi
    [Fact]
    public async Task UTCID14_Job_repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<JobPosting>("Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, Q()));
        Assert.Equal("Job DB Error", ex.Message);
    }
}
