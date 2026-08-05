using System;
using System.Collections.Generic;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>Factory dựng entity cho test Luồng 3 — Job Board công khai + nộp hồ sơ (UC-16/17/18/27/28).</summary>
internal static class JobBoardData
{
    /// <summary>Tin đăng công khai đủ điều kiện hiển thị (active + public, chưa hết hạn) — override để test filter.</summary>
    public static JobPosting PublicJob(
        string title = "Backend Developer",
        string? category = "backend",
        string? employmentType = "full_time",
        string? experienceLevel = "senior",
        string? workMode = "remote",
        string? location = "Hà Nội",
        string? language = "vi",
        List<string>? skills = null,
        decimal? salaryMin = null, decimal? salaryMax = null, string currency = "VND", bool negotiable = false,
        bool isUrgent = false,
        bool isPublic = true, string status = "active",
        DateTimeOffset? deadline = null, DateTimeOffset? publishedAt = null,
        Guid? owner = null) => new()
    {
        CreatedByUserId = owner ?? Guid.NewGuid(),
        Title = title,
        JobDescription = "Mô tả công việc",
        InterviewMode = "remote",
        Status = status,
        IsPublicListing = isPublic,
        JobCategory = category,
        EmploymentType = employmentType,
        ExperienceLevel = experienceLevel,
        WorkMode = workMode,
        Location = location,
        DetectedLanguage = language,
        Skills = skills ?? new List<string> { "C#", "PostgreSQL" },
        SalaryMin = salaryMin,
        SalaryMax = salaryMax,
        SalaryCurrency = currency,
        SalaryIsNegotiable = negotiable,
        IsUrgent = isUrgent,
        ApplicationDeadline = deadline,
        PublishedAt = publishedAt,
    };

    public static User Staff(Guid id, string fullName = "Nguoi Tao Tin", string role = "hr_admin") => new()
    {
        Id = id,
        Email = "staff@example.io",
        FullName = fullName,
        Role = role,
    };

    public static CandidateAccount Candidate(Guid id, string? cvUrl = null, string skillsJson = "[]") => new()
    {
        Id = id,
        Email = "cand@example.io",
        ProfileCvUrl = cvUrl,
        ProfileCvFileName = cvUrl == null ? null : "my-cv.pdf",
        SkillsJson = skillsJson,
    };

    public static CvJdAnalysis Analysis(Guid jobId, string cvHash, string status = "completed", int score = 88) => new()
    {
        JobPostingId = jobId,
        CvHash = cvHash,
        Status = status,
        MatchScore = score,
        Summary = "Phù hợp",
        AiModel = "Gemini",
        ErrorMessage = status == "failed" ? "CV không hợp lệ." : null,
    };
}
