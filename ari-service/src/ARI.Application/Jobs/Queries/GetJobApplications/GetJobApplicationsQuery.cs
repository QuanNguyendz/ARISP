using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Services;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Jobs.Queries.GetJobApplications
{
    /// <summary>
    /// Danh sách ứng viên (Application) của MỘT job. Recruiter chỉ xem được job mình tạo;
    /// HrAdmin/SuperAdmin xem được mọi job.
    /// </summary>
    public record GetJobApplicationsQuery(Guid JobId, Guid UserId, string? Role)
        : IRequest<Result<List<ApplicationResponse>>>;

    public class GetJobApplicationsQueryHandler : IRequestHandler<GetJobApplicationsQuery, Result<List<ApplicationResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationService _applicationService;
        private readonly IFileStorageService _fileStorage;

        public GetJobApplicationsQueryHandler(
            IUnitOfWork unitOfWork,
            ApplicationService applicationService,
            IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _applicationService = applicationService;
            _fileStorage = fileStorage;
        }

        public async Task<Result<List<ApplicationResponse>>> Handle(GetJobApplicationsQuery request, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobId, ct);
            if (job == null)
                return Result.Failure<List<ApplicationResponse>>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            var isAdmin = request.Role == AppRoles.SuperAdmin || request.Role == AppRoles.HrAdmin;
            if (!isAdmin && job.CreatedByUserId != request.UserId)
                return Result.Failure<List<ApplicationResponse>>(
                    "Bạn không có quyền xem ứng viên của tin tuyển dụng này.", CommonErrorCodes.Forbidden);

            var result = await _applicationService.GetApplicationsByJobAsync(request.JobId, ct);
            if (result.IsFailure)
                return Result.Failure<List<ApplicationResponse>>(result.Error);

            // Resolve storageKey -> URL client dùng được (local: relative, R2: presigned)
            foreach (var app in result.Value!)
            {
                if (!string.IsNullOrEmpty(app.CvFileUrl))
                    app.CvFileUrl = await _fileStorage.GetUrlAsync(app.CvFileUrl, ct);
            }

            return Result.Success(result.Value);
        }
    }
}
