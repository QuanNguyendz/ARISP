using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.HiringTeam;
using ARI.Application.Jobs.Commands.UpdateJobStatus;
using ARI.Application.UnitTests.JobPostings;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ARI.Application.UnitTests.HiringTeam;

/// <summary>
/// Hiring Manager ký duyệt mô tả công việc (ADR-061/063) — và vòng "yêu cầu sửa" của ADR-068.
///
/// Trước ADR-068 lệnh này không có test nào, và nó có một ngõ cụt: "yêu cầu sửa" để tin nằm nguyên ở
/// <c>pending</c> trong khi Recruiter chỉ sửa được tin <c>draft</c>/<c>rejected</c> — tin kẹt cho tới khi có HR
/// Admin đi từ chối hộ. Bộ test này khoá cả vòng: yêu cầu sửa → Recruiter sửa → gửi lại → HM ký → tin đăng.
/// </summary>
public class JobSignOffTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _hmId = Guid.NewGuid();

    private const string ChangeReason = "Mục quyền lợi còn thiếu chế độ bảo hiểm";

    /// <summary>
    /// <see cref="ISender"/> tối thiểu: chỉ chuyển <see cref="UpdateJobStatusCommand"/> tới đúng handler thật —
    /// ký duyệt gọi lại lệnh đó để đăng tin (ADR-063), và đó chính là thứ cần kiểm chung một giao dịch.
    /// </summary>
    private sealed class PublishSender : ISender
    {
        private readonly InMemoryUnitOfWork _uow;
        private readonly RecordingNotificationService _notif;

        public PublishSender(InMemoryUnitOfWork uow, RecordingNotificationService notif) { _uow = uow; _notif = notif; }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is not UpdateJobStatusCommand cmd)
                throw new NotSupportedException(request.GetType().Name);

            var result = await new UpdateJobStatusCommandHandler(
                    _uow, new RecordingFileStorage(), new RecordingJdStampService(), new StubDocumentParser(),
                    _notif, new RecordingEmailService(), NullLogger<UpdateJobStatusCommandHandler>.Instance)
                .Handle(cmd, cancellationToken);
            return (TResponse)(object)result;
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private (InMemoryUnitOfWork uow, JobPosting job) Seed(
        string status = "pending", string? signOff = HmSignOffStatus.Pending, DateTimeOffset? deadline = null)
    {
        var job = JobPostingData.Job(_ownerId, status: status, deadline: deadline);
        job.HmSignOffStatus = signOff;
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(new User { Id = _ownerId, Email = "owner@corp.io", Role = RoleNames.Recruiter, FullName = "Recruiter A", IsActive = true });
        HiringManagerSeed.Primary(uow, job.Id, _hmId, _ownerId);
        return (uow, job);
    }

    private Task<Result<bool>> SignOff(
        InMemoryUnitOfWork uow, Guid jobId, string decision, string? reason = null,
        Guid? actor = null, string role = AppRoles.HiringManager)
    {
        var notif = new RecordingNotificationService();
        return new JobHmSignOffCommandHandler(uow, notif, new PublishSender(uow, notif))
            .Handle(new JobHmSignOffCommand(jobId, decision, reason, actor ?? _hmId, role), CancellationToken.None);
    }

    private Task<Result<JobPostingResponse>> Resubmit(InMemoryUnitOfWork uow, Guid jobId)
        => new UpdateJobStatusCommandHandler(
                uow, new RecordingFileStorage(), new RecordingJdStampService(), new StubDocumentParser(),
                new RecordingNotificationService(), new RecordingEmailService(), NullLogger<UpdateJobStatusCommandHandler>.Instance)
            .Handle(new UpdateJobStatusCommand(jobId, new UpdateJobStatusRequest { Status = "pending" }, _ownerId, AppRoles.Recruiter),
                CancellationToken.None);

    // ===== Yêu cầu sửa =====

    [Fact]
    public async Task Request_changes_returns_the_job_to_the_recruiter_as_rejected()
    {
        var (uow, job) = Seed();

        var res = await SignOff(uow, job.Id, HmSignOffStatus.Rejected, ChangeReason);

        Assert.True(res.IsSuccess);
        // `rejected` là trạng thái DUY NHẤT Recruiter sửa và gửi lại được.
        Assert.Equal("rejected", job.Status);
        Assert.Equal(ChangeReason, job.RejectionReason);
        Assert.Equal(HmSignOffStatus.Rejected, job.HmSignOffStatus);
        Assert.Equal(_hmId, job.HmSignOffByUserId);
        var notice = Assert.Single(uow.Repo<Notification>().Items, n => n.RecipientUserId == _ownerId);
        Assert.Equal($"/recruiter/my-jobs/{job.Id}", notice.Link); // link đúng workspace của người nhận
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "job_hm_signoff_rejected");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("sửa đi")]
    public async Task Request_changes_needs_a_real_reason(string? reason)
    {
        var (uow, job) = Seed();

        var res = await SignOff(uow, job.Id, HmSignOffStatus.Rejected, reason);

        Assert.True(res.IsFailure);
        Assert.Equal("pending", job.Status);
        Assert.Equal(HmSignOffStatus.Pending, job.HmSignOffStatus);
    }

    [Fact]
    public async Task After_request_changes_the_recruiter_resubmits_and_the_gate_reopens()
    {
        // Đúng vòng sẵn có `rejected → sửa → pending`: gửi lại thì góp ý cũ bị xoá và cổng về `pending`.
        var (uow, job) = Seed();
        await SignOff(uow, job.Id, HmSignOffStatus.Rejected, ChangeReason);

        var res = await Resubmit(uow, job.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal("pending", job.Status);
        Assert.Null(job.RejectionReason);
        Assert.Equal(HmSignOffStatus.Pending, job.HmSignOffStatus);
        Assert.Null(job.HmSignOffReason);
        Assert.Contains(uow.Repo<Notification>().Items,
            n => n.RecipientUserId == _hmId && n.DedupKey!.StartsWith($"job_hm_signoff:{job.Id}:"));
    }

    // ===== Ký duyệt =====

    [Fact]
    public async Task Approving_publishes_the_job_and_records_the_signature()
    {
        var (uow, job) = Seed();

        var res = await SignOff(uow, job.Id, HmSignOffStatus.Approved);

        Assert.True(res.IsSuccess);
        Assert.Equal("active", job.Status);
        Assert.NotNull(job.PublishedAt);
        Assert.Equal(HmSignOffStatus.Approved, job.HmSignOffStatus);
        Assert.Equal(_hmId, job.HmSignOffByUserId);
        Assert.Equal(_hmId, job.ApprovedByUserId);
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "job_hm_signoff_approved");
    }

    [Fact]
    public async Task Approving_a_job_that_cannot_be_published_records_nothing()
    {
        // Được ăn cả ngã về không: trước đây chữ ký lưu trước, đăng sau — hạn nộp đã qua là tin mắc ở "đã ký
        // mà chưa đăng", Recruiter không sửa được (tin vẫn pending) còn HM không ký lại được (cổng approved).
        var (uow, job) = Seed(deadline: DateTimeOffset.UtcNow.AddDays(-1));

        var res = await SignOff(uow, job.Id, HmSignOffStatus.Approved);

        Assert.True(res.IsFailure);
        Assert.Contains("Yêu cầu sửa", res.Error);
        Assert.Equal("pending", job.Status);
        Assert.Equal(HmSignOffStatus.Pending, job.HmSignOffStatus);
        Assert.Null(job.HmSignOffByUserId);
        Assert.DoesNotContain(uow.Repo<AuditLog>().Items, a => a.Action == "job_hm_signoff_approved");
    }

    [Fact]
    public async Task A_job_hr_already_returned_cannot_be_signed()
    {
        // HR từ chối tin đang chờ mà cổng vẫn ghi `pending`: trước đây HM vẫn ký được, rồi lượt đăng hỏng vì
        // `rejected → active` không hợp lệ — để lại chữ ký `approved` trên một tin chưa đăng.
        var (uow, job) = Seed(status: "rejected");

        var res = await SignOff(uow, job.Id, HmSignOffStatus.Approved);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Equal("rejected", job.Status);
        Assert.Equal(HmSignOffStatus.Pending, job.HmSignOffStatus);
    }

    [Theory]
    [InlineData(AppRoles.HiringManager)] // HM của tin khác
    [InlineData(AppRoles.HrAdmin)]       // quản trị viên có đường riêng: đăng vượt cổng kèm lý do
    public async Task Only_the_jobs_primary_hiring_manager_signs(string role)
    {
        var (uow, job) = Seed();

        var res = await SignOff(uow, job.Id, HmSignOffStatus.Approved, actor: Guid.NewGuid(), role: role);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Equal("pending", job.Status);
    }

    [Fact]
    public async Task A_bypassed_job_is_no_longer_waiting_for_a_signature()
    {
        var (uow, job) = Seed(status: "active", signOff: HmSignOffStatus.Bypassed);

        var res = await SignOff(uow, job.Id, HmSignOffStatus.Approved);

        Assert.True(res.IsFailure);
        Assert.Equal(HmSignOffStatus.Bypassed, job.HmSignOffStatus);
    }
}
