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
        Task<Result<string>> GetSpeechAudioAsync(Guid sessionId, string text, Guid? accountId, string? email, CancellationToken ct = default);
        Task<Result<PracticeMediaConfigResponse>> GetMediaConfigAsync(Guid sessionId, Guid? accountId, string? email, CancellationToken ct = default);
        Task<List<HrInterviewSessionItem>> GetSessionsForHrAsync(CancellationToken ct = default);
        Task<Result<StartSessionResponse>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default);
        Task<Result<string>> GenerateAndSendNextQuestionAsync(Guid sessionId, CancellationToken ct = default);
        Task<Result<Answer>> SubmitAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default);
        Task<Result<Answer>> SaveAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default);
        Task AnalyzeAnswerAndAdaptAsync(Guid sessionId, Guid questionId, string transcript, CancellationToken ct = default);
        Task<Result<bool>> EndSessionAsync(Guid sessionId, string status = "completed", CancellationToken ct = default);
        Task<Result<bool>> PracticeTimeoutCloseAsync(Guid sessionId, CancellationToken ct = default);
        Task<Result<bool>> SubmitHrReviewAsync(Guid hrUserId, ConfirmReviewRequest request, string? frontendBaseUrl = null, CancellationToken ct = default);
    }
}
