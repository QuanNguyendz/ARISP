using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ARI.Application.Jobs.Commands.UpdateJobDisplay
{
    public record UpdateJobDisplayCommand(Guid Id, UpdateJobDisplayRequest Request, Guid UserId, string? Role)
        : IRequest<Result<JobPostingResponse>>;

    public class UpdateJobDisplayCommandHandler : IRequestHandler<UpdateJobDisplayCommand, Result<JobPostingResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateJobDisplayCommandHandler> _logger;
        private readonly INotificationService _notificationService;

        public UpdateJobDisplayCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<UpdateJobDisplayCommandHandler> logger,
            INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<Result<JobPostingResponse>> Handle(UpdateJobDisplayCommand command, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(command.Id, ct);
            if (job == null)
                return Result.Failure<JobPostingResponse>("Job posting not found.", CommonErrorCodes.NotFound);

            var isSuperOrHrAdmin = RoleNames.IsAdmin(command.Role);
            var isOwner = job.CreatedByUserId == command.UserId;

            if (!isSuperOrHrAdmin && !isOwner)
                return Result.Failure<JobPostingResponse>("Bạn không có quyền cập nhật hiển thị tin tuyển dụng này.", CommonErrorCodes.Forbidden);

            // HR Admin can toggle anything at any time.
            // Recruiter can only toggle IsUrgent at any time.
            // Recruiter can only toggle IsPublicListing if job is draft.

            if (command.Request.IsUrgent.HasValue)
            {
                job.IsUrgent = command.Request.IsUrgent.Value;
            }

            if (command.Request.IsPublicListing.HasValue)
            {
                if (!isSuperOrHrAdmin && job.Status != "draft")
                {
                    return Result.Failure<JobPostingResponse>("Chuyên viên tuyển dụng không được quyền bật/tắt Public khi tin đã được gửi duyệt.", CommonErrorCodes.Forbidden);
                }
                job.IsPublicListing = command.Request.IsPublicListing.Value;
            }

            job.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<JobPosting>().Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            // Publish event if active job changed
            if (job.Status == "active")
            {
                await _notificationService.PublishAllEventAsync("ReceivePublicJobUpdate", new { JobId = job.Id, Status = job.Status }, ct);
            }

            var rounds = await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == job.Id, ct);
            var roundDtos = rounds.Select(RoundConfigDto.FromEntity).OrderBy(r => r.RoundNumber).ToList();

            return Result.Success(JobPostingResponse.FromEntity(job, roundDtos));
        }
    }
}
