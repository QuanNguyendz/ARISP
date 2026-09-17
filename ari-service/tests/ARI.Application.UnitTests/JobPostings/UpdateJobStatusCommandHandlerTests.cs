using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.UpdateJobStatus;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Approval workflow tin tuyển dụng (<see cref="UpdateJobStatusCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "UpdateJobStatus" (UTCID01–15): validate status (rỗng/không hợp lệ/draft), tồn tại, phân quyền, trùng trạng thái,
/// archived, các nhánh reject/active/pending/archived + happy path (pending/active/closed).
/// </summary>
public class UpdateJobStatusCommandHandlerTests
{
    private static readonly Guid OwnerA = Guid.Parse("86000000-0000-0000-0000-000000000001");
    private static readonly Guid HrA = Guid.Parse("87000000-0000-0000-0000-000000000001");

    private sealed record Ctx(InMemoryUnitOfWork Uow, RecordingFileStorage Storage, RecordingJdStampService Stamp,
        StubDocumentParser Parser, RecordingNotificationService Notif, RecordingEmailService Email);

    private static Ctx NewCtx() => new(new InMemoryUnitOfWork(), new RecordingFileStorage(), new RecordingJdStampService(),
        new StubDocumentParser(), new RecordingNotificationService(), new RecordingEmailService());

    private static Task<Result<JobPostingResponse>> Run(
        Ctx c, Guid jobId, UpdateJobStatusRequest req, Guid userId, string role, bool withRubric = true)
    {
        if (withRubric) ARI.Application.UnitTests.CvScoring.CvScoringKit.EnsureRubricsForAllJobs(c.Uow);
        return new UpdateJobStatusCommandHandler(c.Uow, c.Storage, c.Stamp, c.Parser, c.Notif, c.Email, NullLogger<UpdateJobStatusCommandHandler>.Instance)
            .Handle(new UpdateJobStatusCommand(jobId, req, userId, role), CancellationToken.None);
    }

    private static UpdateJobStatusRequest Req(string status, string? reason = null) => JobPostingData.StatusRequest(status, reason);

    // UTCID01 — status rỗng
    [Fact]
    public async Task UTCID01_Status_required()
    {
        var res = await Run(NewCtx(), Guid.NewGuid(), Req(" "), OwnerA, AppRoles.Recruiter);
        Assert.Equal("Status is required.", res.Error);
    }

    // UTCID02 — status không được hỗ trợ
    [Fact]
    public async Task UTCID02_Unsupported_status()
    {
        var res = await Run(NewCtx(), Guid.NewGuid(), Req("unknown"), OwnerA, AppRoles.Recruiter);
        Assert.Equal("Trạng thái không hợp lệ. Sử dụng một trong: draft, pending, active, rejected, closed, archived.", res.Error);
    }

    // UTCID03 — status=draft
    [Fact]
    public async Task UTCID03_Draft_not_allowed()
    {
        var res = await Run(NewCtx(), Guid.NewGuid(), Req("draft"), OwnerA, AppRoles.Recruiter);
        Assert.Equal("Không thể chuyển trạng thái về 'draft'. 'draft' chỉ dùng khi tạo hoặc chỉnh sửa nháp ban đầu.", res.Error);
    }

    // UTCID04 — job không tồn tại
    [Fact]
    public async Task UTCID04_Job_not_found()
    {
        var res = await Run(NewCtx(), Guid.NewGuid(), Req("pending"), OwnerA, AppRoles.Recruiter);
        Assert.True(res.IsFailure);
        Assert.Equal("Job posting not found.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID05 — không có quyền
    [Fact]
    public async Task UTCID05_Unauthorized()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "draft");
        c.Uow.Seed(job);
        var res = await Run(c, job.Id, Req("pending"), Guid.NewGuid(), AppRoles.Recruiter);
        Assert.Equal("Bạn không có quyền thay đổi trạng thái tin tuyển dụng này.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // UTCID06 — trùng trạng thái
    [Fact]
    public async Task UTCID06_Same_status()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: OwnerA, status: "pending");
        c.Uow.Seed(job);
        var res = await Run(c, job.Id, Req("pending"), OwnerA, AppRoles.Recruiter);
        Assert.True(res.IsFailure);
        Assert.Contains("đã ở trạng thái 'pending'", res.Error);
    }

    // UTCID07 — job đã archived
    [Fact]
    public async Task UTCID07_Archived_locked()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: OwnerA, status: "archived");
        c.Uow.Seed(job);
        var res = await Run(c, job.Id, Req("closed"), OwnerA, AppRoles.Recruiter);
        Assert.Equal("Không thể thay đổi trạng thái của tin tuyển dụng đã lưu trữ (archived).", res.Error);
    }

    // UTCID08 — recruiter yêu cầu rejected
    [Fact]
    public async Task UTCID08_Recruiter_cannot_reject()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: OwnerA, status: "pending");
        c.Uow.Seed(job);
        var res = await Run(c, job.Id, Req("rejected", "x"), OwnerA, AppRoles.Recruiter);
        Assert.Equal("Chỉ HrAdmin hoặc SuperAdmin mới có quyền từ chối duyệt bài.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // UTCID09 — admin reject pending nhưng thiếu lý do
    [Fact]
    public async Task UTCID09_Reject_without_reason()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending");
        c.Uow.Seed(job);
        var res = await Run(c, job.Id, Req("rejected", " "), HrA, AppRoles.HrAdmin);
        Assert.Equal("Vui lòng cung cấp lý do từ chối duyệt bài (RejectionReason).", res.Error);
    }

    // UTCID10 — admin activate pending nhưng deadline đã quá khứ
    [Fact]
    public async Task UTCID10_Activate_expired_deadline()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending", deadline: DateTimeOffset.UtcNow.AddDays(-1));
        c.Uow.Seed(job);
        var res = await Run(c, job.Id, Req("active"), HrA, AppRoles.HrAdmin);
        Assert.Equal("Hạn nộp hồ sơ của Job này đã ở quá khứ. Hãy cập nhật lại gia hạn Deadline trước khi chuyển sang Active.", res.Error);
    }

    // UTCID11 — không phải owner mà gửi duyệt (pending)
    [Fact]
    public async Task UTCID11_Non_owner_submit_pending()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "draft");
        c.Uow.Seed(job);
        var res = await Run(c, job.Id, Req("pending"), HrA, AppRoles.HrAdmin);
        Assert.Equal("Chỉ Recruiter/Owner mới có quyền gửi duyệt bài.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // UTCID12 — archive tin còn hồ sơ hoạt động
    [Fact]
    public async Task UTCID12_Archive_with_active_apps()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: OwnerA, status: "active");
        c.Uow.Seed(job).Seed(new ARI.Domain.Entities.Application { JobPostingId = job.Id, CandidateEmail = "a@x.io", Status = "cv_submitted" });
        var res = await Run(c, job.Id, Req("archived"), OwnerA, AppRoles.Recruiter);
        Assert.Equal("Không thể chuyển tin tuyển dụng sang lưu trữ (archived) khi đang có hồ sơ ứng tuyển đang hoạt động.", res.Error);
    }

    // UTCID13 — owner đổi draft → pending → Success, xoá RejectionReason cũ, mở cổng ký cho HM
    [Fact]
    public async Task UTCID13_Draft_to_pending()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: OwnerA, status: "draft");
        job.RejectionReason = "old reason";
        c.Uow.Seed(job);
        var hm = HiringManagerSeed.Primary(c.Uow, job.Id);

        var res = await Run(c, job.Id, Req("pending"), OwnerA, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("pending", job.Status);
        Assert.Null(job.RejectionReason);
        Assert.Equal(HmSignOffStatus.Pending, job.HmSignOffStatus);
        Assert.Contains(c.Uow.Repo<Notification>().Items, n => n.RecipientUserId == hm.Id);
    }

    // UTCID13b — ADR-068: gửi duyệt là gửi cho HM chính; tin thiếu HM thì không gửi được
    [Fact]
    public async Task UTCID13b_Draft_to_pending_without_a_hiring_manager_is_refused()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: OwnerA, status: "draft");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, Req("pending"), OwnerA, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(JobAccessErrors.HiringManagerMissing, res.Error);
        Assert.Equal("draft", job.Status);
    }

    // UTCID13c — gửi duyệt KHÔNG còn báo "chờ duyệt" cho HR (ADR-063: HR không duyệt đăng nữa)
    [Fact]
    public async Task UTCID13c_Resubmitting_tells_only_the_hiring_manager()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: OwnerA, status: "rejected");
        c.Uow.Seed(job).Seed(JobPostingData.Staff(HrA, role: "hr_admin"));
        var hm = HiringManagerSeed.Primary(c.Uow, job.Id);

        var res = await Run(c, job.Id, Req("pending"), OwnerA, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        var notices = c.Uow.Repo<Notification>().Items;
        Assert.Contains(notices, n => n.RecipientUserId == hm.Id);
        Assert.DoesNotContain(notices, n => n.RecipientUserId == HrA);
    }

    // UTCID14 — admin đăng pending → active khi HM chưa ký: bắt buộc lý do vượt cổng, cổng ghi `bypassed`
    [Fact]
    public async Task UTCID14_Admin_publishing_without_hm_signature_needs_a_bypass_reason()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending");
        job.HmSignOffStatus = HmSignOffStatus.Pending;
        c.Uow.Seed(job).Seed(JobPostingData.Staff(HrA, role: "hr_admin"));
        var hm = HiringManagerSeed.Primary(c.Uow, job.Id);

        var refused = await Run(c, job.Id, Req("active"), HrA, AppRoles.HrAdmin);
        Assert.True(refused.IsFailure);
        Assert.Equal("pending", job.Status);

        var req = Req("active");
        req.HmBypassReason = "Hiring Manager nghỉ phép, vị trí cần đăng gấp";
        var res = await Run(c, job.Id, req, HrA, AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal("active", job.Status);
        Assert.NotNull(job.PublishedAt);
        Assert.Equal(HrA, job.ApprovedByUserId);
        Assert.NotNull(job.ApprovedAt);
        Assert.False(string.IsNullOrEmpty(job.ApproverName));
        // Ghi `bypassed` chứ không để nguyên `pending` — nếu không, HM vẫn thấy nút ký trên tin đã đăng.
        Assert.Equal(HmSignOffStatus.Bypassed, job.HmSignOffStatus);
        Assert.Contains(c.Uow.Repo<Notification>().Items, n => n.RecipientUserId == hm.Id);
    }

    // UTCID14b — ADR-068: bản nháp CHƯA từng gửi ký (cổng null) cũng không được đăng thẳng mà không lý do
    [Fact]
    public async Task UTCID14b_Admin_cannot_publish_a_draft_straight_without_a_bypass_reason()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "draft");
        c.Uow.Seed(job).Seed(JobPostingData.Staff(HrA, role: "hr_admin"));
        HiringManagerSeed.Primary(c.Uow, job.Id);

        var res = await Run(c, job.Id, Req("active"), HrA, AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Equal("draft", job.Status);
    }

    // UTCID15 — owner đóng tin active → closed → Success
    [Fact]
    public async Task UTCID15_Close_active()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: OwnerA, status: "active");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, Req("closed"), OwnerA, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("closed", job.Status);
    }
}
