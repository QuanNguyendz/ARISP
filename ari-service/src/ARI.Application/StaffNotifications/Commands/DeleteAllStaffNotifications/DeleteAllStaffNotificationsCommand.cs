using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.StaffNotifications.Commands.DeleteAllStaffNotifications
{
    /// <summary>Xóa (soft delete) toàn bộ thông báo của user; trả về số bản ghi đã xóa.</summary>
    public record DeleteAllStaffNotificationsCommand(Guid UserId) : IRequest<Result<int>>;

    public class DeleteAllStaffNotificationsCommandHandler
        : IRequestHandler<DeleteAllStaffNotificationsCommand, Result<int>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteAllStaffNotificationsCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<int>> Handle(DeleteAllStaffNotificationsCommand request, CancellationToken ct)
        {
            var list = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.RecipientUserId == request.UserId, ct)).ToList();
            foreach (var n in list)
                _unitOfWork.Repository<Notification>().Delete(n);
            if (list.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Result.Success(list.Count);
        }
    }
}
