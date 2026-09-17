using System;
using ARI.Application.DTOs;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>Factory dựng entity/DTO cho test luồng Application (nộp hồ sơ → duyệt CV → đổi trạng thái).</summary>
internal static class ApplicationData
{
    public static JobPosting Job(
        Guid? owner = null, string status = "active", DateTimeOffset? deadline = null, int ttlHours = 48) => new()
    {
        CreatedByUserId = owner ?? Guid.NewGuid(),
        Title = "Backend Developer",
        Status = status,
        ApplicationDeadline = deadline,
        InviteTokenTtlHours = ttlHours,
    };

    public static ARI.Domain.Entities.Application Application(
        Guid jobId, Guid? accountId = null, string status = "cv_submitted", string email = "cand@example.io") => new()
    {
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = email,
        CandidateName = "Nguyen Van A",
        Status = status,
    };

    /// <summary>CvFileUrl mặc định null → không có gì để chấm; truyền <paramref name="cvFileUrl"/> để kiểm việc đưa vào hàng chấm.</summary>
    public static SubmitApplicationRequest SubmitRequest(
        Guid jobId, Guid? accountId = null, string? cvText = "Kinh nghiệm 5 năm C#/.NET", string? cvFileUrl = null) => new()
    {
        CvFileUrl = cvFileUrl,
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = "cand@example.io",
        CandidateName = "Nguyen Van A",
        CandidatePhone = "0900000000",
        CvText = cvText,
        CoverLetter = "Tôi rất phù hợp",
        NoticePeriod = "30 ngày",
    };

    public static CvJdAnalysis Analysis(Guid jobId, string cvHash, int score = 85) => new()
    {
        JobPostingId = jobId,
        CvHash = cvHash,
        MatchScore = score,
        Summary = "Phù hợp",
        Status = "completed",
        RubricDocumentId = Guid.NewGuid(), // chấm theo bộ tiêu chí (ADR-070) → điểm được hiện
    };

    public static InterviewSession PracticeSession(Guid appId, int round = 1) => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        SessionType = "practice",
    };
}
