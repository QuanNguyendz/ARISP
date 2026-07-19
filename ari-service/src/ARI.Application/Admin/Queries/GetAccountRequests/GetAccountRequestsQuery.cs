using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Queries.GetAccountRequests
{
    /// <summary>Super Admin xem yêu cầu tạo tài khoản (lọc theo status hoặc "all").</summary>
    public record GetAccountRequestsQuery(string? Status) : IRequest<Result<List<AccountRequestListItemDto>>>;

    public class GetAccountRequestsQueryHandler : IRequestHandler<GetAccountRequestsQuery, Result<List<AccountRequestListItemDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAccountRequestsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<AccountRequestListItemDto>>> Handle(GetAccountRequestsQuery request, CancellationToken ct)
        {
            var normalized = string.IsNullOrWhiteSpace(request.Status) ? "pending" : request.Status.Trim().ToLower();

            var requests = normalized == "all"
                ? (await _unitOfWork.Repository<AccountRequest>().GetAllAsync(ct)).ToList()
                : (await _unitOfWork.Repository<AccountRequest>().FindAsync(r => r.Status == normalized, ct)).ToList();

            // Resolve tên người gửi yêu cầu (HR Leader)
            var requesterIds = requests.Select(r => r.RequestedByUserId).Distinct().ToList();
            var nameById = new Dictionary<Guid, string>();
            if (requesterIds.Count > 0)
            {
                var requesters = await _unitOfWork.Repository<User>().FindAsync(u => requesterIds.Contains(u.Id), ct);
                nameById = requesters.ToDictionary(u => u.Id, u => u.FullName ?? u.Email);
            }

            var items = requests
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new AccountRequestListItemDto(
                    r.Id, r.BatchId, r.Email, r.FullName, r.Role, r.Department, r.Status, r.ReviewReason,
                    nameById.TryGetValue(r.RequestedByUserId, out var n) ? n : "—",
                    r.CreatedAt, r.ReviewedAt))
                .ToList();

            return Result.Success(items);
        }
    }
}
