using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Queries.GetPendingUsers
{
    public record GetPendingUsersQuery : IRequest<Result<List<PendingUserDto>>>;

    public class GetPendingUsersQueryHandler : IRequestHandler<GetPendingUsersQuery, Result<List<PendingUserDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetPendingUsersQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<PendingUserDto>>> Handle(GetPendingUsersQuery request, CancellationToken ct)
        {
            var users = await _unitOfWork.Repository<User>().FindAsync(u => !u.IsActive, ct);
            var usersResponse = users
                .Select(u => new PendingUserDto(u.Id, u.Email, u.Role, u.FullName, u.CreatedAt))
                .ToList();

            return Result.Success(usersResponse);
        }
    }
}
