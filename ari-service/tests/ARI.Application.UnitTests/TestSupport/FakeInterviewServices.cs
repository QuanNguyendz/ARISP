using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// IInterviewCodeService giả cho test các wrapper CQRS Interview Code (forward + propagate): trả kết quả nạp sẵn
/// hoặc ném lỗi, ghi lại tham số + số lần gọi để assert handler forward đúng.
/// </summary>
public sealed class FakeInterviewCodeService : IInterviewCodeService
{
    public Result<InterviewCode> GenerateResult { get; set; } = Result.Success(new InterviewCode());
    public Exception? GenerateThrows { get; set; }
    public (Guid AppId, int? Round, Guid Hr)? LastGenerate { get; private set; }

    public Result<List<InterviewCode>> BatchResult { get; set; } = Result.Success(new List<InterviewCode>());
    public Exception? BatchThrows { get; set; }
    public (List<Guid> AppIds, int? Round, Guid Hr)? LastBatch { get; private set; }

    public Result<KioskSessionResponse> ValidateResult { get; set; } = Result.Success(new KioskSessionResponse());
    public Exception? ValidateThrows { get; set; }
    public string? LastValidatedCode { get; private set; }
    public int ValidateCallCount { get; private set; }

    public List<InterviewCodeSummaryDto> CodesByJob { get; set; } = new();
    public Exception? CodesByJobThrows { get; set; }
    public Guid? LastCodesJobId { get; private set; }
    public int CodesByJobCallCount { get; private set; }

    public Task<Result<InterviewCode>> GenerateCodeAsync(Guid applicationId, int? roundNumber, Guid createdByUserId, CancellationToken ct = default)
    {
        LastGenerate = (applicationId, roundNumber, createdByUserId);
        if (GenerateThrows != null) throw GenerateThrows;
        return Task.FromResult(GenerateResult);
    }

    public Task<Result<List<InterviewCode>>> GenerateBatchAsync(List<Guid> applicationIds, int? roundNumber, Guid createdByUserId, CancellationToken ct = default)
    {
        LastBatch = (applicationIds, roundNumber, createdByUserId);
        if (BatchThrows != null) throw BatchThrows;
        return Task.FromResult(BatchResult);
    }

    public Task<Result<KioskSessionResponse>> ValidateCodeAsync(string code, CancellationToken ct = default)
    {
        ValidateCallCount++;
        LastValidatedCode = code;
        if (ValidateThrows != null) throw ValidateThrows;
        return Task.FromResult(ValidateResult);
    }

    public Task<List<InterviewCodeSummaryDto>> GetCodesByJobAsync(Guid jobPostingId, CancellationToken ct = default)
    {
        CodesByJobCallCount++;
        LastCodesJobId = jobPostingId;
        if (CodesByJobThrows != null) throw CodesByJobThrows;
        return Task.FromResult(CodesByJob);
    }
}

/// <summary>
/// IInterviewService giả cho test các wrapper CQRS phiên phỏng vấn (forward + propagate). Chỉ cấu hình 7 hàm
/// report cần (sessions/start/media/tts/answer/end/hr-review); các hàm khác chưa dùng nên ném NotImplemented.
/// </summary>
public sealed class FakeInterviewService : IInterviewService
{
    public List<HrInterviewSessionItem> HrSessions { get; set; } = new();
    public Exception? HrSessionsThrows { get; set; }
    public Guid? LastHrApplicationId { get; private set; }

    public Result<StartSessionResponse> StartResult { get; set; } = Result.Success(new StartSessionResponse());
    public Exception? StartThrows { get; set; }
    public StartSessionRequest? LastStartRequest { get; private set; }

    public Result<PracticeMediaConfigResponse> MediaResult { get; set; } = Result.Success(new PracticeMediaConfigResponse());
    public Exception? MediaThrows { get; set; }
    public (Guid Session, Guid? Account, string? Email, bool Kiosk)? LastMedia { get; private set; }

    public Result<string> SpeechResult { get; set; } = Result.Success("audio-data-or-url");
    public Exception? SpeechThrows { get; set; }
    public (Guid Session, string Text, Guid? Account, string? Email, bool Kiosk)? LastSpeech { get; private set; }

    public Result<Answer> AnswerResult { get; set; } = Result.Success(new Answer());
    public Exception? AnswerThrows { get; set; }
    public (Guid Session, Guid Question, string Transcript, int? ResponseTimeMs)? LastAnswer { get; private set; }

    public Result<bool> EndResult { get; set; } = Result.Success(true);
    public Exception? EndThrows { get; set; }
    public (Guid Session, string Status)? LastEnd { get; private set; }

    public Result<bool> HrReviewResult { get; set; } = Result.Success(true);
    public Exception? HrReviewThrows { get; set; }
    public (Guid Hr, ConfirmReviewRequest Request, string? BaseUrl)? LastHrReview { get; private set; }

    public Task<List<HrInterviewSessionItem>> GetSessionsForHrAsync(Guid? applicationId = null, CancellationToken ct = default)
    {
        LastHrApplicationId = applicationId;
        if (HrSessionsThrows != null) throw HrSessionsThrows;
        return Task.FromResult(HrSessions);
    }

    public Task<Result<StartSessionResponse>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default)
    {
        LastStartRequest = request;
        if (StartThrows != null) throw StartThrows;
        return Task.FromResult(StartResult);
    }

    public Task<Result<PracticeMediaConfigResponse>> GetMediaConfigAsync(Guid sessionId, Guid? accountId, string? email, bool kioskAuthorized = false, CancellationToken ct = default)
    {
        LastMedia = (sessionId, accountId, email, kioskAuthorized);
        if (MediaThrows != null) throw MediaThrows;
        return Task.FromResult(MediaResult);
    }

    public Task<Result<string>> GetSpeechAudioAsync(Guid sessionId, string text, Guid? accountId, string? email, bool kioskAuthorized = false, CancellationToken ct = default)
    {
        LastSpeech = (sessionId, text, accountId, email, kioskAuthorized);
        if (SpeechThrows != null) throw SpeechThrows;
        return Task.FromResult(SpeechResult);
    }

    public Task<Result<Answer>> SubmitAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default)
    {
        LastAnswer = (sessionId, questionId, transcript, responseTimeMs);
        if (AnswerThrows != null) throw AnswerThrows;
        return Task.FromResult(AnswerResult);
    }

    public Task<Result<bool>> EndSessionAsync(Guid sessionId, string status = "completed", CancellationToken ct = default)
    {
        LastEnd = (sessionId, status);
        if (EndThrows != null) throw EndThrows;
        return Task.FromResult(EndResult);
    }

    public Task<Result<bool>> SubmitHrReviewAsync(Guid hrUserId, ConfirmReviewRequest request, string? frontendBaseUrl = null, CancellationToken ct = default)
    {
        LastHrReview = (hrUserId, request, frontendBaseUrl);
        if (HrReviewThrows != null) throw HrReviewThrows;
        return Task.FromResult(HrReviewResult);
    }

    // ── Không dùng trong test forwarder — ném NotImplemented ──
    public Task<List<InterviewJobSummaryDto>> GetInterviewJobsAsync(Guid? userId, string? role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<List<InterviewSlotDetailDto>>> GetSlotsForJobAsync(Guid jobPostingId, Guid? userId, string? role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<List<SlotCandidateDto>>> GetCandidatesInSlotAsync(Guid slotId, Guid? userId, string? role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> SendBookingReminderAsync(Guid bookingId, Guid? userId, string? role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> RescheduleBookingAsync(Guid bookingId, Guid targetSlotId, Guid? userId, string? role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<RescheduleResultDto>> RescheduleBookingsAsync(IReadOnlyList<Guid> bookingIds, Guid targetSlotId, Guid? userId, string? role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<string>> GenerateAndSendNextQuestionAsync(Guid sessionId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<Answer>> SaveAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AnalyzeAnswerAndAdaptAsync(Guid sessionId, Guid questionId, string transcript, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> PracticeTimeoutCloseAsync(Guid sessionId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<int>> RecordCheatSignalAsync(Guid sessionId, string signalType, string? payloadJson, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> RegenerateEvaluationAsync(Guid sessionId, string? reportLanguage = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<RecordingUploadResponse>> SaveRecordingAsync(Guid sessionId, byte[] content, string fileName, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
}
