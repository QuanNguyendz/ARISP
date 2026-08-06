using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.UpdateJobStatus;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Luồng 2 — Approve Job Posting (UC-48/78/79/80, <see cref="UpdateJobStatusCommandHandler"/>): workflow
/// draft→pending→active|rejected, phân quyền Owner (gửi duyệt) vs HrAdmin/SuperAdmin (duyệt/từ chối), bắt
/// buộc lý do từ chối, đóng dấu duyệt PDF (best-effort không chặn), thông báo người tạo + nhóm hr_admin.
/// </summary>
public class UpdateJobStatusCommandHandlerTests
{
    private sealed record Ctx(
        InMemoryUnitOfWork Uow, RecordingFileStorage Storage, RecordingJdStampService Stamp,
        StubDocumentParser Parser, RecordingNotificationService Notif, RecordingEmailService Email);

    private static Ctx NewCtx() => new(
        new InMemoryUnitOfWork(), new RecordingFileStorage(), new RecordingJdStampService(),
        new StubDocumentParser(), new RecordingNotificationService(), new RecordingEmailService());

    private static Task<Result<JobPostingResponse>> Run(Ctx c, Guid jobId, UpdateJobStatusRequest req, Guid userId, string? role)
        => new UpdateJobStatusCommandHandler(c.Uow, c.Storage, c.Stamp, c.Parser, c.Notif, c.Email, NullLogger<UpdateJobStatusCommandHandler>.Instance)
            .Handle(new UpdateJobStatusCommand(jobId, req, userId, role), CancellationToken.None);

    // ---------- Guards ----------

    [Fact]
    public async Task Empty_status_fails()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest(""), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("Status is required", res.Error);
    }

    [Fact]
    public async Task Invalid_status_fails()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("foobar"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("không hợp lệ", res.Error);
    }

    [Fact]
    public async Task Cannot_transition_back_to_draft()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("draft"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("draft", res.Error);
    }

    [Fact]
    public async Task Job_not_found_fails()
    {
        var res = await Run(NewCtx(), Guid.NewGuid(), JobPostingData.StatusRequest("pending"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Same_status_fails()
    {
        var c = NewCtx();
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "active");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), userId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Contains("đã ở trạng thái", res.Error);
    }

    [Fact]
    public async Task Archived_job_cannot_change_status()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "archived");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("lưu trữ", res.Error);
    }

    [Fact]
    public async Task Unauthorized_user_is_forbidden()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "active");
        c.Uow.Seed(job);

        // Không phải chủ tin, không phải admin → chặn ngay ở cổng chung.
        var res = await Run(c, job.Id, JobPostingData.StatusRequest("closed"), Guid.NewGuid(), AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ---------- UC-48: Submit for Approval (→ pending) ----------

    [Fact]
    public async Task Owner_submits_draft_to_pending()
    {
        var c = NewCtx();
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "draft");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("pending"), userId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("pending", job.Status);
        Assert.Contains(c.Notif.GroupEvents, e => e.Group == "hr_admin" && e.EventType == "ReceiveJobPostingUpdate");
    }

    [Fact]
    public async Task Non_owner_cannot_submit_for_approval()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "draft");
        c.Uow.Seed(job);

        // HrAdmin nhưng không phải chủ tin → không được gửi duyệt (chỉ Owner mới gửi).
        var res = await Run(c, job.Id, JobPostingData.StatusRequest("pending"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("Recruiter/Owner", res.Error);
    }

    [Fact]
    public async Task Pending_only_from_draft_or_rejected()
    {
        var c = NewCtx();
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "active");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("pending"), userId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Contains("gửi duyệt", res.Error);
    }

    [Fact]
    public async Task Rejected_job_can_be_resubmitted_and_clears_reason()
    {
        var c = NewCtx();
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "rejected");
        job.RejectionReason = "Thiếu mô tả";
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("pending"), userId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("pending", job.Status);
        Assert.Null(job.RejectionReason);
    }

    [Fact]
    public async Task Submitting_notifies_hr_admins_with_notification_record()
    {
        var c = NewCtx();
        var userId = Guid.NewGuid();
        var hrAdmin = JobPostingData.Staff(Guid.NewGuid(), role: "hr_admin"); // query dùng role chữ thường
        var job = JobPostingData.Job(owner: userId, status: "draft");
        c.Uow.Seed(hrAdmin).Seed(job);

        await Run(c, job.Id, JobPostingData.StatusRequest("pending"), userId, AppRoles.Recruiter);

        Assert.Contains(c.Uow.Repo<Notification>().Items, n => n.RecipientUserId == hrAdmin.Id && n.Type == "pending");
    }

    // ---------- UC-78: Approve (→ active) ----------

    [Fact]
    public async Task Admin_approves_pending_and_notifies_creator()
    {
        var c = NewCtx();
        var adminId = Guid.NewGuid();
        var creator = JobPostingData.Staff(Guid.NewGuid(), "recruiter");
        var job = JobPostingData.Job(owner: creator.Id, status: "pending");
        c.Uow.Seed(creator).Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), adminId, AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal("active", job.Status);
        Assert.Equal(adminId, job.ApprovedByUserId);
        Assert.NotNull(job.PublishedAt);
        Assert.Contains("ReceivePublicJobUpdate", c.Notif.AllEvents);
        Assert.Contains(c.Notif.UserEvents, e => e.UserId == creator.Id && e.EventType == "ReceiveJobPostingUpdate");
        Assert.Contains(c.Uow.Repo<Notification>().Items, n => n.RecipientUserId == creator.Id && n.Type == "approved");
    }

    [Fact]
    public async Task Non_admin_cannot_approve()
    {
        var c = NewCtx();
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "pending");
        c.Uow.Seed(job);

        // Owner (Recruiter) tự duyệt tin mình → không được (chỉ HrAdmin/SuperAdmin).
        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), userId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Contains("HrAdmin hoặc SuperAdmin", res.Error);
    }

    [Fact]
    public async Task Approve_from_invalid_status_fails()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "rejected");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("kích hoạt", res.Error);
    }

    [Fact]
    public async Task Approve_with_past_deadline_fails()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending", deadline: DateTimeOffset.UtcNow.AddDays(-1));
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("Hạn nộp hồ sơ", res.Error);
    }

    [Fact]
    public async Task Reactivating_closed_job_does_not_re_approve()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "closed");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal("active", job.Status);
        Assert.Null(job.ApprovedByUserId); // closed→active không phải phê duyệt lần đầu
    }

    // ---------- UC-79: Reject (→ rejected) ----------

    [Fact]
    public async Task Admin_rejects_pending_with_reason_and_notifies_creator()
    {
        var c = NewCtx();
        var creator = JobPostingData.Staff(Guid.NewGuid(), "recruiter");
        var job = JobPostingData.Job(owner: creator.Id, status: "pending");
        c.Uow.Seed(creator).Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("rejected", "JD chưa rõ ràng"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal("rejected", job.Status);
        Assert.Equal("JD chưa rõ ràng", job.RejectionReason);
        Assert.Contains(c.Uow.Repo<Notification>().Items, n => n.RecipientUserId == creator.Id && n.Type == "rejected");
    }

    [Fact]
    public async Task Non_admin_cannot_reject()
    {
        var c = NewCtx();
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "pending");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("rejected", "lý do"), userId, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Reject_requires_reason()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("rejected", reason: null), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("lý do từ chối", res.Error);
    }

    [Fact]
    public async Task Reject_only_from_pending()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "active");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("rejected", "lý do"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("từ chối", res.Error);
    }

    // ---------- UC-80: Generate Approved Job PDF (đóng dấu duyệt) ----------

    [Fact]
    public async Task Approving_pdf_job_stamps_and_sets_signed_url()
    {
        var c = NewCtx();
        c.Storage.FileBytes = new byte[] { 1, 2, 3 }; // file JD gốc đọc được
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending", jdFileUrl: "jd/original.pdf", jdFileFormat: "pdf");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, c.Stamp.StampPdfCallCount);
        Assert.NotNull(job.SignedJdFileUrl);
    }

    [Fact]
    public async Task Approving_docx_job_stamps_from_text()
    {
        var c = NewCtx();
        c.Storage.FileBytes = new byte[] { 1, 2, 3 };
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending", jdFileUrl: "jd/original.docx", jdFileFormat: "docx");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, c.Stamp.StampFromTextCallCount);
        Assert.Equal(0, c.Stamp.StampPdfCallCount);
        Assert.NotNull(job.SignedJdFileUrl);
    }

    [Fact]
    public async Task Stamp_failure_does_not_block_approval()
    {
        var c = NewCtx();
        c.Storage.FileBytes = new byte[] { 1, 2, 3 };
        c.Stamp.ThrowOnStamp = true;
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending", jdFileUrl: "jd/original.pdf", jdFileFormat: "pdf");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);          // vẫn duyệt được
        Assert.Equal("active", job.Status);
        Assert.Null(job.SignedJdFileUrl);    // không có bản đóng dấu
    }

    [Fact]
    public async Task Approving_job_without_jd_file_skips_stamp()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "pending"); // không có file JD
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("active"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, c.Stamp.StampPdfCallCount);
        Assert.Equal(0, c.Stamp.StampFromTextCallCount);
    }

    // ---------- Đóng / Lưu trữ (cùng handler) ----------

    [Fact]
    public async Task Owner_closes_active_job()
    {
        var c = NewCtx();
        var userId = Guid.NewGuid();
        var job = JobPostingData.Job(owner: userId, status: "active");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("closed"), userId, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("closed", job.Status);
    }

    [Fact]
    public async Task Archive_blocked_by_active_applications()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "closed");
        c.Uow.Seed(job).Seed(new ARI.Domain.Entities.Application { JobPostingId = job.Id, Status = "screening", CandidateEmail = "a@b.io" });

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("archived"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsFailure);
        Assert.Contains("hồ sơ ứng tuyển đang hoạt động", res.Error);
    }

    [Fact]
    public async Task Archive_soft_deletes_when_no_active_applications()
    {
        var c = NewCtx();
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "closed");
        c.Uow.Seed(job);

        var res = await Run(c, job.Id, JobPostingData.StatusRequest("archived"), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        Assert.Equal("archived", job.Status);
        Assert.NotNull(job.DeletedAt);
    }
}
