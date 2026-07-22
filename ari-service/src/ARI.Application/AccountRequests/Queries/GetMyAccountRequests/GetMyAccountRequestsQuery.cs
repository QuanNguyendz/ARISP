using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.AccountRequests.Queries.GetMyAccountRequests
{
    /// <summary>Danh sách yêu cầu do chính HR Leader hiện tại đã gửi (theo dõi trạng thái).</summary>
    public record GetMyAccountRequestsQuery(Guid UserId) : IRequest<Result<List<MyAccountRequestItemDto>>>;

    public class GetMyAccountRequestsQueryHandler : IRequestHandler<GetMyAccountRequestsQuery, Result<List<MyAccountRequestItemDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetMyAccountRequestsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<MyAccountRequestItemDto>>> Handle(GetMyAccountRequestsQuery request, CancellationToken ct)
        {
            var requests = await _unitOfWork.Repository<AccountRequest>()
                .FindAsync(r => r.RequestedByUserId == request.UserId, ct);

            var items = requests
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new MyAccountRequestItemDto(
                    r.Id, r.BatchId, r.Email, r.FullName, r.Role, r.Department,
                    r.Status, r.ReviewReason, r.CreatedAt, r.ReviewedAt))
                .ToList();

            return Result.Success(items);
        }
    }
}
