using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Services;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// Dựng <see cref="InterviewService"/> cho unit test luồng HR Review. Service có 8 dependency nhưng
/// <c>SubmitHrReviewAsync</c> chỉ chạm <see cref="IUnitOfWork"/> + <see cref="INotificationService"/>;
/// 6 dependency media/AI còn lại được cắm stub ném lỗi (không được gọi trong luồng này).
/// </summary>
internal static class InterviewServiceFactory
{
    public static InterviewService Create(IUnitOfWork uow, INotificationService notif) => new(
        uow,
        new ThrowingAIProvider(),
        new ThrowingEmbeddingProvider(),
        new ThrowingAvatarService(),
        notif,
        new ThrowingDeepgramTokenService(),
        new ThrowingRagIngestionService(),
        new ThrowingTTSService());

    private sealed class ThrowingAIProvider : IAIProvider
    {
        public IAsyncEnumerable<string> StreamQuestionAsync(QuestionContext ctx, CancellationToken ct) => throw new NotImplementedException();
        public Task<AnswerAnalysis> AnalyzeAnswerAsync(AnswerContext ctx, CancellationToken ct) => throw new NotImplementedException();
        public Task<EvaluationReport> GenerateEvaluationAsync(SessionContext ctx, CancellationToken ct) => throw new NotImplementedException();
        public Task<string> DetectLanguageRequirementAsync(string jdText, CancellationToken ct) => throw new NotImplementedException();
        public Task<LanguageAssessment> AssessLanguageProficiencyAsync(SessionContext ctx, CancellationToken ct) => throw new NotImplementedException();
        public Task<string> CompleteJsonAsync(string systemInstruction, string userContent, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class ThrowingEmbeddingProvider : IEmbeddingProvider
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IEnumerable<DocumentChunk>> RetrieveAsync(Guid? sourceId, float[] queryVector, int topK = 5, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class ThrowingAvatarService : IAvatarService
    {
        public Task<SdpMessage> StartSessionAsync(string voiceId, string style, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> SubmitSdpAnswerAsync(string sessionId, SdpMessage sdpAnswer, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> SendIceCandidateAsync(string sessionId, IceCandidateMessage candidate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> SpeakTextAsync(string sessionId, string text, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> StopSessionAsync(string sessionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AvatarStreamingToken?> CreateStreamingTokenAsync(string? avatarId, string? voiceId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class ThrowingDeepgramTokenService : IDeepgramTokenService
    {
        public Task<DeepgramToken?> CreateTemporaryTokenAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class ThrowingRagIngestionService : IRagIngestionService
    {
        public Task<int> IngestAsync(string sourceType, Guid sourceId, string text, string? scope = null,
            string? documentType = null, bool replaceExisting = true, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class ThrowingTTSService : ITTSService
    {
        public Task<Stream> TextToSpeechAsync(string text, string voiceId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> TextToSpeechBase64PcmAsync(string text, string voiceId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
