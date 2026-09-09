using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Queries.GetAdminStats
{
    public record GetAdminStatsQuery : IRequest<Result<AdminStatsDto>>;

    public class GetAdminStatsQueryHandler : IRequestHandler<GetAdminStatsQuery, Result<AdminStatsDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAdminStatsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<AdminStatsDto>> Handle(GetAdminStatsQuery request, CancellationToken ct)
        {
            // Chỉ kéo cột cần cho thống kê (Role, IsActive) thay vì cả entity User;
            // ứng viên & yêu cầu chờ duyệt đếm bằng SQL COUNT (không nạp rows).
            var userRoles = await _unitOfWork.Repository<User>()
                .QueryAsync(q => q.Select(u => new { u.Role, u.IsActive }), ct);
            var candidateCount = await _unitOfWork.Repository<CandidateAccount>().CountAsync(_ => true, ct);
            var pendingCount = await _unitOfWork.Repository<AccountRequest>().CountAsync(r => r.Status == "pending", ct);

            string Role(string? r) => (r ?? string.Empty).Trim().ToLowerInvariant();

            return Result.Success(new AdminStatsDto(
                TotalUsers: userRoles.Count,
                ActiveUsers: userRoles.Count(u => u.IsActive),
                LockedUsers: userRoles.Count(u => !u.IsActive),
                PendingRequests: pendingCount,
                SuperAdmins: userRoles.Count(u => Role(u.Role) == "super_admin"),
                HrAdmins: userRoles.Count(u => Role(u.Role) == "hr_admin"),
                Recruiters: userRoles.Count(u => Role(u.Role) == "recruiter"),
                HiringManagers: userRoles.Count(u => Role(u.Role) == "hiring_manager"),
                Candidates: candidateCount));
        }
    }
}
