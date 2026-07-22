using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Interviews
{
    // ============================================================
    // GET /api/interview/sessions (HR)
    // ============================================================

    public record GetHrInterviewSessionsQuery : IRequest<Result<List<HrInterviewSessionItem>>>;

    public class GetHrInterviewSessionsQueryHandler : IRequestHandler<GetHrInterviewSessionsQuery, Result<List<HrInterviewSessionItem>>>
    {
        private readonly IInterviewService _interviewService;

        public GetHrInterviewSessionsQueryHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public async Task<Result<List<HrInterviewSessionItem>>> Handle(GetHrInterviewSessionsQuery request, CancellationToken ct)
            => Result.Success(await _interviewService.GetSessionsForHrAsync(ct));
    }

    // ============================================================
    // POST /api/interview/session/start
    // ============================================================

    public record StartInterviewSessionCommand(StartSessionRequest Request) : IRequest<Result<StartSessionResponse>>;

    public class StartInterviewSessionCommandHandler : IRequestHandler<StartInterviewSessionCommand, Result<StartSessionResponse>>
    {
        private readonly IInterviewService _interviewService;

        public StartInterviewSessionCommandHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<StartSessionResponse>> Handle(StartInterviewSessionCommand request, CancellationToken ct)
            => _interviewService.StartSessionAsync(request.Request, ct);
    }

    // ============================================================
    // GET /api/interview/session/{id}/media-config
    // ============================================================

    public record GetMediaConfigQuery(Guid SessionId, Guid? AccountId, string? Email) : IRequest<Result<PracticeMediaConfigResponse>>;

    public class GetMediaConfigQueryHandler : IRequestHandler<GetMediaConfigQuery, Result<PracticeMediaConfigResponse>>
    {
        private readonly IInterviewService _interviewService;

        public GetMediaConfigQueryHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<PracticeMediaConfigResponse>> Handle(GetMediaConfigQuery request, CancellationToken ct)
            => _interviewService.GetMediaConfigAsync(request.SessionId, request.AccountId, request.Email, ct);
    }

    // ============================================================
    // POST /api/interview/session/{id}/tts
    // ============================================================

    public record SynthesizeSpeechCommand(Guid SessionId, string Text, Guid? AccountId, string? Email) : IRequest<Result<string>>;

    public class SynthesizeSpeechCommandHandler : IRequestHandler<SynthesizeSpeechCommand, Result<string>>
    {
        private readonly IInterviewService _interviewService;

        public SynthesizeSpeechCommandHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<string>> Handle(SynthesizeSpeechCommand request, CancellationToken ct)
            => _interviewService.GetSpeechAudioAsync(request.SessionId, request.Text, request.AccountId, request.Email, ct);
    }

    // ============================================================
    // POST /api/interview/session/{id}/answer
    // ============================================================

    public record SubmitAnswerCommand(Guid SessionId, Guid QuestionId, string Transcript, int? ResponseTimeMs) : IRequest<Result<Answer>>;

    public class SubmitAnswerCommandHandler : IRequestHandler<SubmitAnswerCommand, Result<Answer>>
    {
        private readonly IInterviewService _interviewService;

        public SubmitAnswerCommandHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<Answer>> Handle(SubmitAnswerCommand request, CancellationToken ct)
            => _interviewService.SubmitAnswerAsync(request.SessionId, request.QuestionId, request.Transcript, request.ResponseTimeMs, ct);
    }

    // ============================================================
    // POST /api/interview/session/{id}/end
    // ============================================================

    public record EndInterviewSessionCommand(Guid SessionId, string Status) : IRequest<Result<bool>>;

    public class EndInterviewSessionCommandHandler : IRequestHandler<EndInterviewSessionCommand, Result<bool>>
    {
        private readonly IInterviewService _interviewService;

        public EndInterviewSessionCommandHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<bool>> Handle(EndInterviewSessionCommand request, CancellationToken ct)
            => _interviewService.EndSessionAsync(request.SessionId, request.Status, ct);
    }

    // ============================================================
    // POST /api/interview/review/confirm (HR Confirm/Override — Phase 6)
    // ============================================================

    public record ConfirmHrReviewCommand(Guid HrUserId, ConfirmReviewRequest Request) : IRequest<Result<bool>>;

    public class ConfirmHrReviewCommandHandler : IRequestHandler<ConfirmHrReviewCommand, Result<bool>>
    {
        private readonly IInterviewService _interviewService;
        private readonly IConfiguration _configuration;

        public ConfirmHrReviewCommandHandler(IInterviewService interviewService, IConfiguration configuration)
        {
            _interviewService = interviewService;
            _configuration = configuration;
        }

        public Task<Result<bool>> Handle(ConfirmHrReviewCommand request, CancellationToken ct)
        {
            var candidateBaseUrl = _configuration["Frontend:CandidateBaseUrl"]
                ?? _configuration["Authentication:AdminFrontendUrl"]
                ?? "http://localhost:3000";
            return _interviewService.SubmitHrReviewAsync(request.HrUserId, request.Request, candidateBaseUrl, ct);
        }
    }
}
