using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Commands.ApproveUser
{
    public record ApproveUserCommand(Guid Id, Guid? ActorId) : IRequest<Result>;

    public class ApproveUserCommandHandler : IRequestHandler<ApproveUserCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ApproveUserCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(ApproveUserCommand request, CancellationToken ct)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, ct);
            if (user == null)
                return Result.Failure("User not found.", CommonErrorCodes.NotFound);

            if (user.IsActive)
                return Result.Failure("User already active.");

            user.IsActive = true;
            _unitOfWork.Repository<User>().Update(user);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "user_approved", "User", user.Id,
                $"{{\"email\":\"{user.Email}\",\"role\":\"{user.Role}\"}}", ct);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
