using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Interviews
{
    // ============================================================
    // POST /api/interview/generate-code
    // ============================================================

    public record GenerateInterviewCodeCommand(Guid ApplicationId, int? RoundNumber, Guid HrUserId)
        : IRequest<Result<InterviewCode>>;

    public class GenerateInterviewCodeCommandHandler : IRequestHandler<GenerateInterviewCodeCommand, Result<InterviewCode>>
    {
        private readonly IInterviewCodeService _interviewCodeService;

        public GenerateInterviewCodeCommandHandler(IInterviewCodeService interviewCodeService)
        {
            _interviewCodeService = interviewCodeService;
        }

        public Task<Result<InterviewCode>> Handle(GenerateInterviewCodeCommand request, CancellationToken ct)
            => _interviewCodeService.GenerateCodeAsync(request.ApplicationId, request.RoundNumber, request.HrUserId, ct);
    }

    // ============================================================
    // POST /api/interview/generate-code-batch
    // ============================================================

    public record GenerateInterviewCodeBatchCommand(List<Guid> ApplicationIds, int? RoundNumber, Guid HrUserId)
        : IRequest<Result<List<InterviewCode>>>;

    public class GenerateInterviewCodeBatchCommandHandler : IRequestHandler<GenerateInterviewCodeBatchCommand, Result<List<InterviewCode>>>
    {
        private readonly IInterviewCodeService _interviewCodeService;

        public GenerateInterviewCodeBatchCommandHandler(IInterviewCodeService interviewCodeService)
        {
            _interviewCodeService = interviewCodeService;
        }

        public Task<Result<List<InterviewCode>>> Handle(GenerateInterviewCodeBatchCommand request, CancellationToken ct)
            => _interviewCodeService.GenerateBatchAsync(request.ApplicationIds, request.RoundNumber, request.HrUserId, ct);
    }

    // ============================================================
    // POST /api/interview/validate-code (Kiosk, anonymous)
    // ============================================================

    public record ValidateInterviewCodeCommand(string Code) : IRequest<Result<KioskSessionResponse>>;

    public class ValidateInterviewCodeCommandHandler : IRequestHandler<ValidateInterviewCodeCommand, Result<KioskSessionResponse>>
    {
        private readonly IInterviewCodeService _interviewCodeService;

        public ValidateInterviewCodeCommandHandler(IInterviewCodeService interviewCodeService)
        {
            _interviewCodeService = interviewCodeService;
        }

        public Task<Result<KioskSessionResponse>> Handle(ValidateInterviewCodeCommand request, CancellationToken ct)
            => _interviewCodeService.ValidateCodeAsync(request.Code, ct);
    }

    // ============================================================
    // GET /api/interview/codes?jobPostingId=
    // ============================================================

    public record GetInterviewCodesByJobQuery(Guid JobPostingId) : IRequest<Result<List<InterviewCodeSummaryDto>>>;

    public class GetInterviewCodesByJobQueryHandler : IRequestHandler<GetInterviewCodesByJobQuery, Result<List<InterviewCodeSummaryDto>>>
    {
        private readonly IInterviewCodeService _interviewCodeService;

        public GetInterviewCodesByJobQueryHandler(IInterviewCodeService interviewCodeService)
        {
            _interviewCodeService = interviewCodeService;
        }

        public async Task<Result<List<InterviewCodeSummaryDto>>> Handle(GetInterviewCodesByJobQuery request, CancellationToken ct)
            => Result.Success(await _interviewCodeService.GetCodesByJobAsync(request.JobPostingId, ct));
    }
}
