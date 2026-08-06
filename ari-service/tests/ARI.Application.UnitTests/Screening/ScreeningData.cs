using System;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.Screening;

/// <summary>
/// Factory dựng entity cho test Luồng 4 — Sàng lọc hồ sơ (UC-53/54/55/56/57): danh sách ứng viên theo job,
/// chi tiết ứng viên, làm giàu dữ liệu (vòng hiện tại, điểm phỏng vấn, lịch hẹn) trong <c>MapApplications</c>.
/// </summary>
internal static class ScreeningData
{
    public static JobPosting Job(Guid? owner = null, string title = "Backend Developer", string status = "active") => new()
    {
        CreatedByUserId = owner ?? Guid.NewGuid(),
        Title = title,
        Status = status,
    };

    public static ARI.Domain.Entities.Application App(
        Guid jobId,
        string status = "cv_submitted",
        Guid? accountId = null,
        string? cvText = null,
        string? cvFileUrl = null,
        Guid? analysisId = null,
        DateTimeOffset? createdAt = null,
        string email = "cand@example.io",
        string name = "Nguyen Van A") => new()
    {
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = email,
        CandidateName = name,
        CandidatePhone = "0900000000",
        CvText = cvText,
        CvFileUrl = cvFileUrl,
        CoverLetter = "Thư giới thiệu",
        NoticePeriod = "30 ngày",
        Status = status,
        CvJdAnalysisId = analysisId,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
    };

    public static CvJdAnalysis Analysis(Guid jobId, int score = 90, string summary = "Rất phù hợp") => new()
    {
        JobPostingId = jobId,
        CvHash = Guid.NewGuid().ToString("N"),
        Status = "completed",
        MatchScore = score,
        Summary = summary,
        AiModel = "Gemini",
    };

    public static AvailabilitySlot Slot(Guid jobId, DateTimeOffset startTime, int round = 1) => new()
    {
        JobPostingId = jobId,
        RoundNumber = round,
        StartTime = startTime,
        EndTime = startTime.AddMinutes(30),
    };

    public static InterviewBooking Booking(
        Guid appId, Guid slotId, int round = 1, string status = "scheduled",
        string confirmationStatus = "pending", string? declineReason = null,
        DateTimeOffset? respondedAt = null) => new()
    {
        ApplicationId = appId,
        AvailabilitySlotId = slotId,
        RoundNumber = round,
        Status = status,
        ConfirmationStatus = confirmationStatus,
        DeclineReason = declineReason,
        RespondedAt = respondedAt,
    };

    public static InterviewInvite Invite(Guid appId, int round) => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        TokenHash = "hash",
    };

    public static InterviewSession Session(Guid appId, int round, string type = "real") => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        SessionType = type,
    };

    public static Evaluation Eval(Guid appId, int round, decimal score, string sessionType = "real") => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        SessionType = sessionType,
        OverallScore = score,
    };

    public static User Staff(Guid id, string role = "hr_admin") => new()
    {
        Id = id,
        Email = "staff@example.io",
        FullName = "Nguoi Tao Tin",
        Role = role,
    };
}
