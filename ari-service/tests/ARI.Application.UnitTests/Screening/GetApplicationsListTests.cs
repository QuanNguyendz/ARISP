using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Screening;

/// <summary>
/// Danh sách ứng viên để sàng lọc (UC-53/55): theo job (<see cref="ApplicationService.GetApplicationsByJobAsync"/>),
/// theo tin của recruiter (<see cref="ApplicationService.GetApplicationsForCreatorAsync"/>) và toàn hệ thống
/// (<see cref="ApplicationService.GetAllApplicationsAsync"/>) — sắp mới nhất trước, không kèm CvText, kèm điểm CV-JD.
/// </summary>
public class GetApplicationsListTests
{
    private static ApplicationService Svc(InMemoryUnitOfWork uow)
        => ApplicationServiceFactory.Create(
            uow, new RecordingNotificationService(), new RecordingEmailService(), new RecordingRagIngestionService());

    // ---------- GetApplicationsByJobAsync ----------

    [Fact]
    public async Task By_job_fails_when_job_not_found()
    {
        var res = await Svc(new InMemoryUnitOfWork()).GetApplicationsByJobAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Job posting not found", res.Error);
    }

    [Fact]
    public async Task By_job_returns_only_that_jobs_apps_newest_first()
    {
        var job = ScreeningData.Job(title: "Backend Developer");
        var other = ScreeningData.Job(title: "Frontend Developer");
        var older = ScreeningData.App(job.Id, createdAt: DateTimeOffset.UtcNow.AddHours(-2), name: "Cũ");
        var newer = ScreeningData.App(job.Id, createdAt: DateTimeOffset.UtcNow, name: "Mới");
        var uow = new InMemoryUnitOfWork().Seed(job, other).Seed(older, newer, ScreeningData.App(other.Id));

        var res = await Svc(uow).GetApplicationsByJobAsync(job.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value!.Count);
        Assert.Equal("Mới", res.Value[0].CandidateName); // sắp mới nhất trước
        Assert.All(res.Value, a => Assert.Equal("Backend Developer", a.JobTitle)); // JobTitle override từ job
    }

    [Fact]
    public async Task By_job_list_omits_cv_text()
    {
        var job = ScreeningData.Job();
        var app = ScreeningData.App(job.Id, cvText: "Nội dung CV dài");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await Svc(uow).GetApplicationsByJobAsync(job.Id, CancellationToken.None);

        Assert.Null(Assert.Single(res.Value!).CvText); // danh sách không kéo cột text lớn
    }

    [Fact]
    public async Task By_job_includes_match_score_and_summary()
    {
        var job = ScreeningData.Job();
        var analysis = ScreeningData.Analysis(job.Id, score: 82, summary: "Khớp kỹ năng");
        var app = ScreeningData.App(job.Id, analysisId: analysis.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(analysis).Seed(app);

        var res = await Svc(uow).GetApplicationsByJobAsync(job.Id, CancellationToken.None);

        var dto = Assert.Single(res.Value!);
        Assert.Equal(82, dto.MatchScore);
        Assert.Equal("Khớp kỹ năng", dto.CvJdSummary);
    }

    // ---------- GetAllApplicationsAsync ----------

    [Fact]
    public async Task All_orders_by_created_desc_and_resolves_job_titles()
    {
        var jobA = ScreeningData.Job(title: "Job A");
        var jobB = ScreeningData.Job(title: "Job B");
        var first = ScreeningData.App(jobA.Id, createdAt: DateTimeOffset.UtcNow.AddHours(-1), name: "A-app");
        var second = ScreeningData.App(jobB.Id, createdAt: DateTimeOffset.UtcNow, name: "B-app");
        var uow = new InMemoryUnitOfWork().Seed(jobA, jobB).Seed(first, second);

        var res = await Svc(uow).GetAllApplicationsAsync(CancellationToken.None);

        Assert.Equal(2, res.Value!.Count);
        Assert.Equal("B-app", res.Value[0].CandidateName);       // mới nhất trước
        Assert.Equal("Job B", res.Value[0].JobTitle);            // tiêu đề resolve theo từng job (batch)
        Assert.Equal("Job A", res.Value[1].JobTitle);
    }

    // ---------- GetApplicationsForCreatorAsync ----------

    [Fact]
    public async Task For_creator_returns_empty_when_creator_owns_no_jobs()
    {
        var res = await Svc(new InMemoryUnitOfWork()).GetApplicationsForCreatorAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value!);
    }

    [Fact]
    public async Task For_creator_only_includes_apps_of_own_jobs()
    {
        var creatorId = Guid.NewGuid();
        var ownJob = ScreeningData.Job(owner: creatorId, title: "Của tôi");
        var otherJob = ScreeningData.Job(owner: Guid.NewGuid(), title: "Của người khác");
        var uow = new InMemoryUnitOfWork().Seed(ownJob, otherJob)
            .Seed(ScreeningData.App(ownJob.Id, name: "Ứng viên A"), ScreeningData.App(otherJob.Id, name: "Ứng viên B"));

        var res = await Svc(uow).GetApplicationsForCreatorAsync(creatorId, CancellationToken.None);

        var dto = Assert.Single(res.Value!);
        Assert.Equal("Ứng viên A", dto.CandidateName);
        Assert.Equal("Của tôi", dto.JobTitle);
    }
}
