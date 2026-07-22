using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
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
                return Result.Failure("Role is required.");

            var normalizedRole = request.Role.Trim().ToLower();
            if (normalizedRole != "hr_admin" && normalizedRole != "recruiter")
                return Result.Failure("Invalid role. Role must be 'hr_admin' or 'recruiter'.");

            if (request.ActorId.HasValue && request.ActorId.Value == request.Id)
                return Result.Failure("You cannot change your own role.");

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, ct);
            if (user == null)
                return Result.Failure("User not found.", CommonErrorCodes.NotFound);

            user.Role = normalizedRole;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "user_role_updated", "User", user.Id,
                $"{{\"email\":\"{user.Email}\",\"new_role\":\"{user.Role}\"}}", ct);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
