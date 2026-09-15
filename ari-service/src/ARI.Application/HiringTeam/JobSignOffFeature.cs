using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Jobs.Commands.UpdateJobStatus;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.HiringTeam
{
    /// <summary>
    /// Hiring Manager ký duyệt bản mô tả công việc trước khi tin được đăng (ADR-061, cổng 3b; ADR-063:
    /// chữ ký này LÀ cổng đăng tin).
    ///
    /// <b>Yêu cầu sửa trả tin về cho Recruiter</b> (ADR-068): tin sang <c>rejected</c> kèm góp ý, đúng
    /// vòng <c>rejected → sửa → pending</c> sẵn có, và gửi lại thì cổng tự về <c>pending</c>. Trước đây
    /// lệnh này chỉ ghi cột chữ ký mà để tin nằm nguyên ở <c>pending</c> — trong khi Recruiter chỉ sửa
    /// được tin <c>draft</c>/<c>rejected</c>, nên tin kẹt cho tới khi có HR Admin đi từ chối hộ.
    ///
    /// <b>Ký duyệt = đăng tin, được ăn cả ngã về không.</b> Chữ ký chỉ được ghi khi tin đăng được; đăng
    /// hỏng (hạn nộp đã qua, ngân hàng đề thiếu câu) thì chữ ký trả về như cũ và HM được gợi ý yêu cầu
    /// sửa. Trước đây chữ ký lưu trước, đăng sau — hỏng là tin mắc ở "đã ký mà chưa đăng", Recruiter
    /// không sửa được (tin vẫn <c>pending</c>) còn HM không ký lại được (cổng đã <c>approved</c>).
    /// </summary>
    public record JobHmSignOffCommand(Guid JobPostingId, string Decision, string? Reason, Guid? ActorId, string? ActorRole)
        : IRequest<Result<bool>>;

    public class JobHmSignOffCommandHandler : IRequestHandler<JobHmSignOffCommand, Result<bool>>
    {
        /// <summary>Cùng ngưỡng với mọi lý do trả về / vượt cổng khác (phiếu, shortlist, đăng vượt cổng).</summary>
        public const int MinReasonLength = 10;

        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;
        private readonly ISender _sender;

        public JobHmSignOffCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications, ISender sender)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
            _sender = sender;
        }

        public async Task<Result<bool>> Handle(JobHmSignOffCommand request, CancellationToken ct)
        {
            var decision = (request.Decision ?? string.Empty).Trim().ToLowerInvariant();
            if (decision != HmSignOffStatus.Approved && decision != HmSignOffStatus.Rejected)
                return Result<bool>.Failure("Quyết định phải là 'approved' hoặc 'rejected'.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
            if (job == null)
                return Result<bool>.Failure("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            if (!await JobAccess.IsPrimaryHiringManagerAsync(_unitOfWork, job.Id, request.ActorId, ct))
                return Result<bool>.Failure(
                    "Chỉ Hiring Manager phụ trách tin này mới ký duyệt được.", CommonErrorCodes.Forbidden);

            // Ký chỉ có nghĩa với một tin ĐANG chờ ký. Tin HR đã trả về (`rejected`) mà cổng còn ghi
            // `pending` thì trước đây vẫn ký được — và lượt đăng sau đó hỏng vì `rejected → active` không
            // hợp lệ, để lại chữ ký `approved` trên một tin chưa đăng.
            var jobPending = string.Equals(job.Status?.Trim(), "pending", StringComparison.OrdinalIgnoreCase);
            if (!jobPending || !HmSignOffStatus.Is(job.HmSignOffStatus, HmSignOffStatus.Pending))
                return Result<bool>.Failure("Tin này không ở trạng thái chờ bạn ký duyệt.", CommonErrorCodes.Conflict);

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            return decision == HmSignOffStatus.Approved
                ? await ApproveAsync(job, request, ct)
                : await RequestChangesAsync(job, reason, request, ct);
        }

        private async Task<Result<bool>> ApproveAsync(JobPosting job, JobHmSignOffCommand request, CancellationToken ct)
        {
            // Giữ lại để trả về nguyên trạng nếu bước đăng hỏng. Không dựa vào việc "chưa SaveChanges
            // thì không có gì được lưu": thực thể đang được theo dõi đã bị sửa trong bộ nhớ, và bất kỳ
            // lượt lưu nào sau đó trong cùng phạm vi sẽ ghi luôn chữ ký dở dang này.
            var before = (job.HmSignOffStatus, job.HmSignOffByUserId, job.HmSignOffAt, job.HmSignOffReason);

            job.HmSignOffStatus = HmSignOffStatus.Approved;
            job.HmSignOffByUserId = request.ActorId;
            job.HmSignOffAt = DateTimeOffset.UtcNow;
            job.HmSignOffReason = null;

            // ADR-063: đăng bằng cách gọi lại chính `UpdateJobStatusCommand` chứ không nhân bản — nhánh
            // `→ active` ở đó còn ghi người duyệt, đóng dấu duyệt lên file JD, đặt `PublishedAt` và báo cho
            // người tạo tin. Lệnh đó lưu MỘT lần, gồm cả chữ ký vừa gán ở trên: ký và đăng là một giao dịch.
            var publish = await _sender.Send(new UpdateJobStatusCommand(
                job.Id,
                new UpdateJobStatusRequest { Status = "active" },
                request.ActorId ?? Guid.Empty,
                request.ActorRole), ct);

            if (publish.IsFailure)
            {
                (job.HmSignOffStatus, job.HmSignOffByUserId, job.HmSignOffAt, job.HmSignOffReason) = before;
                var message = $"Chưa đăng được tin nên chữ ký chưa được ghi: {publish.Error} "
                              + "Nếu tin cần sửa (ví dụ hạn nộp hồ sơ), hãy chọn \"Yêu cầu sửa\" để Recruiter cập nhật.";
                return publish.ErrorCode == null
                    ? Result<bool>.Failure(message)
                    : Result<bool>.Failure(message, publish.ErrorCode);
            }

            // Tin đã đăng (và lưu). Dấu vết ghi ở lượt lưu thứ hai: nó không thể làm hỏng lại việc đăng.
            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "job_hm_signoff_approved",
                nameof(JobPosting), job.Id, AuditMetadata.Serialize(new { jobTitle = job.Title }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Thông báo "tin đã được duyệt" cho người tạo tin do `UpdateJobStatusCommand` gửi (tên người
            // duyệt là HM) — không gửi thêm một bản thứ hai ở đây.
            await _notifications.PublishUserEventAsync(job.CreatedByUserId, "ReceiveUserNotification",
                new { Type = "JobHmSignOff", JobPostingId = job.Id, Decision = HmSignOffStatus.Approved }, ct);

            return Result.Success(true);
        }

        private async Task<Result<bool>> RequestChangesAsync(
            JobPosting job, string? reason, JobHmSignOffCommand request, CancellationToken ct)
        {
            if (reason == null || reason.Length < MinReasonLength)
                return Result<bool>.Failure(
                    $"Vui lòng nêu rõ cần sửa gì trong mô tả công việc (tối thiểu {MinReasonLength} ký tự).");

            // Trả tin về cho Recruiter: `rejected` là trạng thái DUY NHẤT họ sửa và gửi lại được. Góp ý
            // ghi ở cả hai cột — `RejectionReason` là thứ màn tin của Recruiter vốn hiện thành banner, còn
            // cột chữ ký giữ dấu vết "ai yêu cầu sửa, lúc nào" để phân biệt với HR từ chối.
            job.Status = "rejected";
            job.RejectionReason = reason;
            job.HmSignOffStatus = HmSignOffStatus.Rejected;
            job.HmSignOffByUserId = request.ActorId;
            job.HmSignOffAt = DateTimeOffset.UtcNow;
            job.HmSignOffReason = reason;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<JobPosting>().Update(job);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "job_hm_signoff_rejected",
                nameof(JobPosting), job.Id, AuditMetadata.Serialize(new { jobTitle = job.Title, reason }), ct);

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = job.CreatedByUserId,
                Type = "rejected",
                Title = "Hiring Manager yêu cầu sửa mô tả công việc",
                Body = $"Tin \"{job.Title}\" đã được trả về để bạn sửa rồi gửi duyệt lại. Góp ý: {reason}",
                Link = await StaffLinks.JobAsync(_unitOfWork, job.CreatedByUserId, job.Id, ct),
                DedupKey = $"job_hm_signoff_decided:{job.Id}:{DateTimeOffset.UtcNow.Ticks}",
                IsRead = false,
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(job.CreatedByUserId, "ReceiveUserNotification",
                new { Type = "JobHmSignOff", JobPostingId = job.Id, Decision = HmSignOffStatus.Rejected }, ct);
            await _notifications.PublishUserEventAsync(job.CreatedByUserId, "ReceiveJobPostingUpdate",
                new { JobId = job.Id, Status = "rejected", Title = job.Title }, ct);

            return Result.Success(true);
        }
    }
}
