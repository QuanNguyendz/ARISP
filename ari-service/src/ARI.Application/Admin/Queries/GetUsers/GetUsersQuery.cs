using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Departments;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Admin.Queries.GetUsers
{
    public record GetUsersQuery(string? Search, string? Role, bool? IsActive, int Page, int PageSize)
        : IRequest<Result<PagedListDto<StaffUserListItemDto>>>;

    public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<PagedListDto<StaffUserListItemDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetUsersQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<PagedListDto<StaffUserListItemDto>>> Handle(GetUsersQuery request, CancellationToken ct)
        {
            var page = request.Page;
            var pageSize = request.PageSize;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var cleanSearch = !string.IsNullOrWhiteSpace(request.Search) ? request.Search.Trim().ToLower() : null;
            var normalizedRole = !string.IsNullOrWhiteSpace(request.Role) ? request.Role.Trim().ToLower() : null;

            var users = await _unitOfWork.Repository<User>().FindAsync(u =>
                (cleanSearch == null || (u.FullName != null && u.FullName.ToLower().Contains(cleanSearch)) || (u.Email != null && u.Email.ToLower().Contains(cleanSearch))) &&
                (normalizedRole == null || (u.Role != null && u.Role.ToLower() == normalizedRole)) &&
                (!request.IsActive.HasValue || u.IsActive == request.IsActive.Value), ct);

            var filteredUsers = users.ToList();
            var totalCount = filteredUsers.Count;

            var pageUsers = filteredUsers
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Chỉ tra tên đội của ĐÚNG trang đang xem, một truy vấn cho cả trang.
            var departmentNames = await DepartmentLookup.NamesAsync(
                _unitOfWork, pageUsers.Where(u => u.DepartmentId.HasValue).Select(u => u.DepartmentId!.Value), ct);

            var items = pageUsers
                .Select(u => new StaffUserListItemDto(
                    u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.LockReason, u.CreatedAt,
                    u.DepartmentId,
                    u.DepartmentId is { } d && departmentNames.TryGetValue(d, out var name) ? name : null))
                .ToList();

            return Result.Success(new PagedListDto<StaffUserListItemDto>(
                totalCount, page, pageSize, (int)Math.Ceiling((double)totalCount / pageSize), items));
        }
    }
}
