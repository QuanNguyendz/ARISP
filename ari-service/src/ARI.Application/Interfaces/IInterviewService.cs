using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Domain.Entities;

namespace ARI.Application.Interfaces
{
    /// <summary>
    /// Lõi phỏng vấn AI — service dùng chung. QUAN TRỌNG: <c>SessionHub</c> gọi TRỰC TIẾP
    /// (không qua MediatR pipeline) để giữ latency critical path ~0.8–1.2s (ADR-006).
    /// </summary>
    public interface IInterviewService
    {
        Task<Result<string>> GetSpeechAudioAsync(Guid sessionId, string text, Guid? accountId, string? email, bool kioskAuthorized = false, CancellationToken ct = default);
        Task<Result<PracticeMediaConfigResponse>> GetMediaConfigAsync(Guid sessionId, Guid? accountId, string? email, bool kioskAuthorized = false, CancellationToken ct = default);
        Task<List<HrInterviewSessionItem>> GetSessionsForHrAsync(Guid? applicationId = null, CancellationToken ct = default);
        // ─── Interview Management: Job → Slot → Candidate ───
        Task<List<InterviewJobSummaryDto>> GetInterviewJobsAsync(CancellationToken ct = default);
        Task<List<InterviewSlotDetailDto>> GetSlotsForJobAsync(Guid jobPostingId, CancellationToken ct = default);
        Task<List<SlotCandidateDto>> GetCandidatesInSlotAsync(Guid slotId, CancellationToken ct = default);
        Task<Result<bool>> SendBookingReminderAsync(Guid bookingId, CancellationToken ct = default);
        Task<Result<bool>> RescheduleBookingAsync(Guid bookingId, Guid targetSlotId, CancellationToken ct = default);
        // ─────────────────────────────────────────────────────
        Task<Result<StartSessionResponse>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default);
        Task<Result<string>> GenerateAndSendNextQuestionAsync(Guid sessionId, CancellationToken ct = default);
        Task<Result<Answer>> SubmitAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default);
        Task<Result<Answer>> SaveAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default);
        Task AnalyzeAnswerAndAdaptAsync(Guid sessionId, Guid questionId, string transcript, CancellationToken ct = default);
        Task<Result<bool>> EndSessionAsync(Guid sessionId, string status = "completed", CancellationToken ct = default);
        Task<Result<bool>> PracticeTimeoutCloseAsync(Guid sessionId, CancellationToken ct = default);
        /// <summary>
        /// Ghi nhận tín hiệu nghi vấn của phiên (thoát toàn màn hình, chuyển tab…) và trả về
        /// số lần đã ghi của chính loại đó (ADR-054).
        /// </summary>
        Task<Result<int>> RecordCheatSignalAsync(Guid sessionId, string signalType, string? payloadJson, CancellationToken ct = default);
        /// <summary>Chấm lại phiên đã kết thúc bằng prompt hiện tại (dev/ops) — chặn nếu HR đã xác nhận.</summary>
        Task<Result<bool>> RegenerateEvaluationAsync(Guid sessionId, string? reportLanguage = null, CancellationToken ct = default);
        /// <summary>Lưu video buổi phỏng vấn THẬT vào storage kèm hạn xoá tự động (ADR-052).</summary>
        Task<Result<RecordingUploadResponse>> SaveRecordingAsync(Guid sessionId, byte[] content, string fileName, string contentType, CancellationToken ct = default);
        Task<Result<bool>> SubmitHrReviewAsync(Guid hrUserId, ConfirmReviewRequest request, string? frontendBaseUrl = null, CancellationToken ct = default);
    }
}
