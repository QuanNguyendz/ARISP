using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Playbooks.Commands.DeletePlaybook;
using ARI.Application.Playbooks.Queries.GetJobPlaybooks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Playbooks;

/// <summary>
/// ADR-069 — playbook THEO TIN quản lý ngay trong màn tin: HM chính (và quản trị viên) thêm/xoá, mọi
/// thành viên đội đọc; playbook công ty vẫn là của HR Leader.
/// </summary>
public class JobPlaybookAccessTests
{
    private static (InMemoryUnitOfWork uow, JobPosting job, User hm, Guid owner) Setup()
    {
        var owner = Guid.NewGuid();
        var job = new JobPosting { Title = "Backend", JobDescription = "JD", Status = "active", CreatedByUserId = owner };
        var uow = new InMemoryUnitOfWork().Seed(job);
        var hm = HiringManagerSeed.Primary(uow, job.Id);
        return (uow, job, hm, owner);
    }

    private static PlaybookDocument JobDoc(Guid jobId, string scope = "job_posting", int? round = null) => new()
    {
        Scope = scope, ScopeRefId = jobId, RoundNumber = round, DocumentType = "question_bank",
        FileName = "qb.docx", FileFormat = "docx", Status = "ready", UploadedByUserId = Guid.NewGuid(), ParsedText = "x",
    };

    private static DeletePlaybookCommandHandler DeleteHandler(InMemoryUnitOfWork uow)
        => new(uow, new RecordingRagIngestionService());

    // ---------- Xoá ----------

    [Fact]
    public async Task Primary_hm_deletes_a_playbook_of_their_job()
    {
        var (uow, job, hm, _) = Setup();
        var doc = JobDoc(job.Id);
        uow.Seed(doc);

        var res = await DeleteHandler(uow).Handle(
            new DeletePlaybookCommand(doc.Id, hm.Id, AppRoles.HiringManager, job.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(doc.DeletedAt);
    }

    [Fact]
    public async Task Job_owner_recruiter_cannot_delete()
    {
        var (uow, job, _, owner) = Setup();
        var doc = JobDoc(job.Id);
        uow.Seed(doc);

        var res = await DeleteHandler(uow).Handle(
            new DeletePlaybookCommand(doc.Id, owner, AppRoles.Recruiter, job.Id), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Null(doc.DeletedAt);
    }

    /// <summary>
    /// Id của tài liệu tin KHÁC đi qua URL của tin này → không tồn tại (không phải 403: người gọi không
    /// được biết tài liệu kia có thật). Nếu không chặn, HM của tin A xoá được playbook của tin B.
    /// </summary>
    [Fact]
    public async Task Document_of_another_job_is_not_reachable_through_this_job()
    {
        var (uow, job, hm, _) = Setup();
        var otherJob = new JobPosting { Title = "Khác", JobDescription = "JD", Status = "active", CreatedByUserId = Guid.NewGuid() };
        var doc = JobDoc(otherJob.Id);
        uow.Seed(otherJob).Seed(doc);

        var res = await DeleteHandler(uow).Handle(
            new DeletePlaybookCommand(doc.Id, hm.Id, AppRoles.HiringManager, job.Id), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Null(doc.DeletedAt);
    }

    [Fact]
    public async Task Company_playbook_is_not_deletable_by_an_hm()
    {
        var (uow, _, hm, _) = Setup();
        var doc = new PlaybookDocument
        {
            Scope = "org", DocumentType = "compliance", FileName = "law.pdf", UploadedByUserId = Guid.NewGuid(),
        };
        uow.Seed(doc);

        var res = await DeleteHandler(uow).Handle(
            new DeletePlaybookCommand(doc.Id, hm.Id, AppRoles.HiringManager), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Null(doc.DeletedAt);
    }

    // ---------- Đọc trong màn tin ----------

    [Fact]
    public async Task Team_member_reads_the_job_playbooks_without_manage_rights()
    {
        var (uow, job, _, owner) = Setup();
        uow.Seed(JobDoc(job.Id), JobDoc(job.Id, "round", 1));
        uow.Seed(new PlaybookDocument { Scope = "org", DocumentType = "style_guide", FileName = "org.pdf", UploadedByUserId = Guid.NewGuid() });
        uow.Seed(JobDoc(Guid.NewGuid()));                           // tin khác

        var res = await new GetJobPlaybooksQueryHandler(uow)
            .Handle(new GetJobPlaybooksQuery(job.Id, owner, AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value!.CanManage);
        Assert.Equal(2, res.Value.Items.Count);                     // không lẫn playbook công ty / tin khác
        Assert.Equal("job_posting", res.Value.Items[0].Scope);      // cả tin trước, rồi theo vòng
        Assert.Equal("round", res.Value.Items[1].Scope);
    }

    [Fact]
    public async Task Primary_hm_gets_manage_rights()
    {
        var (uow, job, hm, _) = Setup();

        var res = await new GetJobPlaybooksQueryHandler(uow)
            .Handle(new GetJobPlaybooksQuery(job.Id, hm.Id, AppRoles.HiringManager), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value!.CanManage);
    }

    [Fact]
    public async Task Outsider_cannot_read_a_job_playbook()
    {
        var (uow, job, _, _) = Setup();

        var res = await new GetJobPlaybooksQueryHandler(uow)
            .Handle(new GetJobPlaybooksQuery(job.Id, Guid.NewGuid(), AppRoles.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Archived_job_is_read_only_even_for_the_hm()
    {
        var (uow, job, hm, _) = Setup();
        job.Status = "archived";

        var res = await new GetJobPlaybooksQueryHandler(uow)
            .Handle(new GetJobPlaybooksQuery(job.Id, hm.Id, AppRoles.HiringManager), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value!.CanManage);
    }
}
