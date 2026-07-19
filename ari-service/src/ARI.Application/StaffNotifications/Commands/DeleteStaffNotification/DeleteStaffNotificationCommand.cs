using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.StaffNotifications.Commands.DeleteStaffNotification
{
    /// <summary>Xóa (soft delete) một thông báo — chỉ khi thuộc về đúng user.</summary>
    public record DeleteStaffNotificationCommand(Guid UserId, Guid NotificationId) : IRequest<Result>;

    public class DeleteStaffNotificationCommandHandler
        : IRequestHandler<DeleteStaffNotificationCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteStaffNotificationCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(DeleteStaffNotificationCommand request, CancellationToken ct)
        {
            var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(request.NotificationId, ct);
            if (n == null || n.RecipientUserId != request.UserId)
                return Result.Failure("Không tìm thấy thông báo.");

            _unitOfWork.Repository<Notification>().Delete(n);
            await _unitOfWork.SaveChangesAsync();
            return Result.Success();
        }
    }
}
