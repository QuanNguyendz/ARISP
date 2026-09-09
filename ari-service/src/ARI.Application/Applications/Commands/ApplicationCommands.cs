using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Emails;
using ARI.Application.Interfaces;
using MediatR;

namespace ARI.Application.Applications.Commands
{
    // ============================================================
    // PATCH /api/applications/{id}/status
    // ============================================================

    public record UpdateApplicationStatusCommand(Guid Id, string Status, Guid? UserId, string? Role)
        : IRequest<Result<ApplicationResponse>>;

    public class UpdateApplicationStatusCommandHandler : IRequestHandler<UpdateApplicationStatusCommand, Result<ApplicationResponse>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateApplicationStatusCommandHandler(IApplicationService applicationService, IUnitOfWork unitOfWork)
        {
            _applicationService = applicationService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<ApplicationResponse>> Handle(UpdateApplicationStatusCommand request, CancellationToken ct)
        {
            // Kéo trạng thái hồ sơ bằng tay là thao tác VẬN HÀNH phễu → cần quyền quản lý tin.
            // Thành viên đội tuyển dụng (Hiring Manager) đọc được hồ sơ nhưng không tự đổi trạng thái;
            // họ tác động qua các cổng quyết định riêng (duyệt shortlist, chốt verdict — ADR-061).
            var (application, _, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (application == null)
                return Result.Failure<ApplicationResponse>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result.Failure<ApplicationResponse>(JobAccessErrors.ApplicationManageForbidden, CommonErrorCodes.Forbidden);

            return await _applicationService.UpdateApplicationStatusAsync(request.Id, request.Status, ct);
        }
    }

    // ============================================================
    // POST /api/applications/{id}/reject
    // ============================================================

    public record RejectApplicationCommand(
        Guid Id, Guid? UserId, string? Role, EmailOverride? EmailOverride = null) : IRequest<Result<bool>>;

    public class RejectApplicationCommandHandler : IRequestHandler<RejectApplicationCommand, Result<bool>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IUnitOfWork _unitOfWork;

        public RejectApplicationCommandHandler(IApplicationService applicationService, IUnitOfWork unitOfWork)
        {
            _applicationService = applicationService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<bool>> Handle(RejectApplicationCommand request, CancellationToken ct)
        {
            // Loại hồ sơ là quyết định huỷ + gửi thư cho ứng viên — cần quyền quản lý tin.
            var (application, _, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (application == null)
                return Result<bool>.Failure(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result<bool>.Failure(JobAccessErrors.ApplicationManageForbidden, CommonErrorCodes.Forbidden);

            return await _applicationService.RejectApplicationAsync(request.Id, ct, request.EmailOverride, request.UserId);
        }
    }
}
