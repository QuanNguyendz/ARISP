using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Nộp hồ sơ ứng tuyển (<see cref="ApplicationService.SubmitApplicationAsync"/>, Phase 2): chặn tin
/// đóng/hết hạn, tạo Application ở trạng thái cv_submitted, auto-link CV-JD Analysis theo hash, đẩy CV vào
/// RAG, và bắn thông báo cho nhân sự + ứng viên tự ứng tuyển.
/// </summary>
public class SubmitApplicationTests
{
    private readonly Guid _recruiterId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    private sealed record Ctx(
        InMemoryUnitOfWork Uow, RecordingNotificationService Notif, RecordingEmailService Email, RecordingRagIngestionService Rag);

    private Ctx NewCtx() => new(new InMemoryUnitOfWork(), new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    private static Task<Result<ApplicationResponse>> Run(Ctx c, SubmitApplicationRequest req, string source = "invited")
        => ApplicationServiceFactory.Create(c.Uow, c.Notif, c.Email, c.Rag).SubmitApplicationAsync(req, source, CancellationToken.None);

    // ---------- Chặn ----------

    [Fact]
    public async Task Job_not_found_fails()
    {
        var c = NewCtx();

        var res = await Run(c, ApplicationData.SubmitRequest(Guid.NewGuid()));

        Assert.True(res.IsFailure);
        Assert.Contains("Job posting not found", res.Error);
        Assert.Empty(c.Uow.Repo<Domain.Entities.Application>().Items);
    }

    [Fact]
    public async Task Inactive_job_is_rejected()
    {
        var c = NewCtx();
        var job = ApplicationData.Job(status: "closed");
        c.Uow.Seed(job);

        var res = await Run(c, ApplicationData.SubmitRequest(job.Id));

        Assert.True(res.IsFailure);
        Assert.Contains("không hoạt động", res.Error);
        Assert.Empty(c.Uow.Repo<Domain.Entities.Application>().Items);
    }

    [Fact]
    public async Task Expired_deadline_is_rejected()
    {
        var c = NewCtx();
        var job = ApplicationData.Job(deadline: DateTimeOffset.UtcNow.AddDays(-1));
        c.Uow.Seed(job);

        var res = await Run(c, ApplicationData.SubmitRequest(job.Id));

        Assert.True(res.IsFailure);
        Assert.Contains("hết hạn", res.Error);
    }

    // ---------- Tạo hồ sơ ----------

    [Fact]
    public async Task Valid_application_is_created_as_cv_submitted()
    {
        var c = NewCtx();
        var job = ApplicationData.Job(owner: _recruiterId);
        c.Uow.Seed(job);

        var res = await Run(c, ApplicationData.SubmitRequest(job.Id));

        Assert.True(res.IsSuccess);
        Assert.Equal("cv_submitted", res.Value.Status);
        var saved = Assert.Single(c.Uow.Repo<Domain.Entities.Application>().Items);
        Assert.Equal("cv_submitted", saved.Status);
        Assert.Equal("cand@example.io", saved.CandidateEmail);
        Assert.Equal("30 ngày", saved.NoticePeriod);
        Assert.True(c.Uow.SaveChangesCount >= 1); // đã persist
    }

    [Fact]
    public async Task Source_is_recorded()
    {
        var c = NewCtx();
        var job = ApplicationData.Job();
        c.Uow.Seed(job);

        var res = await Run(c, ApplicationData.SubmitRequest(job.Id), source: "self_applied");

        Assert.Equal("self_applied", res.Value.Source);
        Assert.Equal("self_applied", Assert.Single(c.Uow.Repo<Domain.Entities.Application>().Items).Source);
    }

    // ---------- RAG + CV-JD auto-link ----------

    [Fact]
    public async Task Cv_text_is_ingested_to_rag()
    {
        var c = NewCtx();
        var job = ApplicationData.Job();
        c.Uow.Seed(job);

        var res = await Run(c, ApplicationData.SubmitRequest(job.Id, cvText: "Nội dung CV"));

        var ingest = Assert.Single(c.Rag.Ingested);
        Assert.Equal("cv", ingest.SourceType);
        Assert.Equal(res.Value.Id, ingest.SourceId);
        Assert.Equal("Nội dung CV", ingest.Text);
    }

    [Fact]
    public async Task No_cv_text_skips_rag()
    {
        var c = NewCtx();
        var job = ApplicationData.Job();
        c.Uow.Seed(job);

        await Run(c, ApplicationData.SubmitRequest(job.Id, cvText: null));

        Assert.Empty(c.Rag.Ingested);
    }

    [Fact]
    public async Task Auto_links_matching_analysis_by_hash()
    {
        var c = NewCtx();
        var job = ApplicationData.Job();
        var analysis = ApplicationData.Analysis(job.Id, cvHash: "HASH-1");
        c.Uow.Seed(job).Seed(analysis);

        var res = await Run(c, ApplicationData.SubmitRequest(job.Id, cvHash: "HASH-1"));

        Assert.Equal(analysis.Id, res.Value.CvJdAnalysisId);
        Assert.Equal(analysis.Id, Assert.Single(c.Uow.Repo<Domain.Entities.Application>().Items).CvJdAnalysisId);
    }

    [Fact]
    public async Task Non_matching_hash_leaves_analysis_unlinked()
    {
        var c = NewCtx();
        var job = ApplicationData.Job();
        c.Uow.Seed(job).Seed(ApplicationData.Analysis(job.Id, cvHash: "HASH-1"));

        var res = await Run(c, ApplicationData.SubmitRequest(job.Id, cvHash: "OTHER-HASH"));

        Assert.Null(res.Value.CvJdAnalysisId);
    }

    // ---------- Thông báo ----------

    [Fact]
    public async Task Notifies_hr_group_and_recruiter()
    {
        var c = NewCtx();
        var job = ApplicationData.Job(owner: _recruiterId);
        c.Uow.Seed(job);

        await Run(c, ApplicationData.SubmitRequest(job.Id));

        Assert.Contains(c.Notif.GroupEvents, e => e.Group == "hr_admin" && e.EventType == "ReceiveNewApplication");
        Assert.Contains(c.Notif.UserEvents, e => e.UserId == _recruiterId && e.EventType == "ReceiveNewApplication");
    }

    [Fact]
    public async Task Self_applied_candidate_gets_notification_record_and_realtime()
    {
        var c = NewCtx();
        var job = ApplicationData.Job();
        c.Uow.Seed(job);

        var res = await Run(c, ApplicationData.SubmitRequest(job.Id, accountId: _accountId));

        var record = Assert.Single(c.Uow.Repo<Domain.Entities.Notification>().Items);
        Assert.Equal($"applied:{res.Value.Id}", record.DedupKey);
        Assert.Equal("applied", record.Type);
        Assert.Contains(c.Notif.UserEvents, e => e.UserId == _accountId && e.EventType == "ReceiveApplicationStatusUpdate");
        Assert.Contains(c.Notif.UserEvents, e => e.UserId == _accountId && e.EventType == "ReceiveUserNotification");
    }

    [Fact]
    public async Task Anonymous_application_creates_no_candidate_notification()
    {
        var c = NewCtx();
        var job = ApplicationData.Job();
        c.Uow.Seed(job);

        await Run(c, ApplicationData.SubmitRequest(job.Id, accountId: null));

        Assert.Empty(c.Uow.Repo<Domain.Entities.Notification>().Items);
    }
}
