using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.StaffNotifications.Commands.MarkAllStaffNotificationsRead
{
    /// <summary>Đánh dấu tất cả thông báo của user đã đọc; trả về số bản ghi đã cập nhật.</summary>
    public record MarkAllStaffNotificationsReadCommand(Guid UserId) : IRequest<Result<int>>;

    public class MarkAllStaffNotificationsReadCommandHandler
        : IRequestHandler<MarkAllStaffNotificationsReadCommand, Result<int>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public MarkAllStaffNotificationsReadCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<int>> Handle(MarkAllStaffNotificationsReadCommand request, CancellationToken ct)
        {
            var list = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.RecipientUserId == request.UserId && !n.IsRead, ct)).ToList();
            foreach (var n in list)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Notification>().Update(n);
            }
            if (list.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Result.Success(list.Count);
        }
    }
}
