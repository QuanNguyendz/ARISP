using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Commands.DeactivateUser
{
    public record DeactivateUserCommand(Guid Id, string? Reason, Guid? ActorId) : IRequest<Result>;

    public class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeactivateUserCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(DeactivateUserCommand request, CancellationToken ct)
        {
            if (request.ActorId.HasValue && request.ActorId.Value == request.Id)
                return Result.Failure("Bạn không thể khóa chính tài khoản của mình.");

            var reason = request.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure("Vui lòng nhập lý do khóa tài khoản.");

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, ct);
            if (user == null)
                return Result.Failure("User not found.", CommonErrorCodes.NotFound);

            if (!user.IsActive)
                return Result.Failure("Tài khoản đã bị khóa.");

            user.IsActive = false;
            user.LockReason = reason;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);
            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "user_deactivated", "User", user.Id,
                $"{{\"email\":\"{user.Email}\",\"reason\":{System.Text.Json.JsonSerializer.Serialize(reason)}}}", ct);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
