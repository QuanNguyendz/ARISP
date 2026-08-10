using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Application.Services;
using ARI.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// Dựng <see cref="InterviewService"/> cho unit test luồng HR Review. Service có các dependency nhưng
/// <c>SubmitHrReviewAsync</c> chỉ chạm <see cref="IUnitOfWork"/> + <see cref="INotificationService"/>;
/// các dependency còn lại được cắm stub.
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
        new ThrowingTTSService(),
        new ThrowingFileStorageService(),
        new TestScopeFactory(uow),
        new MemoryCache(new MemoryCacheOptions()));

    /// <summary>
    /// Overload cho luồng phỏng vấn thử (Luồng 6): cắm AI provider + TTS điều khiển được để test sinh
    /// câu hỏi / chấm điểm / đóng phiên.
    /// </summary>
    public static InterviewService Create(
        IUnitOfWork uow, INotificationService notif, IAIProvider ai, ITTSService tts, InterviewOptions? options = null) => new(
        uow,
        ai,
        new ThrowingEmbeddingProvider(),
        new ThrowingAvatarService(),
        notif,
        new ThrowingDeepgramTokenService(),
        new ThrowingRagIngestionService(),
        tts,
        new ThrowingFileStorageService(),
        new TestScopeFactory(uow),
        new MemoryCache(new MemoryCacheOptions()),
        options);

    /// <summary>
    /// Overload cho luồng lưu video Kiosk (ADR-052): cắm <see cref="IFileStorageService"/> thật
    /// (thường là <see cref="RecordingFileStorage"/>) + <see cref="InterviewOptions"/> để test
    /// giới hạn dung lượng / hạn lưu / ghi đè file cũ.
    /// </summary>
    public static InterviewService Create(
        IUnitOfWork uow, INotificationService notif, IFileStorageService storage, InterviewOptions? options = null) => new(
        uow,
        new ThrowingAIProvider(),
        new ThrowingEmbeddingProvider(),
        new ThrowingAvatarService(),
        notif,
        new ThrowingDeepgramTokenService(),
        new ThrowingRagIngestionService(),
        new ThrowingTTSService(),
        storage,
        new TestScopeFactory(uow),
        new MemoryCache(new MemoryCacheOptions()),
        options);

    private sealed class TestScopeFactory : IServiceScopeFactory, IServiceScope
    {
        private readonly IServiceProvider _provider;
        public TestScopeFactory(IUnitOfWork uow)
        {
            var services = new ServiceCollection();
            services.AddSingleton(uow);
            _provider = services.BuildServiceProvider();
        }
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => _provider;
        public void Dispose() { }
    }

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

    /// <summary>Storage chỉ dùng ở luồng lưu video Kiosk (ADR-052) — không chạm trong các test này.</summary>
    private sealed class ThrowingFileStorageService : IFileStorageService
    {
        public Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> GetUrlAsync(string storageKey, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> GetDownloadUrlAsync(string storageKey, string downloadFileName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(string storageKey, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<byte[]?> ReadAllBytesAsync(string storageKey, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
