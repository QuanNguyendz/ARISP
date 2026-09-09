using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Applications.Queries
{
    // ============================================================
    // GET /api/applications/{id}
    // ============================================================

    public record GetApplicationByIdQuery(Guid Id, Guid? UserId, string? Role) : IRequest<Result<ApplicationResponse>>;

    public class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, Result<ApplicationResponse>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IFileStorageService _fileStorage;
        private readonly IUnitOfWork _unitOfWork;

        public GetApplicationByIdQueryHandler(
            IApplicationService applicationService, IFileStorageService fileStorage, IUnitOfWork unitOfWork)
        {
            _applicationService = applicationService;
            _fileStorage = fileStorage;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<ApplicationResponse>> Handle(GetApplicationByIdQuery request, CancellationToken ct)
        {
            // Endpoint này trả CV, thông tin liên hệ và kết quả phân tích của ứng viên. Trước đây
            // nó KHÔNG kiểm tra gì ngoài policy InternalStaff, nên bất kỳ nhân sự nào biết id là
            // đọc được hồ sơ thuộc tin của người khác.
            var (application, _, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (application == null)
                return Result.Failure<ApplicationResponse>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.TeamMember)
                return Result.Failure<ApplicationResponse>(JobAccessErrors.ApplicationForbidden, CommonErrorCodes.Forbidden);

            var result = await _applicationService.GetApplicationByIdAsync(request.Id, ct);
            if (result.IsFailure)
                return Result.Failure<ApplicationResponse>(result.Error, CommonErrorCodes.NotFound);

            // Resolve storageKey -> URL client dùng được
            if (!string.IsNullOrEmpty(result.Value!.CvFileUrl))
                result.Value.CvFileUrl = await _fileStorage.GetUrlAsync(result.Value.CvFileUrl, ct);

            return result;
        }
    }

    // ============================================================
    // GET /api/applications (mine=true: theo tin của người tạo)
    // ============================================================

    /// <summary>
    /// <paramref name="MineOnly"/> là BỘ LỌC GIAO DIỆN ("chỉ hiện tin tôi tạo"), không phải cổng
    /// bảo mật. Phạm vi thật luôn do <see cref="JobAccess.ScopedJobIdsAsync"/> quyết định ở server:
    /// trước đây bỏ tham số <c>?mine</c> đi là thấy hồ sơ của toàn công ty.
    /// </summary>
    public record GetApplicationsQuery(Guid? UserId, string? Role, bool MineOnly)
        : IRequest<Result<List<ApplicationResponse>>>;

    public class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, Result<List<ApplicationResponse>>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IFileStorageService _fileStorage;
        private readonly IUnitOfWork _unitOfWork;

        public GetApplicationsQueryHandler(
            IApplicationService applicationService, IFileStorageService fileStorage, IUnitOfWork unitOfWork)
        {
            _applicationService = applicationService;
            _fileStorage = fileStorage;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<ApplicationResponse>>> Handle(GetApplicationsQuery request, CancellationToken ct)
        {
            var scope = await JobAccess.ScopedJobIdsAsync(_unitOfWork, request.UserId, request.Role, ct);

            // Người dùng bấm "chỉ tin của tôi" → thu hẹp thêm trong phạm vi đã được phép.
            if (request.MineOnly && request.UserId is { } uid && uid != Guid.Empty)
            {
                var owned = await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => j.CreatedByUserId == uid).Select(j => j.Id), ct);
                var ownedSet = owned.ToHashSet();
                scope = scope == null ? ownedSet : scope.Intersect(ownedSet).ToHashSet();
            }

            var result = scope == null
                ? await _applicationService.GetAllApplicationsAsync(ct)
                : await _applicationService.GetApplicationsForJobsAsync(scope, ct);

            if (result.IsFailure)
                return result;

            // Resolve storageKey -> URL client dùng được
            foreach (var app in result.Value!)
            {
                if (!string.IsNullOrEmpty(app.CvFileUrl))
                    app.CvFileUrl = await _fileStorage.GetUrlAsync(app.CvFileUrl, ct);
            }

            return result;
        }
    }

    // ============================================================
    // GET /api/applications/practice-eligibility/{id}
    // ============================================================

    public record GetPracticeEligibilityQuery(Guid Id, int RoundNumber) : IRequest<Result<bool>>;

    public class GetPracticeEligibilityQueryHandler : IRequestHandler<GetPracticeEligibilityQuery, Result<bool>>
    {
        private readonly IApplicationService _applicationService;

        public GetPracticeEligibilityQueryHandler(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        public Task<Result<bool>> Handle(GetPracticeEligibilityQuery request, CancellationToken ct)
            => _applicationService.CheckPracticeEligibilityAsync(request.Id, request.RoundNumber, ct);
    }
}
