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
    /// Hiring Manager ký duyệt bản mô tả công việc trước khi tin được đăng (ADR-061, cổng 3b).
    ///
    /// Nằm NGOÀI máy trạng thái của tin: ký duyệt và vòng đời tin là hai trục vuông góc — tin có
    /// thể đã ký mà vẫn ở <c>draft</c>/<c>pending</c>/<c>rejected</c>. Từ chối ở đây KHÔNG đổi
    /// <c>job.Status</c>: Recruiter sửa rồi gửi duyệt lại qua đúng đường <c>rejected|draft → pending</c>
    /// sẵn có, và đường đó tự đặt lại cổng về <c>pending</c>.
    /// </summary>
    public record JobHmSignOffCommand(Guid JobPostingId, string Decision, string? Reason, Guid? ActorId, string? ActorRole)
        : IRequest<Result<bool>>;

    public class JobHmSignOffCommandHandler : IRequestHandler<JobHmSignOffCommand, Result<bool>>
    {
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

            if (!await ShortlistGateSupport.IsPrimaryHmAsync(_unitOfWork, job.Id, request.ActorId, ct))
                return Result<bool>.Failure(
                    "Chỉ Hiring Manager phụ trách tin này mới ký duyệt được.", CommonErrorCodes.Forbidden);

            if (!HmSignOffStatus.Is(job.HmSignOffStatus, HmSignOffStatus.Pending))
                return Result<bool>.Failure("Tin này không ở trạng thái chờ bạn ký duyệt.");

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (decision == HmSignOffStatus.Rejected && reason == null)
                return Result<bool>.Failure("Vui lòng nêu rõ cần sửa gì trong mô tả công việc.");

            job.HmSignOffStatus = decision;
            job.HmSignOffByUserId = request.ActorId;
            job.HmSignOffAt = DateTimeOffset.UtcNow;
            job.HmSignOffReason = reason;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<JobPosting>().Update(job);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId,
                decision == HmSignOffStatus.Approved ? "job_hm_signoff_approved" : "job_hm_signoff_rejected",
                nameof(JobPosting), job.Id,
                AuditMetadata.Serialize(new { jobTitle = job.Title, reason }), ct);

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = job.CreatedByUserId,
                Type = decision == HmSignOffStatus.Approved ? "approved" : "rejected",
                Title = decision == HmSignOffStatus.Approved
                    ? "Hiring Manager đã ký duyệt tin"
                    : "Hiring Manager yêu cầu sửa mô tả công việc",
                Body = $"Tin \"{job.Title}\"." + (reason != null ? $" Góp ý: {reason}" : string.Empty),
                Link = $"/recruiter/my-jobs/{job.Id}",
                DedupKey = $"job_hm_signoff_decided:{job.Id}:{DateTimeOffset.UtcNow.Ticks}",
                IsRead = false,
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(job.CreatedByUserId, "ReceiveUserNotification",
                new { Type = "JobHmSignOff", JobPostingId = job.Id, Decision = decision }, ct);

            // ADR-063: chữ ký của Hiring Manager LÀ cổng đăng tin — không còn bước duyệt riêng của
            // HR Leader. Đăng bằng cách gọi lại chính `UpdateJobStatusCommand` chứ không nhân bản:
            // nhánh `→ active` ở đó còn ghi người duyệt, đóng dấu duyệt lên file JD, đặt
            // `PublishedAt` và báo cho người tạo tin. Chép tay bốn việc đó sang đây là bảo đảm sẽ
            // lệch — cùng lý lẽ với ADR-059 khi duyệt CV gọi lại `AssignSlotCommand`.
            //
            // Chạy SAU khi đã lưu chữ ký: nếu bước đăng hỏng (hạn nộp hồ sơ đã qua, ngân hàng đề
            // trắc nghiệm chưa đủ câu), chữ ký vẫn còn nguyên và thông báo dưới đây nói rõ phải
            // sửa gì rồi đăng lại — thay vì mất cả hai.
            // Chỉ đăng khi tin đang chờ (`pending`/`draft`/`rejected`). Tin đã `active`, `closed`
            // hay `archived` thì ký lại không được kéo ngược trạng thái.
            var statusNow = (job.Status ?? string.Empty).Trim().ToLowerInvariant();
            var awaitingPublish = statusNow is "pending" or "draft" or "rejected";

            if (decision == HmSignOffStatus.Approved && awaitingPublish)
            {
                var publish = await _sender.Send(new UpdateJobStatusCommand(
                    job.Id,
                    new UpdateJobStatusRequest { Status = "active" },
                    request.ActorId ?? Guid.Empty,
                    request.ActorRole), ct);

                if (publish.IsFailure)
                    return Result<bool>.Failure(
                        $"Đã ghi nhận chữ ký duyệt nhưng chưa đăng được tin: {publish.Error}");
            }

            return Result.Success(true);
        }
    }
}
