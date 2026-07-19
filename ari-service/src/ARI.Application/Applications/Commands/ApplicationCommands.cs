using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Applications.Commands
{
    /// <summary>Base URL portal ứng viên (theo môi trường), fallback AdminFrontendUrl rồi localhost.</summary>
    internal static class ApplicationsSupport
    {
        public static string CandidateBaseUrl(IConfiguration configuration) =>
            configuration["Frontend:CandidateBaseUrl"]
            ?? configuration["Authentication:AdminFrontendUrl"]
            ?? "http://localhost:3000";
    }

    // ============================================================
    // PATCH /api/applications/{id}/status
    // ============================================================

    public record UpdateApplicationStatusCommand(Guid Id, string Status) : IRequest<Result<ApplicationResponse>>;

    public class UpdateApplicationStatusCommandHandler : IRequestHandler<UpdateApplicationStatusCommand, Result<ApplicationResponse>>
    {
        private readonly IApplicationService _applicationService;

        public UpdateApplicationStatusCommandHandler(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        public Task<Result<ApplicationResponse>> Handle(UpdateApplicationStatusCommand request, CancellationToken ct)
            => _applicationService.UpdateApplicationStatusAsync(request.Id, request.Status, ct);
    }

    // ============================================================
    // POST /api/applications/{id}/send-invite
    // ============================================================

    public record SendInterviewInviteCommand(Guid Id, int RoundNumber) : IRequest<Result<bool>>;

    public class SendInterviewInviteCommandHandler : IRequestHandler<SendInterviewInviteCommand, Result<bool>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IConfiguration _configuration;

        public SendInterviewInviteCommandHandler(IApplicationService applicationService, IConfiguration configuration)
        {
            _applicationService = applicationService;
            _configuration = configuration;
        }

        public Task<Result<bool>> Handle(SendInterviewInviteCommand request, CancellationToken ct)
            => _applicationService.SendInterviewInviteAsync(request.Id, ApplicationsSupport.CandidateBaseUrl(_configuration), request.RoundNumber, ct);
    }

    // ============================================================
    // POST /api/applications/{id}/accept
    // ============================================================

    public record AcceptApplicationCommand(Guid Id) : IRequest<Result<bool>>;

    public class AcceptApplicationCommandHandler : IRequestHandler<AcceptApplicationCommand, Result<bool>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IConfiguration _configuration;

        public AcceptApplicationCommandHandler(IApplicationService applicationService, IConfiguration configuration)
        {
            _applicationService = applicationService;
            _configuration = configuration;
        }

        public Task<Result<bool>> Handle(AcceptApplicationCommand request, CancellationToken ct)
            => _applicationService.AcceptApplicationAsync(request.Id, ApplicationsSupport.CandidateBaseUrl(_configuration), ct);
    }

    // ============================================================
    // POST /api/applications/{id}/reject
    // ============================================================

    public record RejectApplicationCommand(Guid Id) : IRequest<Result<bool>>;

    public class RejectApplicationCommandHandler : IRequestHandler<RejectApplicationCommand, Result<bool>>
    {
        private readonly IApplicationService _applicationService;

        public RejectApplicationCommandHandler(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        public Task<Result<bool>> Handle(RejectApplicationCommand request, CancellationToken ct)
            => _applicationService.RejectApplicationAsync(request.Id, ct);
    }
}
