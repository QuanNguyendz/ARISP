using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.CreateJob;
using ARI.Application.Jobs.Commands.UpdateJobStatus;
using ARI.Application.Playbooks;
using ARI.Application.Playbooks.Commands.DeletePlaybook;
using ARI.Application.Playbooks.Commands.UploadPlaybook;
using ARI.Application.Playbooks.Queries.GetJobPlaybooks;
using ARI.Application.RecruitmentRequests;
using ARI.Application.UnitTests.JobPostings;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static ARI.Application.UnitTests.CvScoring.CvScoringKit;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// ADR-070 — bộ tiêu chí chấm CV là BẮT BUỘC ở mọi cửa: lập phiếu, duyệt/gửi lại phiếu, dựng tin, gửi duyệt,
/// đăng tin (kể cả quản trị viên vượt cổng); và chỉ đi vào tin qua trình soạn — không qua upload playbook,
/// không xoá được bản đang dùng.
/// </summary>
public class CvRubricGateTests
{
    // ================= Phiếu yêu cầu =================

    private static readonly Guid HmId = Guid.NewGuid();
    private static readonly Department Team = new() { Id = Guid.NewGuid(), Name = "Engineering" };

    private static InMemoryUnitOfWork RequestWorld() => new InMemoryUnitOfWork()
        .Seed(Team)
        .Seed(new User { Id = HmId, Email = "hm@x.io", Role = RoleNames.HiringManager, IsActive = true, DepartmentId = Team.Id });

    private static RecruitmentRequestInput Input(IReadOnlyList<CvRubricCriterionInput>? rubric) =>
        new("Backend Developer", 1, RecruitmentPriority.Medium, "Mở rộng", "Cần kỹ sư .NET", "C#",
            new[] { InterviewRoundTypes.Technical }, "full_time", "onsite", "Hà Nội", "senior",
            DateTimeOffset.UtcNow.AddMonths(1), 20_000_000, 30_000_000, "VND", false, rubric);

    private static Task<Result<Guid>> CreateRequest(InMemoryUnitOfWork uow, IReadOnlyList<CvRubricCriterionInput>? rubric)
        => new CreateRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new CreateRecruitmentRequestCommand(Input(rubric), HmId, RoleNames.HiringManager), CancellationToken.None);

    [Fact]
    public async Task Request_without_rubric_is_refused()
    {
        var uow = RequestWorld();
        var res = await CreateRequest(uow, null);
        Assert.True(res.IsFailure);
        Assert.Contains("bộ tiêu chí chấm CV", res.Error);
        Assert.Empty(uow.Repo<RecruitmentRequest>().Items);
    }

    [Fact]
    public async Task Request_with_weights_not_summing_to_100_is_refused()
    {
        var res = await CreateRequest(RequestWorld(), Inputs(("Kinh nghiệm", 50), ("Kỹ năng", 40)));
        Assert.True(res.IsFailure);
        Assert.Contains("100", res.Error);
    }

    [Fact]
    public async Task Request_stores_a_keyed_rubric_snapshot()
    {
        var uow = RequestWorld();
        var res = await CreateRequest(uow, Inputs(("Kinh nghiệm .NET", 60), ("Kỹ năng", 40)));

        Assert.True(res.IsSuccess, res.Error);
        var saved = ScoringRubric.Deserialize(Assert.Single(uow.Repo<RecruitmentRequest>().Items).CvRubricJson);
        Assert.Equal(new[] { "kinh_nghiem_net", "ky_nang" }, saved.Select(c => c.Key));
    }

    private static RecruitmentRequest LegacyRequest(string status) => new()
    {
        RequestedByUserId = HmId, Title = "Cũ", DepartmentId = Team.Id, Status = status, CvRubricJson = null,
    };

    [Fact]
    public async Task Legacy_request_without_rubric_cannot_be_approved()
    {
        var hrId = Guid.NewGuid();
        var recruiterId = Guid.NewGuid();
        var req = LegacyRequest(RecruitmentRequestStatus.Pending);
        var uow = RequestWorld().Seed(req)
            .Seed(new User { Id = recruiterId, Email = "r@x.io", Role = RoleNames.Recruiter, IsActive = true });

        var res = await new ApproveRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new ApproveRecruitmentRequestCommand(req.Id, recruiterId, null, hrId), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("chưa có bộ tiêu chí chấm CV", res.Error);
        Assert.Equal(RecruitmentRequestStatus.Pending, req.Status);
    }

    [Fact]
    public async Task Legacy_rejected_request_cannot_be_resubmitted_until_edited()
    {
        var req = LegacyRequest(RecruitmentRequestStatus.Rejected);
        var uow = RequestWorld().Seed(req);

        var res = await new ResubmitRecruitmentRequestCommandHandler(uow, new RecordingNotificationService())
            .Handle(new ResubmitRecruitmentRequestCommand(req.Id, HmId), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(RecruitmentRequestStatus.Rejected, req.Status);
    }

    // ================= Dựng tin =================

    [Fact]
    public async Task Creating_a_job_copies_the_request_rubric()
    {
        var recruiterId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobPostingData.Staff(recruiterId));
        var rr = JobPostingData.ApprovedRecruitmentRequest(Guid.NewGuid(), recruiterId);
        rr.CvRubricJson = SampleRubricJson();
        uow.Seed(rr);
        var req = JobPostingData.Request();
        req.RecruitmentRequestId = rr.Id;
        var queue = new RecordingCvScoringQueue();

        var res = await new CreateJobCommandHandler(uow, new RecordingRagIngestionService(), new RecordingNotificationService(),
                RubricService(uow, queue), NullLogger<CreateJobCommandHandler>.Instance)
            .Handle(new CreateJobCommand(req, recruiterId), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        var live = await CvRubricStore.LiveAsync(uow, res.Value!.Id);
        Assert.NotNull(live);
        Assert.Equal(rr.RequestedByUserId, live!.UploadedByUserId);
        Assert.Equal(2, CvRubricStore.Criteria(live).Count);
    }

    // ================= Gửi duyệt / đăng tin =================

    private static Task<Result<JobPostingResponse>> ChangeStatus(
        InMemoryUnitOfWork uow, Guid jobId, UpdateJobStatusRequest req, Guid actor, string role)
        => new UpdateJobStatusCommandHandler(uow, new RecordingFileStorage(), new RecordingJdStampService(),
                new StubDocumentParser(), new RecordingNotificationService(), new RecordingEmailService(),
                TestConfig.Frontend(), NullLogger<UpdateJobStatusCommandHandler>.Instance)
            .Handle(new UpdateJobStatusCommand(jobId, req, actor, role), CancellationToken.None);

    [Fact]
    public async Task Job_without_rubric_cannot_be_sent_for_sign_off()
    {
        var owner = Guid.NewGuid();
        var job = JobPostingData.Job(owner: owner, status: "draft");
        var uow = new InMemoryUnitOfWork().Seed(job);
        HiringManagerSeed.Primary(uow, job.Id);

        var res = await ChangeStatus(uow, job.Id, new UpdateJobStatusRequest { Status = "pending" }, owner, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CvScoringErrors.RubricRequired, res.ErrorCode);
        Assert.Equal("draft", job.Status);
    }

    /// <summary>Vượt chữ ký HM không làm ra bộ tiêu chí — quản trị viên cũng bị chặn.</summary>
    [Fact]
    public async Task Admin_bypass_does_not_bypass_the_rubric()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending");
        var uow = new InMemoryUnitOfWork().Seed(job);
        HiringManagerSeed.Primary(uow, job.Id);

        var res = await ChangeStatus(uow, job.Id,
            new UpdateJobStatusRequest { Status = "active", HmBypassReason = "HM nghỉ phép, cần đăng gấp" },
            Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.Equal(CvScoringErrors.RubricRequired, res.ErrorCode);
        Assert.Equal("pending", job.Status);
    }

    [Fact]
    public async Task With_a_rubric_the_job_can_be_sent_for_sign_off()
    {
        var owner = Guid.NewGuid();
        var job = JobPostingData.Job(owner: owner, status: "draft");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        HiringManagerSeed.Primary(uow, job.Id);

        var res = await ChangeStatus(uow, job.Id, new UpdateJobStatusRequest { Status = "pending" }, owner, AppRoles.Recruiter);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal("pending", job.Status);
    }

    // ================= Playbook =================

    private static (InMemoryUnitOfWork Uow, JobPosting Job, User Hm) JobWithHm()
    {
        var job = new JobPosting { Title = "Backend", JobDescription = "JD", Status = "active", CreatedByUserId = Guid.NewGuid() };
        var uow = new InMemoryUnitOfWork().Seed(job);
        var hm = HiringManagerSeed.Primary(uow, job.Id);
        return (uow, job, hm);
    }

    [Theory]
    [InlineData("job_posting", null)]
    [InlineData("round", 1)]
    public async Task Job_level_cv_rubric_cannot_be_uploaded_as_a_playbook(string scope, int? round)
    {
        var (uow, job, hm) = JobWithHm();
        var bytes = RubricSheet.Build(CvRubricEditing.Normalize(SampleRubric()).Criteria);

        var res = await new UploadPlaybookCommandHandler(uow, new StubDocumentParser(), new RecordingFileStorage(), new RecordingRagIngestionService())
            .Handle(new UploadPlaybookCommand(hm.Id, AppRoles.HiringManager, scope, job.Id, round,
                ScoringRubric.TypeCvRubric, "rubric.xlsx", bytes, ".xlsx"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("màn tin", res.Error);
        Assert.Empty(uow.Repo<PlaybookDocument>().Items);
    }

    [Fact]
    public async Task Org_cv_rubric_is_still_uploadable_as_a_template()
    {
        var uow = new InMemoryUnitOfWork();
        var bytes = RubricSheet.Build(CvRubricEditing.Normalize(SampleRubric()).Criteria);

        var res = await new UploadPlaybookCommandHandler(uow, new StubDocumentParser(), new RecordingFileStorage(), new RecordingRagIngestionService())
            .Handle(new UploadPlaybookCommand(Guid.NewGuid(), AppRoles.HrAdmin, "org", null, null,
                ScoringRubric.TypeCvRubric, "mau.xlsx", bytes, ".xlsx"), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal(2, res.Value!.CriteriaCount);
    }

    [Fact]
    public async Task Live_job_rubric_cannot_be_deleted()
    {
        var (uow, job, hm) = JobWithHm();
        var rubric = DefaultRubric(job.Id);
        uow.Seed(rubric);

        var res = await new DeletePlaybookCommandHandler(uow, new RecordingRagIngestionService())
            .Handle(new DeletePlaybookCommand(rubric.Id, hm.Id, AppRoles.HiringManager, job.Id), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Null(rubric.DeletedAt);
    }

    [Fact]
    public async Task Job_playbook_list_leaves_the_cv_rubric_to_its_own_panel()
    {
        var (uow, job, hm) = JobWithHm();
        uow.Seed(DefaultRubric(job.Id));
        uow.Seed(new PlaybookDocument
        {
            Scope = "job_posting", ScopeRefId = job.Id, DocumentType = "question_bank", FileName = "qb.docx",
            FileFormat = "docx", Status = "ready", UploadedByUserId = hm.Id, ParsedText = "x",
        });

        var res = await new GetJobPlaybooksQueryHandler(uow)
            .Handle(new GetJobPlaybooksQuery(job.Id, hm.Id, AppRoles.HiringManager), CancellationToken.None);

        var item = Assert.Single(res.Value!.Items);
        Assert.Equal("question_bank", item.DocumentType);
    }
}
