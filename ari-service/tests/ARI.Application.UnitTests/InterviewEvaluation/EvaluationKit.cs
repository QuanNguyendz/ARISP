using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Application.InterviewRubrics;
using ARI.Application.UnitTests.PracticeInterview;
using ARI.Application.UnitTests.TestSupport;

namespace ARI.Application.UnitTests.InterviewEvaluation;

/// <summary>Hàng đợi chấm báo cáo giả — ghi lại phiên nào được đưa vào hàng (ADR-073).</summary>
public sealed class RecordingEvaluationQueue : IEvaluationQueue
{
    public List<Guid> Enqueued { get; } = new();

    public void Enqueue(Guid sessionId) => Enqueued.Add(sessionId);

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        => throw new NotSupportedException("Test không tiêu thụ hàng đợi.");

    public void Complete(Guid sessionId) { }
}

/// <summary>Dựng các thành phần của pipeline chấm báo cáo phỏng vấn cho unit test (ADR-073).</summary>
internal static class EvaluationKit
{
    public static InterviewEvaluator Evaluator(
        InMemoryUnitOfWork uow, StubAiProvider? ai = null, RecordingNotificationService? notif = null)
        => new(uow, ai ?? new StubAiProvider(), notif ?? new RecordingNotificationService());

    public static InterviewRubricService RubricService(
        InMemoryUnitOfWork uow, RecordingEvaluationQueue? queue = null, RecordingRagIngestionService? rag = null)
        => new(uow, new RecordingFileStorage(), rag ?? new RecordingRagIngestionService(),
            Evaluator(uow), queue ?? new RecordingEvaluationQueue());

    /// <summary>
    /// Đóng phiên (<c>completed</c>) và — trừ khi <paramref name="withAnswer"/> = false — gieo một lượt hỏi–đáp có
    /// lời, rồi chạy bộ chấm y như hàng đợi nền làm sau khi đóng phiên.
    /// </summary>
    public static Task<EvaluationOutcome> CompleteAndEvaluateAsync(
        InMemoryUnitOfWork uow, ARI.Domain.Entities.InterviewSession session,
        StubAiProvider? ai = null, RecordingNotificationService? notif = null, bool withAnswer = true)
    {
        session.Status = "completed";
        session.EndedAt ??= DateTimeOffset.UtcNow;
        if (withAnswer && !System.Linq.Enumerable.Any(uow.Repo<ARI.Domain.Entities.Answer>().Items, a => a.SessionId == session.Id))
        {
            var q = PracticeData.Question(session.Id, 99);
            uow.Seed(q).Seed(PracticeData.Answer(session.Id, q.Id));
        }
        return Evaluator(uow, ai, notif).EvaluateSessionAsync(session.Id, CancellationToken.None);
    }
}
