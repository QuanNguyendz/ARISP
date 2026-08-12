using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Options;
using ARI.Application.UnitTests.JobBoard;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Đọc hồ sơ của chính ứng viên (<c>PortalApplicationsFeature.cs</c>). Cả hai handler trả
/// <c>Result&lt;object&gt;</c> (shape ẩn danh) nên assert qua serialize JSON + bất biến bảo mật:
/// chỉ trả hồ sơ của đúng ứng viên; chi tiết chặn IDOR (NotFound/Forbidden) + auto-link theo email.
/// </summary>
public class GetMyApplicationsQueryHandlerTests
{
    private static ARI.Domain.Entities.Application App(Guid candId, Guid jobId, string name)
        => new()
        {
            CandidateAccountId = candId,
            JobPostingId = jobId,
            CandidateName = name,
            CandidateEmail = "cand@example.io",
            Status = "cv_submitted",
        };

    private static GetMyApplicationsQueryHandler Handler(InMemoryUnitOfWork uow)
        => new(uow, new RecordingFileStorage(), new InterviewOptions());

    [Fact]
    public async Task Returns_only_own_applications()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var job = JobBoardData.PublicJob(title: "Backend Developer");
        var mine = App(me, job.Id, "Nguyen Van A");
        var theirs = App(other, job.Id, "Tran Thi B");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(mine, theirs);

        // Email=null → bỏ nhánh auto-link SQL; chỉ lấy hồ sơ đã gắn CandidateAccountId.
        var res = await Handler(uow).Handle(new GetMyApplicationsQuery(me, Email: null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var json = JsonSerializer.Serialize(res.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetArrayLength());              // chỉ hồ sơ của tôi
        Assert.Contains(mine.Id.ToString(), json);
        Assert.DoesNotContain(theirs.Id.ToString(), json);             // không lộ hồ sơ người khác
        Assert.Contains("Backend Developer", json);                    // job title được join
    }

    [Fact]
    public async Task Empty_when_no_applications()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new GetMyApplicationsQuery(Guid.NewGuid(), Email: null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(res.Value));
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }
}

/// <summary>Chi tiết 1 hồ sơ (<see cref="GetMyApplicationDetailQueryHandler"/>): IDOR (NotFound/Forbidden) + auto-link email.</summary>
public class GetMyApplicationDetailQueryHandlerTests
{
    private static GetMyApplicationDetailQueryHandler Handler(InMemoryUnitOfWork uow)
        => new(uow, new RecordingFileStorage());

    [Fact]
    public async Task Unknown_application_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow)
            .Handle(new GetMyApplicationDetailQuery(Guid.NewGuid(), Guid.NewGuid(), Email: null), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("Không tìm thấy hồ sơ", res.Error);
    }

    [Fact]
    public async Task Foreign_application_is_forbidden()
    {
        var me = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var job = JobBoardData.PublicJob();
        var app = new ARI.Domain.Entities.Application
        {
            CandidateAccountId = owner,
            JobPostingId = job.Id,
            CandidateName = "Chủ thật",
            CandidateEmail = "owner@example.io",
            Status = "cv_submitted",
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await Handler(uow)
            .Handle(new GetMyApplicationDetailQuery(app.Id, me, Email: "attacker@example.io"), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("Forbidden", res.Error);
        Assert.Equal(owner, app.CandidateAccountId);   // không bị chiếm quyền sở hữu
    }

    [Fact]
    public async Task Owner_gets_detail_with_job_title()
    {
        var me = Guid.NewGuid();
        var job = JobBoardData.PublicJob(title: "Backend Developer");
        var app = new ARI.Domain.Entities.Application
        {
            CandidateAccountId = me,
            JobPostingId = job.Id,
            CandidateName = "Nguyen Van A",
            CandidateEmail = "me@example.io",
            Status = "cv_submitted",
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await Handler(uow)
            .Handle(new GetMyApplicationDetailQuery(app.Id, me, Email: null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var json = JsonSerializer.Serialize(res.Value);
        Assert.Contains(app.Id.ToString(), json);
        Assert.Contains("Backend Developer", json);
    }

    [Fact]
    public async Task Auto_links_legacy_application_by_email()
    {
        var me = Guid.NewGuid();
        var job = JobBoardData.PublicJob();
        // Hồ sơ cũ chưa gắn tài khoản nhưng trùng email trong token → auto-link rồi coi là chủ sở hữu.
        var app = new ARI.Domain.Entities.Application
        {
            CandidateAccountId = null,
            JobPostingId = job.Id,
            CandidateName = "Nguyen Van A",
            CandidateEmail = "me@example.io",
            Status = "cv_submitted",
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app);

        var res = await Handler(uow)
            .Handle(new GetMyApplicationDetailQuery(app.Id, me, Email: "me@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(me, app.CandidateAccountId);      // đã gắn tài khoản
        Assert.True(uow.SaveChangesCount >= 1);
    }
}
