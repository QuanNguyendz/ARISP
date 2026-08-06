using System;
using System.Collections.Generic;
using ARI.Application.DTOs;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>Factory dựng request/entity cho test luồng Configure Job Posting (Luồng 1: UC-45/46/47/50).</summary>
internal static class JobPostingData
{
    public static RoundConfigDto Round(
        int number = 1, string type = "screening", string? language = null, int codeTtl = 2, int maxMinutes = 45) => new()
    {
        RoundNumber = number,
        RoundType = type,
        InterviewLanguage = language,
        InterviewCodeTtlHours = codeTtl,
        MaxDurationMinutes = maxMinutes,
    };

    /// <summary>Request hợp lệ mặc định (remote, 1 vòng). Truyền rounds để cấu hình multi-round.</summary>
    public static CreateJobPostingRequest Request(
        string title = "Backend Developer",
        string description = "Chúng tôi cần một kỹ sư backend giàu kinh nghiệm với C# và .NET.",
        string interviewMode = "remote",
        params RoundConfigDto[] rounds) => new()
    {
        Title = title,
        JobDescription = description,
        InterviewMode = interviewMode,
        InviteTokenTtlHours = 48,
        RoundConfigs = rounds.Length > 0 ? new List<RoundConfigDto>(rounds) : new List<RoundConfigDto> { Round() },
    };

    public static User Staff(Guid id, string role = "recruiter") => new()
    {
        Id = id,
        Email = "staff@example.io",
        Role = role,
        FullName = "Nguoi Tao Tin",
    };

    public static JobPosting Job(
        Guid owner, string status = "draft",
        string? jdFileUrl = null, string? jdFileFormat = null, DateTimeOffset? deadline = null) => new()
    {
        CreatedByUserId = owner,
        Title = "Backend Developer",
        JobDescription = "JD cũ",
        InterviewMode = "remote",
        Status = status,
        InviteTokenTtlHours = 48,
        JdFileUrl = jdFileUrl,
        JdFileName = jdFileUrl == null ? null : "jd." + (jdFileFormat ?? "pdf"),
        JdFileFormat = jdFileFormat,
        ApplicationDeadline = deadline,
    };

    /// <summary>Request đổi trạng thái (approval workflow) — kèm lý do khi từ chối.</summary>
    public static UpdateJobStatusRequest StatusRequest(string status, string? reason = null) => new()
    {
        Status = status,
        RejectionReason = reason,
    };

    public static InterviewRoundConfig RoundEntity(
        Guid jobId, int number = 1, string type = "screening", string? language = "vi", int codeTtl = 2, int maxMinutes = 45) => new()
    {
        JobPostingId = jobId,
        RoundNumber = number,
        RoundType = type,
        InterviewLanguage = language,
        InterviewCodeTtlHours = codeTtl,
        MaxDurationMinutes = maxMinutes,
    };

    public static CreateAvailabilitySlotRequest Slot(
        int round = 1, int capacity = 2, DateTimeOffset? start = null) => new()
    {
        RoundNumber = round,
        StartTime = start ?? DateTimeOffset.UtcNow.AddDays(3),
        EndTime = (start ?? DateTimeOffset.UtcNow.AddDays(3)).AddHours(1),
        Timezone = "Asia/Ho_Chi_Minh",
        Capacity = capacity,
    };
}
