using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Playbooks.Queries.GetPlaybooks
{
    /// <summary>Danh sách playbook (lọc theo scope nếu có). Không trả parsedText.</summary>
    public record GetPlaybooksQuery(string? Scope) : IRequest<Result<List<PlaybookListItemDto>>>;

    public class GetPlaybooksQueryHandler : IRequestHandler<GetPlaybooksQuery, Result<List<PlaybookListItemDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetPlaybooksQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<PlaybookListItemDto>>> Handle(GetPlaybooksQuery request, CancellationToken ct)
        {
            // Projection ở tầng SQL — KHÔNG kéo cột parsedText (nội dung playbook rất lớn).
            var scopeLower = request.Scope?.ToLowerInvariant();
            var docs = await _unitOfWork.Repository<PlaybookDocument>().QueryAsync(q =>
                (string.IsNullOrEmpty(scopeLower) ? q : q.Where(d => d.Scope.ToLower() == scopeLower))
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new
                    {
                        d.Id, d.Scope, d.ScopeRefId, d.RoundNumber, d.DocumentType,
                        d.FileName, d.FileFormat, d.Status, d.CreatedAt, d.UploadedByUserId,
                    }), ct);

            var uploaderIds = docs.Select(d => d.UploadedByUserId).Distinct().ToList();
            var uploaderNames = (await _unitOfWork.Repository<User>()
                    .QueryAsync(q => q.Where(u => uploaderIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email }), ct))
                .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName);

            var items = docs.Select(d => new PlaybookListItemDto(
                d.Id, d.Scope, d.ScopeRefId, d.RoundNumber, d.DocumentType,
                d.FileName, d.FileFormat, d.Status, d.CreatedAt,
                uploaderNames.TryGetValue(d.UploadedByUserId, out var n) ? n : null)).ToList();

            return Result.Success(items);
        }
    }
}
