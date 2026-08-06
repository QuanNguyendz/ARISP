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
/// Tạo tin tuyển dụng + cấu hình vòng (UC-46/47, <see cref="CreateJobCommandHandler"/>): validate request,
/// tạo job trạng thái draft của người tạo, sinh InterviewRoundConfig từng vòng (ngôn ngữ vòng kế thừa ngôn
/// ngữ phát hiện từ JD nếu bỏ trống), đẩy JD vào RAG và báo realtime cho người tạo.
/// </summary>
public class CreateJobCommandHandlerTests
{
    private static Task<Result<JobPostingResponse>> Run(
        InMemoryUnitOfWork uow, RecordingRagIngestionService rag, RecordingNotificationService notif,
        CreateJobPostingRequest req, Guid userId)
        => new CreateJobCommandHandler(uow, rag, notif, NullLogger<CreateJobCommandHandler>.Instance)
            .Handle(new CreateJobCommand(req, userId), CancellationToken.None);

    [Fact]
    public async Task Valid_request_creates_draft_job_with_rounds()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, "screening"), JobPostingData.Round(2, "technical") };

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

        Assert.True(res.IsSuccess);
        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        Assert.Equal("draft", job.Status);
        Assert.Equal(userId, job.CreatedByUserId);
        Assert.Equal(2, uow.Repo<InterviewRoundConfig>().Items.Count);
        Assert.Equal(2, res.Value.RoundConfigs.Count);
    }

    [Fact]
    public async Task Creator_not_found_fails_unauthorized()
    {
        var uow = new InMemoryUnitOfWork(); // không seed user

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(),
            JobPostingData.Request(), Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    [Fact]
    public async Task Jd_is_ingested_to_rag()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var rag = new RecordingRagIngestionService();

        var res = await Run(uow, rag, new RecordingNotificationService(), JobPostingData.Request(), userId);

        var ingest = Assert.Single(rag.Ingested);
        Assert.Equal("jd", ingest.SourceType);
        Assert.Equal(res.Value.Id, ingest.SourceId);
    }

    [Fact]
    public async Task Notifies_creator()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var notif = new RecordingNotificationService();

        await Run(uow, new RecordingRagIngestionService(), notif, JobPostingData.Request(), userId);

        Assert.Contains(notif.UserEvents, e => e.UserId == userId && e.EventType == "ReceiveJobPostingUpdate");
    }

    // ---------- Validation (chạy trước, không cần user) ----------

    [Fact]
    public async Task Missing_title_fails_validation()
    {
        var uow = new InMemoryUnitOfWork();
        var req = JobPostingData.Request();
        req.Title = "";

        var res = await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("Title is required", res.Error);
        Assert.Empty(uow.Repo<JobPosting>().Items);
    }

    [Fact]
    public async Task No_rounds_fails_validation()
    {
        var req = JobPostingData.Request();
        req.RoundConfigs = new();

        var res = await Run(new InMemoryUnitOfWork(), new RecordingRagIngestionService(), new RecordingNotificationService(), req, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("At least one interview round", res.Error);
    }

    [Fact]
    public async Task Invalid_interview_mode_fails()
    {
        var req = JobPostingData.Request(interviewMode: "hybrid");

        var res = await Run(new InMemoryUnitOfWork(), new RecordingRagIngestionService(), new RecordingNotificationService(), req, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("InterviewMode must be", res.Error);
    }

    [Fact]
    public async Task Onsite_without_location_fails()
    {
        var req = JobPostingData.Request(interviewMode: "onsite"); // Location null

        var res = await Run(new InMemoryUnitOfWork(), new RecordingRagIngestionService(), new RecordingNotificationService(), req, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("Location is required", res.Error);
    }

    // ---------- Ngôn ngữ vòng ----------

    [Fact]
    public async Task Round_without_language_inherits_detected_language()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, language: null) };

        await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

        var job = Assert.Single(uow.Repo<JobPosting>().Items);
        var config = Assert.Single(uow.Repo<InterviewRoundConfig>().Items);
        Assert.Equal(job.DetectedLanguage, config.InterviewLanguage);
    }

    [Fact]
    public async Task Round_explicit_language_is_kept()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(userId));
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, language: "ja") };

        await Run(uow, new RecordingRagIngestionService(), new RecordingNotificationService(), req, userId);

        Assert.Equal("ja", Assert.Single(uow.Repo<InterviewRoundConfig>().Items).InterviewLanguage);
    }
}
