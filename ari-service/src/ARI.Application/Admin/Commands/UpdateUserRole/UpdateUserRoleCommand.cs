using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.HiringTeam;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Commands.UpdateUserRole
{
    public record UpdateUserRoleCommand(Guid Id, string? Role, Guid? ActorId) : IRequest<Result>;

    public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateUserRoleCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateUserRoleCommand request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Role))
                return Result.Failure("Vui lòng chọn vai trò.");

            var normalizedRole = RoleNames.NormalizeDbRole(request.Role);
            if (normalizedRole == null || !RoleNames.AssignableStaff.Contains(normalizedRole))
                return Result.Failure($"Invalid role. Role must be one of: {string.Join(", ", RoleNames.AssignableStaff)}.");

            if (request.ActorId.HasValue && request.ActorId.Value == request.Id)
                return Result.Failure("Bạn không thể tự đổi vai trò của chính mình.");

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, ct);
            if (user == null)
                return Result.Failure("Không tìm thấy người dùng.", CommonErrorCodes.NotFound);

            var wasHiringManager = RoleNames.Is(user.Role, RoleNames.HiringManager);

            user.Role = normalizedRole;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "user_role_updated", "User", user.Id,
                $"{{\"email\":\"{user.Email}\",\"new_role\":\"{user.Role}\"}}", ct);

            // ADR-068: thôi vai trò Hiring Manager thì các tin họ đang phụ trách đóng cổng (không còn màn
            // HM để duyệt) — báo HR Leader chuyển HM. Không chặn việc đổi vai trò.
            if (wasHiringManager && !RoleNames.Is(normalizedRole, RoleNames.HiringManager))
            {
                var heldJobs = await HiringManagerAlerts.OpenJobsHeldByAsync(_unitOfWork, user.Id, ct);
                await HiringManagerAlerts.NotifyAdminsAsync(_unitOfWork, heldJobs,
                    $"{user.FullName ?? user.Email} không còn vai trò Hiring Manager.",
                    $"hm_inactive:{user.Id}:{DateTimeOffset.UtcNow.Ticks}", ct);
            }

            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
