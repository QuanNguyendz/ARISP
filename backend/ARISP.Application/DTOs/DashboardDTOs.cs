using System;
using System.Collections.Generic;

namespace ARISP.Application.DTOs
{
    /// <summary>
    /// Một bước trong phễu tuyển dụng (label + số lượng).
    /// </summary>
    public class FunnelStepDto
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    /// <summary>
    /// Ứng viên gần đây hiển thị trên dashboard HR.
    /// </summary>
    public class RecentCandidateDto
    {
        public Guid Id { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? MatchScore { get; set; }
        public int? LatestRound { get; set; }
        public string? LatestVerdict { get; set; }
    }

    /// <summary>Một mức điểm match CV–JD (nhãn + số hồ sơ).</summary>
    public class MatchBucketDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>Một điểm trên biểu đồ xu hướng ứng tuyển (ngày + số hồ sơ).</summary>
    public class TrendPointDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>Hiệu suất một Recruiter.</summary>
    public class RecruiterStatDto
    {
        public string Name { get; set; } = string.Empty;
        public int Jobs { get; set; }
        public int Applicants { get; set; }
        public int Hired { get; set; }
    }

    /// <summary>Tiến độ lấp đầy chỉ tiêu của một tin.</summary>
    public class VacancyJobDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Vacancies { get; set; }
        public int Hired { get; set; }
    }

    /// <summary>Tin tuyển dụng tóm tắt cho dashboard (danh sách top).</summary>
    public class DashboardJobDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? CreatedByName { get; set; }
        public int ApplicantCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>Tin chờ HR duyệt (zone ưu tiên).</summary>
    public class PendingJobDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
    }

    /// <summary>Khối phân tích tuyển dụng — tính sẵn ở backend để FE chỉ gọi 1 request.</summary>
    public class HrAnalyticsDto
    {
        public List<MatchBucketDto> MatchBuckets { get; set; } = new();
        public int AnalyzedCount { get; set; }
        public int? AvgMatch { get; set; }
        public List<TrendPointDto> Trend { get; set; } = new();
        public List<RecruiterStatDto> Recruiters { get; set; } = new();
        public int TotalVacancies { get; set; }
        public int TotalHired { get; set; }
        public List<VacancyJobDto> JobsWithQuota { get; set; } = new();
    }

    /// <summary>
    /// Tổng quan dashboard cho HR Leader.
    /// </summary>
    public class HrDashboardResponse
    {
        // KPI
        public int ActiveJobs { get; set; }
        public int DraftJobs { get; set; }
        public int TotalApplications { get; set; }
        public int AiInterviews { get; set; }
        public int PendingReviews { get; set; }
        public int Hired { get; set; }

        // Phễu tuyển dụng
        public List<FunnelStepDto> Funnel { get; set; } = new();

        // Ứng viên gần đây
        public List<RecentCandidateDto> RecentCandidates { get; set; } = new();

        // Phân tích (tính sẵn ở backend, thay cho 2 request getAdminJobPostings + getApplications)
        public HrAnalyticsDto Analytics { get; set; } = new();
        public List<DashboardJobDto> TopJobs { get; set; } = new();
        public List<PendingJobDto> PendingJobs { get; set; } = new();
        public int PendingJobsCount { get; set; }
    }
}
