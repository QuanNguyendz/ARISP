using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.StaffNotifications.Commands.MarkStaffNotificationRead
{
    /// <summary>Đánh dấu một thông báo đã đọc — chỉ khi thuộc về đúng user.</summary>
    public record MarkStaffNotificationReadCommand(Guid UserId, Guid NotificationId) : IRequest<Result>;

    public class MarkStaffNotificationReadCommandHandler
        : IRequestHandler<MarkStaffNotificationReadCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public MarkStaffNotificationReadCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(MarkStaffNotificationReadCommand request, CancellationToken ct)
        {
            var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(request.NotificationId, ct);
            if (n == null || n.RecipientUserId != request.UserId)
                return Result.Failure("Không tìm thấy thông báo.");
            if (!n.IsRead)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Notification>().Update(n);
                await _unitOfWork.SaveChangesAsync();
            }
            return Result.Success();
        }
    }
}
