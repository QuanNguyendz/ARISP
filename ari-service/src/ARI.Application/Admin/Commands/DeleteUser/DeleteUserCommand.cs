using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.HiringTeam;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Commands.DeleteUser
{
    /// <summary>Từ chối hoặc xóa tài khoản staff (soft delete). Dùng cho việc từ chối user chờ duyệt.</summary>
    public record DeleteUserCommand(Guid Id, Guid? ActorId) : IRequest<Result>;

    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteUserCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(DeleteUserCommand request, CancellationToken ct)
        {
            if (request.ActorId.HasValue && request.ActorId.Value == request.Id)
                return Result.Failure("Bạn không thể xóa chính tài khoản của mình.");

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, ct);
            if (user == null)
                return Result.Failure("Không tìm thấy người dùng.", CommonErrorCodes.NotFound);

            // ADR-068: tin người này đang làm Hiring Manager chính sẽ đóng cổng — báo HR Leader chuyển HM.
            var heldJobs = await HiringManagerAlerts.OpenJobsHeldByAsync(_unitOfWork, user.Id, ct);

            _unitOfWork.Repository<User>().Delete(user);
            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "user_deleted", "User", user.Id,
                $"{{\"email\":\"{user.Email}\",\"role\":\"{user.Role}\"}}", ct);
            await HiringManagerAlerts.NotifyAdminsAsync(_unitOfWork, heldJobs,
                $"Tài khoản Hiring Manager {user.FullName ?? user.Email} vừa bị xoá.",
                $"hm_inactive:{user.Id}:{DateTimeOffset.UtcNow.Ticks}", ct);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
