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

    public record GetHrInterviewSessionsQuery(Guid? ApplicationId = null) : IRequest<Result<List<HrInterviewSessionItem>>>;

    public class GetHrInterviewSessionsQueryHandler : IRequestHandler<GetHrInterviewSessionsQuery, Result<List<HrInterviewSessionItem>>>
    {
        private readonly IInterviewService _interviewService;

        public GetHrInterviewSessionsQueryHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public async Task<Result<List<HrInterviewSessionItem>>> Handle(GetHrInterviewSessionsQuery request, CancellationToken ct)
            => Result.Success(await _interviewService.GetSessionsForHrAsync(request.ApplicationId, ct));
    }

    // ============================================================
    // GET /api/interview/management/jobs
    // ============================================================

    public record GetInterviewJobsQuery(Guid? UserId, string? Role) : IRequest<Result<List<InterviewJobSummaryDto>>>;

    public class GetInterviewJobsQueryHandler : IRequestHandler<GetInterviewJobsQuery, Result<List<InterviewJobSummaryDto>>>
    {
        private readonly IInterviewService _interviewService;
        public GetInterviewJobsQueryHandler(IInterviewService interviewService) { _interviewService = interviewService; }
        public async Task<Result<List<InterviewJobSummaryDto>>> Handle(GetInterviewJobsQuery request, CancellationToken ct)
            => Result.Success(await _interviewService.GetInterviewJobsAsync(request.UserId, request.Role, ct));
    }

    // ============================================================
    // GET /api/interview/management/jobs/{jobId}/slots
    // ============================================================

    public record GetSlotsForJobQuery(Guid JobPostingId, Guid? UserId, string? Role) : IRequest<Result<List<InterviewSlotDetailDto>>>;

    public class GetSlotsForJobQueryHandler : IRequestHandler<GetSlotsForJobQuery, Result<List<InterviewSlotDetailDto>>>
    {
        private readonly IInterviewService _interviewService;
        public GetSlotsForJobQueryHandler(IInterviewService interviewService) { _interviewService = interviewService; }
        public async Task<Result<List<InterviewSlotDetailDto>>> Handle(GetSlotsForJobQuery request, CancellationToken ct)
            => await _interviewService.GetSlotsForJobAsync(request.JobPostingId, request.UserId, request.Role, ct);
    }

    // ============================================================
    // GET /api/interview/management/slots/{slotId}/candidates
    // ============================================================

    public record GetCandidatesInSlotQuery(Guid SlotId, Guid? UserId, string? Role) : IRequest<Result<List<SlotCandidateDto>>>;

    public class GetCandidatesInSlotQueryHandler : IRequestHandler<GetCandidatesInSlotQuery, Result<List<SlotCandidateDto>>>
    {
        private readonly IInterviewService _interviewService;
        public GetCandidatesInSlotQueryHandler(IInterviewService interviewService) { _interviewService = interviewService; }
        public async Task<Result<List<SlotCandidateDto>>> Handle(GetCandidatesInSlotQuery request, CancellationToken ct)
            => await _interviewService.GetCandidatesInSlotAsync(request.SlotId, request.UserId, request.Role, ct);
    }

    // ============================================================
    // POST /api/interview/management/booking/{bookingId}/remind
    // ============================================================

    public record SendBookingReminderCommand(Guid BookingId, Guid? UserId, string? Role) : IRequest<Result<bool>>;

    public class SendBookingReminderCommandHandler : IRequestHandler<SendBookingReminderCommand, Result<bool>>
    {
        private readonly IInterviewService _interviewService;
        public SendBookingReminderCommandHandler(IInterviewService interviewService) { _interviewService = interviewService; }
        public async Task<Result<bool>> Handle(SendBookingReminderCommand request, CancellationToken ct)
            => await _interviewService.SendBookingReminderAsync(request.BookingId, request.UserId, request.Role, ct);
    }

    // ============================================================
    // POST /api/interview/management/booking/{bookingId}/reschedule
    // ============================================================

    public record RescheduleBookingCommand(Guid BookingId, Guid TargetSlotId, Guid? UserId, string? Role) : IRequest<Result<bool>>;

    public class RescheduleBookingCommandHandler : IRequestHandler<RescheduleBookingCommand, Result<bool>>
    {
        private readonly IInterviewService _interviewService;
        public RescheduleBookingCommandHandler(IInterviewService interviewService) { _interviewService = interviewService; }
        public async Task<Result<bool>> Handle(RescheduleBookingCommand request, CancellationToken ct)
            => await _interviewService.RescheduleBookingAsync(request.BookingId, request.TargetSlotId, request.UserId, request.Role, ct);
    }

    // ============================================================
    // POST /api/interview/management/bookings/reschedule — dời NHIỀU ứng viên trong 1 lần
    // ============================================================

    public record RescheduleBookingsCommand(IReadOnlyList<Guid> BookingIds, Guid TargetSlotId, Guid? UserId, string? Role)
        : IRequest<Result<RescheduleResultDto>>;

    public class RescheduleBookingsCommandHandler : IRequestHandler<RescheduleBookingsCommand, Result<RescheduleResultDto>>
    {
        private readonly IInterviewService _interviewService;
        public RescheduleBookingsCommandHandler(IInterviewService interviewService) { _interviewService = interviewService; }
        public async Task<Result<RescheduleResultDto>> Handle(RescheduleBookingsCommand request, CancellationToken ct)
            => await _interviewService.RescheduleBookingsAsync(request.BookingIds, request.TargetSlotId, request.UserId, request.Role, ct);
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

    /// <param name="KioskAuthorized">Token Kiosk đã được xác thực đúng phiên (ADR-052) — bỏ qua kiểm tra chủ sở hữu.</param>
    public record GetMediaConfigQuery(Guid SessionId, Guid? AccountId, string? Email, bool KioskAuthorized = false)
        : IRequest<Result<PracticeMediaConfigResponse>>;

    public class GetMediaConfigQueryHandler : IRequestHandler<GetMediaConfigQuery, Result<PracticeMediaConfigResponse>>
    {
        private readonly IInterviewService _interviewService;

        public GetMediaConfigQueryHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<PracticeMediaConfigResponse>> Handle(GetMediaConfigQuery request, CancellationToken ct)
            => _interviewService.GetMediaConfigAsync(request.SessionId, request.AccountId, request.Email, request.KioskAuthorized, ct);
    }

    // ============================================================
    // POST /api/interview/session/{id}/tts
    // ============================================================

    public record SynthesizeSpeechCommand(Guid SessionId, string Text, Guid? AccountId, string? Email, bool KioskAuthorized = false)
        : IRequest<Result<string>>;

    public class SynthesizeSpeechCommandHandler : IRequestHandler<SynthesizeSpeechCommand, Result<string>>
    {
        private readonly IInterviewService _interviewService;

        public SynthesizeSpeechCommandHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<string>> Handle(SynthesizeSpeechCommand request, CancellationToken ct)
            => _interviewService.GetSpeechAudioAsync(request.SessionId, request.Text, request.AccountId, request.Email, request.KioskAuthorized, ct);
    }

    // ============================================================
    // POST /api/interview/session/{id}/signals — tín hiệu nghi vấn (ADR-054)
    // ============================================================

    public record ReportCheatSignalCommand(Guid SessionId, string SignalType, string? Payload) : IRequest<Result<int>>;

    public class ReportCheatSignalCommandHandler : IRequestHandler<ReportCheatSignalCommand, Result<int>>
    {
        private readonly IInterviewService _interviewService;

        public ReportCheatSignalCommandHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<int>> Handle(ReportCheatSignalCommand request, CancellationToken ct)
            => _interviewService.RecordCheatSignalAsync(request.SessionId, request.SignalType, request.Payload, ct);
    }

    // ============================================================
    // POST /api/interview/session/{id}/recording — Kiosk tải video buổi thật (ADR-052)
    // ============================================================

    public record UploadRecordingCommand(Guid SessionId, byte[] Content, string FileName, string ContentType)
        : IRequest<Result<RecordingUploadResponse>>;

    public class UploadRecordingCommandHandler : IRequestHandler<UploadRecordingCommand, Result<RecordingUploadResponse>>
    {
        private readonly IInterviewService _interviewService;

        public UploadRecordingCommandHandler(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public Task<Result<RecordingUploadResponse>> Handle(UploadRecordingCommand request, CancellationToken ct)
            => _interviewService.SaveRecordingAsync(request.SessionId, request.Content, request.FileName, request.ContentType, ct);
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
