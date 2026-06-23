using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ARISP.Domain.Entities;

namespace ARISP.Application.DTOs
{
    /// <summary>
    /// DTO đại diện cho yêu cầu tạo một Job Posting (tin tuyển dụng) mới, kèm theo cấu hình các vòng phỏng vấn.
    /// </summary>
    public class CreateJobPostingRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        // File JD gốc (PDF/DOCX) đã upload qua /jobs/analyze-jd – lưu storageKey + metadata vào job.
        public string? JdFileUrl { get; set; }
        public string? JdFileName { get; set; }
        public string? JdFileFormat { get; set; } // pdf | docx
        public string InterviewMode { get; set; } = "remote"; // remote | onsite | both
        public bool IsPublicListing { get; set; } = false;
        public string? LanguageRequirement { get; set; }
        public int? RescheduleDeadlineHours { get; set; } = 24;
        public int InviteTokenTtlHours { get; set; } = 48;
        public List<RoundConfigDto> RoundConfigs { get; set; } = new();
        public JsonElement? ScoringRubric { get; set; } // JSON
        public string? PersonaName { get; set; }
        public string? PersonaVoiceId { get; set; }
        public string? PersonaStyle { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; }
        public bool SalaryIsNegotiable { get; set; } = false;
        public string? EmploymentType { get; set; }
        public string? ExperienceLevel { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? JobCategory { get; set; }
        public DateTimeOffset? ApplicationDeadline { get; set; }
        public bool IsUrgent { get; set; } = false;
        /// <summary>Số lượng cần tuyển (chỉ tiêu). Null/0 = không giới hạn.</summary>
        public int? Vacancies { get; set; }
    }

    public class UpdateJobStatusRequest
    {
        [Required(ErrorMessage = "Trạng thái (status) là bắt buộc.")]
        public string Status { get; set; } = string.Empty;

        public string? RejectionReason { get; set; } // Chỉ bắt buộc khi đổi status sang 'rejected'
    }

    /// <summary>
    /// DTO cấu hình thông tin chi tiết cho từng vòng phỏng vấn (Interview Round) thuộc Job Posting.
    /// </summary>
    public class RoundConfigDto
    {
        public int RoundNumber { get; set; }
        public string RoundType { get; set; } = "screening";
        public string? InterviewLanguage { get; set; }
        public int InterviewCodeTtlHours { get; set; } = 2;
        public int MaxDurationMinutes { get; set; } = 45;

        public static RoundConfigDto FromEntity(InterviewRoundConfig config) =>
            new()
            {
                RoundNumber = config.RoundNumber,
                RoundType = config.RoundType,
                InterviewLanguage = config.InterviewLanguage,
                InterviewCodeTtlHours = config.InterviewCodeTtlHours,
                MaxDurationMinutes = config.MaxDurationMinutes
            };
    }

    /// <summary>
    /// DTO chi tiết thông tin Job Posting trả về cho client sau khi tạo hoặc lấy chi tiết.
    /// </summary>
    public class JobPostingResponse
    {
        public Guid Id { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string? JdFileUrl { get; set; }
        public string? JdFileName { get; set; }
        public string? JdFileFormat { get; set; }
        public string InterviewMode { get; set; } = "remote";
        public string Status { get; set; } = "draft";
        public string? RejectionReason { get; set; }
        public bool IsPublicListing { get; set; }
        public string? DetectedLanguage { get; set; }
        public string? LanguageRequirement { get; set; }
        public bool LanguageConfirmed { get; set; }
        public List<RoundConfigDto> RoundConfigs { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; }
        public bool SalaryIsNegotiable { get; set; }
        public string? EmploymentType { get; set; }
        public string? ExperienceLevel { get; set; }
        public List<string> Skills { get; set; } = new();
        public string? JobCategory { get; set; }
        public DateTimeOffset? ApplicationDeadline { get; set; }
        public bool IsUrgent { get; set; }
        public int? Vacancies { get; set; }
        public JsonElement? ScoringRubric { get; set; }

        // ===== Phê duyệt của HR Leader =====
        public Guid? ApprovedByUserId { get; set; }
        public DateTimeOffset? ApprovedAt { get; set; }
        public string? ApproverName { get; set; }
        /// <summary>URL file JD đã đóng dấu duyệt (đã resolve cho staff). Null nếu chưa duyệt/không phải PDF.</summary>
        public string? SignedJdFileUrl { get; set; }

        public static JobPostingResponse FromEntity(JobPosting job, List<RoundConfigDto> roundConfigs) =>
            new()
            {
                Id = job.Id,
                CreatedByUserId = job.CreatedByUserId,
                Title = job.Title,
                Department = job.Department,
                JobDescription = job.JobDescription,
                JdFileUrl = job.JdFileUrl,
                JdFileName = job.JdFileName,
                JdFileFormat = job.JdFileFormat,
                InterviewMode = job.InterviewMode,
                Status = job.Status,
                RejectionReason = job.RejectionReason,
                IsPublicListing = job.IsPublicListing,
                DetectedLanguage = job.DetectedLanguage,
                LanguageRequirement = job.LanguageRequirement,
                LanguageConfirmed = job.LanguageConfirmed,
                RoundConfigs = roundConfigs,
                CreatedAt = job.CreatedAt,
                Location = job.Location,
                WorkMode = job.WorkMode,
                SalaryMin = job.SalaryMin,
                SalaryMax = job.SalaryMax,
                SalaryCurrency = job.SalaryCurrency,
                SalaryIsNegotiable = job.SalaryIsNegotiable ?? false,
                EmploymentType = job.EmploymentType,
                ExperienceLevel = job.ExperienceLevel,
                Skills = job.Skills ?? new List<string>(),
                JobCategory = job.JobCategory,
                ApplicationDeadline = job.ApplicationDeadline,
                IsUrgent = job.IsUrgent ?? false,
                Vacancies = job.Vacancies,
                ScoringRubric = !string.IsNullOrEmpty(job.ScoringRubric) ? JsonSerializer.Deserialize<JsonElement>(job.ScoringRubric, (JsonSerializerOptions?)null) : null,
                ApprovedByUserId = job.ApprovedByUserId,
                ApprovedAt = job.ApprovedAt,
                ApproverName = job.ApproverName,
                SignedJdFileUrl = job.SignedJdFileUrl
            };
    }

    /// <summary>
    /// DTO thông tin rút gọn của Job Posting để hiển thị danh sách trên Job Board công khai.
    /// </summary>
    public class JobPostingListItemResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string InterviewMode { get; set; } = "remote";
        public string Status { get; set; } = "draft";
        public string? DetectedLanguage { get; set; }
        public string? LanguageRequirement { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public string? EmploymentType { get; set; }
        public string? ExperienceLevel { get; set; }
        public string? JobCategory { get; set; }
        public bool IsUrgent { get; set; }
        public int? Vacancies { get; set; }
        public List<string> Skills { get; set; } = new();

        // Lương (dùng cho màn duyệt tin & danh sách)
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string? SalaryCurrency { get; set; }
        public bool SalaryIsNegotiable { get; set; }

        // Người tạo tin (Recruiter) – phục vụ màn HR duyệt tin
        public Guid CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public string? RejectionReason { get; set; }

        /// <summary>Số lượng ứng viên đã ứng tuyển vào tin này (dùng cho dashboard HR).</summary>
        public int ApplicantCount { get; set; }

        public static JobPostingListItemResponse FromEntity(JobPosting job) =>
            new()
            {
                Id = job.Id,
                Title = job.Title,
                Department = job.Department,
                InterviewMode = job.InterviewMode,
                Status = job.Status,
                DetectedLanguage = job.DetectedLanguage,
                LanguageRequirement = job.LanguageRequirement,
                CreatedAt = job.CreatedAt,
                PublishedAt = job.PublishedAt,
                Location = job.Location,
                WorkMode = job.WorkMode,
                EmploymentType = job.EmploymentType,
                ExperienceLevel = job.ExperienceLevel,
                JobCategory = job.JobCategory,
                IsUrgent = job.IsUrgent ?? false,
                Vacancies = job.Vacancies,
                Skills = job.Skills ?? new List<string>(),
                SalaryMin = job.SalaryMin,
                SalaryMax = job.SalaryMax,
                SalaryCurrency = job.SalaryCurrency,
                SalaryIsNegotiable = job.SalaryIsNegotiable ?? false,
                CreatedByUserId = job.CreatedByUserId,
                RejectionReason = job.RejectionReason
            };
    }

    /// <summary>Một mục trong bộ lọc (facet): giá trị thô, nhãn hiển thị và số lượng job khớp.</summary>
    public class JobFacetItem
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Tập hợp các bộ lọc khả dụng cho Job Board – chỉ chứa những giá trị THỰC SỰ có
    /// trong danh sách tin đang active &amp; public, kèm số lượng để hiển thị "Junior (2)".
    /// </summary>
    public class JobFacetsResponse
    {
        public List<JobFacetItem> Categories { get; set; } = new();
        public List<JobFacetItem> EmploymentTypes { get; set; } = new();
        public List<JobFacetItem> ExperienceLevels { get; set; } = new();
        public List<JobFacetItem> WorkModes { get; set; } = new();
        public List<JobFacetItem> Locations { get; set; } = new();
        public List<JobFacetItem> Skills { get; set; } = new();
        public List<JobFacetItem> Languages { get; set; } = new();
        public int TotalJobs { get; set; }
    }

    /// <summary>
    /// DTO yêu cầu thiết lập khung giờ rảnh (Availability Slots) cho vòng phỏng vấn của Job Posting.
    /// </summary>
    public class CreateAvailabilitySlotRequest
    {
        public int RoundNumber { get; set; } = 1;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public int Capacity { get; set; } = 1;
    }
}
