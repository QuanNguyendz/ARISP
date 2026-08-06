using System;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.InterviewCodes;

/// <summary>
/// Factory dựng entity + service cho test Luồng 7 — Conduct Official Interview (UC-44/65/66/67),
/// phần lõi <see cref="InterviewCodeService"/>: cấp mã (gate đã đặt lịch), xác thực mã tại Kiosk
/// (tạo phiên thật + mint token), liệt kê mã cho HR.
/// </summary>
internal static class InterviewCodeData
{
    /// <summary>Job KHÔNG set persona → StartSessionAsync bỏ qua nhánh avatar (không chạm stub media).</summary>
    public static JobPosting Job(Guid? owner = null, string title = "Backend Developer", string language = "vi") => new()
    {
        CreatedByUserId = owner ?? Guid.NewGuid(),
        Title = title,
        JobDescription = "Mô tả công việc",
        DetectedLanguage = language,
        Status = "active",
    };

    public static ARI.Domain.Entities.Application App(
        Guid jobId, Guid? accountId = null, string status = "interview",
        string name = "Nguyen Van A", string email = "cand@example.io", string? cvText = null) => new()
    {
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = email,
        CandidateName = name,
        CvText = cvText,
        Status = status,
    };

    /// <summary>Booking "scheduled" của vòng — điều kiện cấp mã On-site (ADR-015/016).</summary>
    public static InterviewBooking Booking(Guid appId, int round = 1, string status = "scheduled") => new()
    {
        ApplicationId = appId,
        AvailabilitySlotId = Guid.NewGuid(),
        RoundNumber = round,
        Status = status,
    };

    public static ARI.Domain.Entities.InterviewCode Code(
        Guid appId, string code, int round = 1, DateTimeOffset? expiresAt = null, DateTimeOffset? usedAt = null) => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        Code = code,
        ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(2),
        UsedAt = usedAt,
        CreatedByUserId = Guid.NewGuid(),
    };

    public static InterviewRoundConfig RoundConfig(Guid jobId, int round = 1, int ttlHours = 2) => new()
    {
        JobPostingId = jobId,
        RoundNumber = round,
        RoundType = round == 1 ? "screening" : "technical",
        InterviewCodeTtlHours = ttlHours,
    };

    public static InterviewSession CompletedSession(Guid appId, int round) => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        SessionType = "real",
        Status = "completed",
    };

    /// <summary>Dựng <see cref="InterviewCodeService"/> — InterviewService dùng stub ném lỗi
    /// (StartSessionAsync chỉ chạm uow + nuốt lỗi RAG/avatar nên an toàn).</summary>
    public static InterviewCodeService Service(
        InMemoryUnitOfWork uow, RecordingNotificationService notif, FakeTokenService token, InterviewOptions? opts = null)
        => new(uow, InterviewServiceFactory.Create(uow, notif), notif, token, opts ?? new InterviewOptions());
}

/// <summary>ITokenService giả: ghi lại tham số mint token Kiosk + trả token cố định.</summary>
internal sealed class FakeTokenService : ITokenService
{
    public Guid? LastSessionId { get; private set; }
    public Guid? LastApplicationId { get; private set; }
    public int LastTtlHours { get; private set; }
    public int MintCount { get; private set; }
    public string TokenToReturn { get; set; } = "kiosk-jwt-token";

    public string CreateStaffToken(User user) => throw new NotImplementedException();
    public string CreateCandidateToken(CandidateAccount candidate) => throw new NotImplementedException();

    public string CreateKioskSessionToken(Guid sessionId, Guid applicationId, int ttlHours)
    {
        LastSessionId = sessionId;
        LastApplicationId = applicationId;
        LastTtlHours = ttlHours;
        MintCount++;
        return TokenToReturn;
    }
}
