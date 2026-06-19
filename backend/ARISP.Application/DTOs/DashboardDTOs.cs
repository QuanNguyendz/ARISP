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
    }
}
