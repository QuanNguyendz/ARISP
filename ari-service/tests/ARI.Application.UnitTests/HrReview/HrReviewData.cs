using System;
using ARI.Application.Evaluations;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.HrReview;

/// <summary>Factory dựng entity cho test luồng HR Review &amp; Confirm/Override (Phase 6).</summary>
internal static class HrReviewData
{
    public static JobPosting Job(Guid? owner = null) => new()
    {
        CreatedByUserId = owner ?? Guid.NewGuid(),
        Title = "Backend Developer",
    };

    public static ARI.Domain.Entities.Application Application(
        Guid jobId, Guid? accountId, string status = "interview", string email = "cand@example.io") => new()
    {
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = email,
        CandidateName = "Nguyen Van A",
        Status = status,
    };

    public static Evaluation Evaluation(
        Guid applicationId, string aiVerdict = "pass", string sessionType = "real", int round = 1) => new()
    {
        ApplicationId = applicationId,
        SessionId = Guid.NewGuid(),
        AiVerdict = aiVerdict,
        SessionType = sessionType,
        RoundNumber = round,
        OverallScore = 80,
    };

    /// <summary>Nhân sự nội bộ review (mặc định HR Admin — đủ quyền override).</summary>
    public static User Reviewer(string role = AppRoles.HrAdmin) => new()
    {
        Email = "hr@example.io",
        Role = role,
    };

    public static InterviewRoundConfig RoundConfig(Guid jobId, int round, string type = "technical") => new()
    {
        JobPostingId = jobId,
        RoundNumber = round,
        RoundType = type,
    };

    public static ConfirmReviewRequest Request(
        Guid evaluationId, string finalVerdict = "pass", string? overrideReason = null) => new()
    {
        EvaluationId = evaluationId,
        FinalVerdict = finalVerdict,
        OverrideReason = overrideReason,
        ShareEvaluation = true,
    };
}
