using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.CreateJob;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Tạo tin tuyển dụng (<see cref="CreateJobCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "CreateJob" (UTCID01–12): validate request (title/JD/độ dài/mode/location/salary/rounds), tồn tại người tạo,
/// happy path (draft + trim + ngôn ngữ phát hiện + round ascending), RAG lỗi không chặn, và mặc định ngôn ngữ/tiền tệ/vacancies.
/// </summary>
public class CreateJobCommandHandlerTests
{
    private static Task<Result<JobPostingResponse>> Run(
        InMemoryUnitOfWork uow, CreateJobPostingRequest req, Guid userId, RecordingRagIngestionService? rag = null)
        => new CreateJobCommandHandler(uow, rag ?? new RecordingRagIngestionService(), new RecordingNotificationService(),
                NullLogger<CreateJobCommandHandler>.Instance)
            .Handle(new CreateJobCommand(req, userId), CancellationToken.None);

    private static CreateJobPostingRequest Req() => JobPostingData.Request();

    // UTCID01 — hợp lệ + người tạo tồn tại → draft, trim, ngôn ngữ phát hiện, round ascending, save 2 lần
    [Fact]
    public async Task UTCID01_Valid_creates_draft()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = Req();
        req.Title = "  Backend Developer  ";
        req.RoundConfigs = new() { JobPostingData.Round(2, "technical"), JobPostingData.Round(1, "screening") };

        var res = await Run(uow, req, userId);

        Assert.True(res.IsSuccess);
        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        Assert.Equal("draft", job.Status);
        Assert.Equal("Backend Developer", job.Title);
        Assert.Equal("vi", job.DetectedLanguage);
        Assert.Equal(1, uow.Repo<InterviewRoundConfig>().Items.First().RoundNumber);   // ascending
        Assert.Equal(2, uow.SaveChangesCount);                                          // job + rounds
    }

    // UTCID02 — Title trống
    [Fact]
    public async Task UTCID02_Title_required()
    {
        var req = Req(); req.Title = " ";
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow, req, Guid.NewGuid());
        Assert.Equal("Title is required.", res.Error);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    // UTCID03 — JobDescription trống
    [Fact]
    public async Task UTCID03_JobDescription_required()
    {
        var req = Req(); req.JobDescription = " ";
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("JobDescription is required.", res.Error);
    }

    // UTCID04 — Title > 200 ký tự
    [Fact]
    public async Task UTCID04_Title_too_long()
    {
        var req = Req(); req.Title = new string('a', 201);
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("Title cannot exceed 200 characters.", res.Error);
    }

    // UTCID05 — InterviewMode không hợp lệ
    [Fact]
    public async Task UTCID05_Invalid_interview_mode()
    {
        var req = Req(); req.InterviewMode = "hybrid-office";
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("InterviewMode must be 'remote', 'onsite', or 'both'.", res.Error);
    }

    // UTCID06 — onsite nhưng không có Location
    [Fact]
    public async Task UTCID06_Onsite_without_location()
    {
        var req = Req(); req.InterviewMode = "onsite"; req.Location = " ";
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("Location is required when InterviewMode is not 'remote'.", res.Error);
    }

    // UTCID07 — SalaryMax < SalaryMin
    [Fact]
    public async Task UTCID07_Salary_max_below_min()
    {
        var req = Req(); req.SalaryMin = 30000000; req.SalaryMax = 20000000;
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("SalaryMax cannot be less than SalaryMin.", res.Error);
    }

    // UTCID08 — SalaryIsNegotiable=true nhưng vẫn có salary
    [Fact]
    public async Task UTCID08_Negotiable_with_salary()
    {
        var req = Req(); req.SalaryIsNegotiable = true; req.SalaryMin = 10000000;
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("SalaryMin and SalaryMax must be null when SalaryIsNegotiable is true.", res.Error);
    }

    // UTCID09 — RoundConfigs rỗng
    [Fact]
    public async Task UTCID09_No_rounds()
    {
        var req = Req(); req.RoundConfigs = new();
        var res = await Run(new InMemoryUnitOfWork(), req, Guid.NewGuid());
        Assert.Equal("At least one interview round configuration is required.", res.Error);
    }

    // UTCID10 — người tạo không tồn tại → unauthorized
    [Fact]
    public async Task UTCID10_Creator_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(), Req(), Guid.NewGuid());
        Assert.True(res.IsFailure);
        Assert.Equal("User not found for the current token.", res.Error);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    // UTCID11 — RAG ingest ném lỗi → job vẫn được tạo
    [Fact]
    public async Task UTCID11_Rag_failure_still_creates_job()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));

        var res = await Run(uow, Req(), userId, new RecordingRagIngestionService { ThrowOnIngest = true });

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<JobPosting>().Items);
    }

    // UTCID12 — LanguageRequirement/SalaryCurrency trống + Vacancies=0 → mặc định
    [Fact]
    public async Task UTCID12_Defaults_applied()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = Req();
        req.LanguageRequirement = " ";
        req.SalaryCurrency = " ";
        req.Vacancies = 0;

        var res = await Run(uow, req, userId);

        Assert.True(res.IsSuccess);
        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        Assert.Equal("Tiếng Việt", job.LanguageRequirement);   // detectedLang=vi
        Assert.Equal("VND", job.SalaryCurrency);
        Assert.Null(job.Vacancies);
    }
}
