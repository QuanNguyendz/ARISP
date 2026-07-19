using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Queries.GetCurrentUser
{
    /// <summary>Trả thông tin người dùng hiện tại từ claims (controller trích claims, handler tra fullName).</summary>
    public record GetCurrentUserQuery(string UserId, string? Email, string? Role) : IRequest<Result<UserMeResponse>>;

    public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserMeResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetCurrentUserQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<UserMeResponse>> Handle(GetCurrentUserQuery request, CancellationToken ct)
        {
            string fullName = "";

            // Xác định user type dựa theo role
            if (request.Role == AppRoles.Candidate)
            {
                if (Guid.TryParse(request.UserId, out var candidateGuid))
                {
                    var candidate = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateGuid, ct);
                    fullName = candidate?.FullName ?? "";
                }
            }
            else
            {
                if (Guid.TryParse(request.UserId, out var userGuid))
                {
                    var user = await _unitOfWork.Repository<User>().GetByIdAsync(userGuid, ct);
                    fullName = user?.FullName ?? "";
                }
            }

            return Result.Success(new UserMeResponse
            {
                Id = request.UserId,
                Email = request.Email ?? "",
                Name = fullName,
                Role = request.Role ?? ""
            });
        }
    }
}
