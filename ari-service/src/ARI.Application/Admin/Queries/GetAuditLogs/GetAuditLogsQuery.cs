using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Queries.GetAuditLogs
{
    public record GetAuditLogsQuery(string? Action, string? EntityType, int Page, int PageSize)
        : IRequest<Result<PagedListDto<AuditLogItemDto>>>;

    public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<PagedListDto<AuditLogItemDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAuditLogsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<PagedListDto<AuditLogItemDto>>> Handle(GetAuditLogsQuery request, CancellationToken ct)
        {
            var page = request.Page;
            var pageSize = request.PageSize;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var normalizedAction = !string.IsNullOrWhiteSpace(request.Action) ? request.Action.Trim().ToLower() : null;
            var normalizedEntity = !string.IsNullOrWhiteSpace(request.EntityType) ? request.EntityType.Trim().ToLower() : null;

            var logs = (await _unitOfWork.Repository<AuditLog>().FindAsync(l =>
                (normalizedAction == null || l.Action.ToLower() == normalizedAction) &&
                (normalizedEntity == null || (l.EntityType != null && l.EntityType.ToLower() == normalizedEntity)), ct)).ToList();

            var totalCount = logs.Count;

            // Resolve actor display names
            var actorIds = logs.Where(l => l.ActorUserId.HasValue).Select(l => l.ActorUserId!.Value).Distinct().ToList();
            var nameById = new Dictionary<Guid, string>();
            if (actorIds.Count > 0)
            {
                var actors = await _unitOfWork.Repository<User>().FindAsync(u => actorIds.Contains(u.Id), ct);
                nameById = actors.ToDictionary(u => u.Id, u => u.FullName ?? u.Email);
            }

            var items = logs
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new AuditLogItemDto(
                    l.Id,
                    l.Action,
                    l.EntityType,
                    l.EntityId,
                    l.Metadata,
                    l.ActorUserId.HasValue && nameById.TryGetValue(l.ActorUserId.Value, out var n) ? n : "Hệ thống",
                    l.CreatedAt))
                .ToList();

            return Result.Success(new PagedListDto<AuditLogItemDto>(
                totalCount, page, pageSize, (int)Math.Ceiling((double)totalCount / pageSize), items));
        }
    }
}
