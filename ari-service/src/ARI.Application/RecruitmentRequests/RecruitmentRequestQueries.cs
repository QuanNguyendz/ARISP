using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Departments;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.RecruitmentRequests
{
    /// <summary>
    /// Đọc phiếu yêu cầu tuyển dụng (ADR-063).
    ///
    /// PHẠM VI DO SERVER QUYẾT ĐỊNH — quy tắc 19: Hiring Manager chỉ thấy phiếu của mình, Recruiter
    /// chỉ thấy phiếu được giao, HR Leader/Super Admin thấy tất cả. Màn hình KHÔNG được gửi lên cờ
    /// kiểu <c>?mine=true</c>; ADR-061 đã phải đi vá đúng lỗ đó ở <c>/jobs/admin</c> và
    /// <c>/applications</c>, nơi client tự khai phạm vi của chính mình.
    /// </summary>
    internal static class RecruitmentRequestScope
    {
        /// <summary>
        /// Lọc theo vai trò NGƯỜI GỌI, áp thẳng trong biểu thức SQL (không kéo về rồi lọc trong bộ nhớ).
        /// Vai trò lạ rơi vào nhánh "không thấy gì" — mặc định ĐÓNG, giống <c>DbChangeRouter</c>.
        /// </summary>
        public static IQueryable<RecruitmentRequest> Apply(
            IQueryable<RecruitmentRequest> q, string? role, Guid? actorId)
        {
            if (RoleNames.IsAdmin(role))
                return q;

            if (RoleNames.Is(role, RoleNames.HiringManager))
                return q.Where(r => r.RequestedByUserId == actorId);

            if (RoleNames.Is(role, RoleNames.Recruiter))
                return q.Where(r => r.AssignedRecruiterId == actorId);

            // Mặc định ĐÓNG: vai trò không nằm trong ba nhánh trên thì không thấy phiếu nào.
            return q.Where(_ => false);
        }
    }

    // ---------------------------------------------------------------------------------
    //  Danh sách phiếu
    // ---------------------------------------------------------------------------------

    public record GetRecruitmentRequestsQuery(
        string? Status, string? Search, string? Priority, Guid? ActorId, string? ActorRole)
        : IRequest<Result<List<RecruitmentRequestListItemDto>>>;

    public class GetRecruitmentRequestsQueryHandler
        : IRequestHandler<GetRecruitmentRequestsQuery, Result<List<RecruitmentRequestListItemDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetRecruitmentRequestsQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<List<RecruitmentRequestListItemDto>>> Handle(
            GetRecruitmentRequestsQuery request, CancellationToken ct)
        {
            var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToLowerInvariant();
            var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim().ToLowerInvariant();
            var priority = RecruitmentPriority.IsValid(request.Priority)
                ? request.Priority!.Trim().ToLowerInvariant()
                : null;

            // Lọc PHẠM VI + trạng thái + tìm kiếm đều đẩy xuống SQL. Kéo cả bảng về rồi lọc trong bộ
            // nhớ là đúng lỗi đã phải chữa ở `GetOffersQuery`: chi phí tăng theo tổng số phiếu của
            // công ty, với mọi người xem, kể cả người chỉ có một phiếu.
            var rows = await _unitOfWork.Repository<RecruitmentRequest>().QueryAsync(q =>
            {
                var scoped = RecruitmentRequestScope.Apply(
                    q.Where(r => r.DeletedAt == null), request.ActorRole, request.ActorId);

                if (status != null)
                    scoped = scoped.Where(r => r.Status == status);

                if (search != null)
                    scoped = scoped.Where(r => r.Title.ToLower().Contains(search));

                if (priority != null)
                    scoped = scoped.Where(r => r.Priority == priority);

                // Ưu tiên CAO lên đầu, cùng mức thì mới nhất trước. Sắp bằng chuỗi thì `high` nằm
                // sau `low` theo bảng chữ cái, nên phải ánh xạ ra số ngay trong biểu thức SQL —
                // `RecruitmentPriority.Rank` là hàm C#, EF không dịch được.
                return scoped
                    .OrderBy(r => r.Priority == RecruitmentPriority.High ? 0
                                : r.Priority == RecruitmentPriority.Medium ? 1 : 2)
                    .ThenByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.Id, r.Title, r.DepartmentId, r.Headcount, r.Priority, r.Status,
                        r.SalaryMin, r.SalaryMax, r.SalaryCurrency,
                        r.RequestedByUserId, r.AssignedRecruiterId, r.ReviewReason,
                        r.SubmissionCount, r.CreatedAt, r.ReviewedAt,
                    });
            }, ct);

            if (rows.Count == 0)
                return Result.Success(new List<RecruitmentRequestListItemDto>());

            var names = await NameLookupAsync(
                _unitOfWork,
                rows.Select(r => r.RequestedByUserId)
                    .Concat(rows.Where(r => r.AssignedRecruiterId.HasValue).Select(r => r.AssignedRecruiterId!.Value)),
                ct);

            var jobByRequest = await JobByRequestAsync(_unitOfWork, rows.Select(r => r.Id).ToList(), ct);
            var departments = await DepartmentLookup.NamesAsync(
                _unitOfWork, rows.Where(r => r.DepartmentId.HasValue).Select(r => r.DepartmentId!.Value), ct);

            return Result.Success(rows.Select(r => new RecruitmentRequestListItemDto(
                r.Id, r.Title, r.DepartmentId,
                r.DepartmentId is { } dId && departments.TryGetValue(dId, out var dName) ? dName : null,
                r.Headcount, r.Priority, r.Status,
                r.SalaryMin, r.SalaryMax, r.SalaryCurrency,
                names.TryGetValue(r.RequestedByUserId, out var hm) ? hm : "—",
                r.RequestedByUserId,
                r.AssignedRecruiterId.HasValue && names.TryGetValue(r.AssignedRecruiterId.Value, out var rec) ? rec : null,
                r.AssignedRecruiterId,
                r.ReviewReason,
                r.SubmissionCount,
                jobByRequest.TryGetValue(r.Id, out var jobId) ? jobId : null,
                r.CreatedAt, r.ReviewedAt)).ToList());
        }

        /// <summary>Tên hiển thị của những người được nhắc tới, tra một lượt.</summary>
        internal static async Task<Dictionary<Guid, string>> NameLookupAsync(
            IUnitOfWork uow, IEnumerable<Guid> userIds, CancellationToken ct)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, string>();

            var users = await uow.Repository<User>().QueryAsync(
                q => q.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email }), ct);

            return users.ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName!);
        }

        /// <summary>
        /// Phiếu nào đã dựng thành tin — SUY RA từ <c>job_postings.recruitment_request_id</c>.
        /// Đây là lý do phiếu không có cột <c>job_posting_id</c>: một chiều thì không thể lệch.
        /// </summary>
        internal static async Task<Dictionary<Guid, Guid>> JobByRequestAsync(
            IUnitOfWork uow, List<Guid> requestIds, CancellationToken ct)
        {
            if (requestIds.Count == 0) return new Dictionary<Guid, Guid>();

            var jobs = await uow.Repository<JobPosting>().QueryAsync(
                q => q.Where(j => j.RecruitmentRequestId != null
                                  && requestIds.Contains(j.RecruitmentRequestId!.Value)
                                  && j.DeletedAt == null)
                      .Select(j => new { j.Id, RequestId = j.RecruitmentRequestId!.Value }), ct);

            return jobs.GroupBy(j => j.RequestId).ToDictionary(g => g.Key, g => g.First().Id);
        }
    }

    // ---------------------------------------------------------------------------------
    //  Chi tiết một phiếu
    // ---------------------------------------------------------------------------------

    public record GetRecruitmentRequestByIdQuery(Guid Id, Guid? ActorId, string? ActorRole)
        : IRequest<Result<RecruitmentRequestDetailDto>>;

    public class GetRecruitmentRequestByIdQueryHandler
        : IRequestHandler<GetRecruitmentRequestByIdQuery, Result<RecruitmentRequestDetailDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetRecruitmentRequestByIdQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<RecruitmentRequestDetailDto>> Handle(
            GetRecruitmentRequestByIdQuery request, CancellationToken ct)
        {
            // Lọc phạm vi ngay trong truy vấn: người ngoài phạm vi nhận 404 chứ không phải 403, để
            // không rò rỉ việc "có tồn tại một phiếu với id này" (IDOR).
            var rows = await _unitOfWork.Repository<RecruitmentRequest>().QueryAsync(
                q => RecruitmentRequestScope.Apply(
                        q.Where(r => r.Id == request.Id && r.DeletedAt == null),
                        request.ActorRole, request.ActorId),
                ct);

            var req = rows.FirstOrDefault();
            if (req == null)
                return Result.Failure<RecruitmentRequestDetailDto>(
                    "Không tìm thấy phiếu yêu cầu tuyển dụng.", CommonErrorCodes.NotFound);

            var ids = new List<Guid> { req.RequestedByUserId };
            if (req.ReviewedByUserId.HasValue) ids.Add(req.ReviewedByUserId.Value);
            if (req.AssignedRecruiterId.HasValue) ids.Add(req.AssignedRecruiterId.Value);
            if (req.RevokedByUserId.HasValue) ids.Add(req.RevokedByUserId.Value);

            var names = await GetRecruitmentRequestsQueryHandler.NameLookupAsync(_unitOfWork, ids, ct);
            var jobByRequest = await GetRecruitmentRequestsQueryHandler.JobByRequestAsync(
                _unitOfWork, new List<Guid> { req.Id }, ct);
            jobByRequest.TryGetValue(req.Id, out var jobPostingId);

            var isOwner = req.RequestedByUserId == request.ActorId;
            var isAdmin = RoleNames.IsAdmin(request.ActorRole);

            return Result.Success(new RecruitmentRequestDetailDto(
                req.Id, req.Title, req.DepartmentId,
                await DepartmentLookup.NameForUserAsync(_unitOfWork, req.DepartmentId, ct),
                req.Headcount, req.Priority, req.Reason, req.Description, req.Requirements,
                req.EmploymentType, req.WorkMode, req.Location, req.ExperienceLevel, req.ExpectedStartDate,
                req.SalaryMin, req.SalaryMax, req.SalaryCurrency,
                req.Status, req.ReviewReason,
                req.RequestedByUserId,
                names.TryGetValue(req.RequestedByUserId, out var hm) ? hm : "—",
                req.ReviewedByUserId,
                req.ReviewedByUserId.HasValue && names.TryGetValue(req.ReviewedByUserId.Value, out var rv) ? rv : null,
                req.ReviewedAt,
                req.AssignedRecruiterId,
                req.AssignedRecruiterId.HasValue && names.TryGetValue(req.AssignedRecruiterId.Value, out var rc) ? rc : null,
                jobPostingId == Guid.Empty ? null : jobPostingId,
                req.SubmissionCount,
                req.RevokedReason,
                req.RevokedByUserId.HasValue && names.TryGetValue(req.RevokedByUserId.Value, out var rb) ? rb : null,
                req.CreatedAt, req.UpdatedAt,

                CanEdit: isOwner && RecruitmentRequestStatus.IsEditable(req.Status),

                // Không tự duyệt phiếu của mình — cùng luật với handler, phát biểu lại ở đây để
                // giao diện không bày ra một nút chắc chắn sẽ bị server từ chối.
                CanReview: isAdmin && !isOwner
                           && RecruitmentRequestStatus.Is(req.Status, RecruitmentRequestStatus.Pending),

                CanCreateJob: RecruitmentRequestStatus.Is(req.Status, RecruitmentRequestStatus.Approved)
                              && jobPostingId == Guid.Empty
                              && (isAdmin || req.AssignedRecruiterId == request.ActorId),

                // Cùng bốn điều kiện với `LoadRevocableAsync`, phát biểu lại ở đây để giao diện không bày ra
                // một nút chắc chắn bị server từ chối. Phần "đã dựng tin chưa" dùng chính `jobPostingId`
                // vừa suy ra ở trên — không truy vấn lại, không có cột thứ hai để trôi lệch.
                CanRevoke: RecruitmentRequestStatus.IsRevocable(req.Status)
                           && jobPostingId == Guid.Empty
                           && (isOwner || isAdmin)));
        }
    }
}
