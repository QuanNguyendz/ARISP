using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using MediatR;

namespace ARI.Application.Applications.Queries
{
    // ============================================================
    // GET /api/applications/{id}
    // ============================================================

    public record GetApplicationByIdQuery(Guid Id) : IRequest<Result<ApplicationResponse>>;

    public class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, Result<ApplicationResponse>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IFileStorageService _fileStorage;

        public GetApplicationByIdQueryHandler(IApplicationService applicationService, IFileStorageService fileStorage)
        {
            _applicationService = applicationService;
            _fileStorage = fileStorage;
        }

        public async Task<Result<ApplicationResponse>> Handle(GetApplicationByIdQuery request, CancellationToken ct)
        {
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

    public record GetApplicationsQuery(Guid? MineUserId) : IRequest<Result<List<ApplicationResponse>>>;

    public class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, Result<List<ApplicationResponse>>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IFileStorageService _fileStorage;

        public GetApplicationsQueryHandler(IApplicationService applicationService, IFileStorageService fileStorage)
        {
            _applicationService = applicationService;
            _fileStorage = fileStorage;
        }

        public async Task<Result<List<ApplicationResponse>>> Handle(GetApplicationsQuery request, CancellationToken ct)
        {
            var result = request.MineUserId.HasValue
                ? await _applicationService.GetApplicationsForCreatorAsync(request.MineUserId.Value, ct)
                : await _applicationService.GetAllApplicationsAsync(ct);

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
