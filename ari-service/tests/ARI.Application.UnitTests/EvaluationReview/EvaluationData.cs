using System;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.EvaluationReview;

/// <summary>
/// Factory dựng entity cho test Luồng 8 — Review Interview Result (UC-64/84–90/95): HR xem danh sách/chi tiết
/// đánh giá + giám sát phiên. Đánh giá/phiên "practice" luôn bị ẩn khỏi nhân sự nội bộ (ADR-051).
/// </summary>
internal static class EvaluationData
{
    public static JobPosting Job(string title = "Backend Developer", Guid? owner = null) => new()
    {
        Title = title,
        JobDescription = "Mô tả",
        Status = "active",
        CreatedByUserId = owner ?? Guid.NewGuid(),
    };

    public static ARI.Domain.Entities.Application App(
        Guid jobId, string name = "Nguyen Van A", string email = "cand@example.io") => new()
    {
        JobPostingId = jobId,
        CandidateName = name,
        CandidateEmail = email,
        Status = "interview",
    };

    public static Evaluation Eval(
        Guid appId, Guid? sessionId = null, int round = 1, string type = "real",
        string verdict = "pass", decimal score = 80m, decimal cheat = 0m, DateTimeOffset? createdAt = null) => new()
    {
        SessionId = sessionId ?? Guid.NewGuid(),
        ApplicationId = appId,
        RoundNumber = round,
        SessionType = type,
        AiVerdict = verdict,
        OverallScore = score,
        CheatScore = cheat,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
    };

    public static ARI.Domain.Entities.HrReview Review(Guid evalId, string finalVerdict = "pass", bool isOverride = false) => new()
    {
        EvaluationId = evalId,
        ReviewedByUserId = Guid.NewGuid(),
        FinalVerdict = finalVerdict,
        IsOverride = isOverride,
    };

    public static InterviewSession Session(
        Guid appId, int round = 1, string type = "real", string status = "completed",
        DateTimeOffset? createdAt = null, string? recordingUrl = null, DateTimeOffset? recordingExpiresAt = null) => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        RoundType = round == 1 ? "screening" : "technical",
        SessionType = type,
        Status = status,
        InterviewLanguage = "vi",
        RecordingUrl = recordingUrl,
        RecordingExpiresAt = recordingExpiresAt,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        StartedAt = createdAt ?? DateTimeOffset.UtcNow,
    };
}
