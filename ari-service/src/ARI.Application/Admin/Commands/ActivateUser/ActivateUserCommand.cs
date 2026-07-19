using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Commands.ActivateUser
{
    public record ActivateUserCommand(Guid Id, Guid? ActorId) : IRequest<Result>;

    public class ActivateUserCommandHandler : IRequestHandler<ActivateUserCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ActivateUserCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(ActivateUserCommand request, CancellationToken ct)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, ct);
            if (user == null)
                return Result.Failure("User not found.", CommonErrorCodes.NotFound);

            if (user.IsActive)
                return Result.Failure("Tài khoản đã đang hoạt động.");

            user.IsActive = true;
            user.LockReason = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);
            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "user_activated", "User", user.Id,
                $"{{\"email\":\"{user.Email}\"}}", ct);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
