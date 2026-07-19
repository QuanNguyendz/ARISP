using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Commands.RejectAccountRequest
{
    public record RejectAccountRequestCommand(Guid Id, string? Reason, Guid? ActorId) : IRequest<Result>;

    public class RejectAccountRequestCommandHandler : IRequestHandler<RejectAccountRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public RejectAccountRequestCommandHandler(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        public async Task<Result> Handle(RejectAccountRequestCommand request, CancellationToken ct)
        {
            var reason = request.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure("Vui lòng nhập lý do từ chối.");

            var req = await _unitOfWork.Repository<AccountRequest>().GetByIdAsync(request.Id, ct);
            if (req == null)
                return Result.Failure("Không tìm thấy yêu cầu.", CommonErrorCodes.NotFound);

            if (req.Status != "pending")
                return Result.Failure("Yêu cầu này đã được xử lý.");

            req.Status = "rejected";
            req.ReviewReason = reason;
            req.ReviewedByUserId = request.ActorId;
            req.ReviewedAt = DateTimeOffset.UtcNow;
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<AccountRequest>().Update(req);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "account_request_rejected", "AccountRequest", req.Id,
                $"{{\"email\":\"{req.Email}\",\"reason\":{System.Text.Json.JsonSerializer.Serialize(reason)}}}", ct);
            await _unitOfWork.SaveChangesAsync();

            // Notify HR Leader (requester)
            await _notificationService.PublishUserEventAsync(req.RequestedByUserId, "ReceiveAccountRequestUpdate",
                new { RequestId = req.Id, Status = "rejected", Email = req.Email, Reason = reason });

            return Result.Success();
        }
    }
}
