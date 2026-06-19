using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(Policy = "InternalStaff")]
    public class DashboardController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public DashboardController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// GET /api/dashboard/hr
        /// Tổng quan tuyển dụng cho HR: KPI, phễu tuyển dụng, ứng viên gần đây.
        /// </summary>
        [HttpGet("hr")]
        public async Task<IActionResult> GetHrOverview(CancellationToken ct)
        {
            var jobs = (await _unitOfWork.Repository<JobPosting>().GetAllAsync(ct)).ToList();
            var apps = (await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetAllAsync(ct)).ToList();
            var sessions = (await _unitOfWork.Repository<InterviewSession>().GetAllAsync(ct)).ToList();
            var evaluations = (await _unitOfWork.Repository<Evaluation>().GetAllAsync(ct)).ToList();
            var reviews = (await _unitOfWork.Repository<HrReview>().GetAllAsync(ct)).ToList();

            var reviewedEvalIds = reviews.Select(r => r.EvaluationId).ToHashSet();
            var pendingReviews = evaluations.Count(e => !reviewedEvalIds.Contains(e.Id));
            var hired = reviews.Count(r => string.Equals(r.FinalVerdict, "pass", StringComparison.OrdinalIgnoreCase));

            var appsWithSession = sessions.Select(s => s.ApplicationId).ToHashSet();
            var appsWithEval = evaluations.Select(e => e.ApplicationId).ToHashSet();

            // KPI + phễu
            var response = new HrDashboardResponse
            {
                ActiveJobs = jobs.Count(j => j.Status == "active"),
                DraftJobs = jobs.Count(j => j.Status == "draft"),
                TotalApplications = apps.Count,
                AiInterviews = sessions.Count,
                PendingReviews = pendingReviews,
                Hired = hired,
                Funnel = new List<FunnelStepDto>
                {
                    new() { Label = "Ứng tuyển", Value = apps.Count },
                    new() { Label = "Sàng lọc CV–JD", Value = apps.Count(a => a.CvJdAnalysisId.HasValue) },
                    new() { Label = "Phỏng vấn AI", Value = appsWithSession.Count },
                    new() { Label = "Đề xuất (HR)", Value = appsWithEval.Count },
                    new() { Label = "Tuyển", Value = hired },
                },
            };

            // Điểm match CV–JD theo batch
            var analysisIds = apps.Where(a => a.CvJdAnalysisId.HasValue).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var scoreByAnalysisId = new Dictionary<Guid, int>();
            if (analysisIds.Count > 0)
            {
                var analyses = await _unitOfWork.Repository<CvJdAnalysis>().FindAsync(c => analysisIds.Contains(c.Id));
                scoreByAnalysisId = analyses.ToDictionary(c => c.Id, c => c.MatchScore);
            }

            var jobTitleById = jobs.ToDictionary(j => j.Id, j => j.Title);

            // Đánh giá mới nhất theo từng hồ sơ (vòng cao nhất)
            var latestEvalByApp = evaluations
                .GroupBy(e => e.ApplicationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.RoundNumber).First());

            response.RecentCandidates = apps
                .OrderByDescending(a => a.CreatedAt)
                .Take(6)
                .Select(a =>
                {
                    latestEvalByApp.TryGetValue(a.Id, out var eval);
                    return new RecentCandidateDto
                    {
                        Id = a.Id,
                        CandidateName = a.CandidateName,
                        JobTitle = jobTitleById.TryGetValue(a.JobPostingId, out var t) ? t : null,
                        Status = a.Status,
                        MatchScore = a.CvJdAnalysisId.HasValue && scoreByAnalysisId.TryGetValue(a.CvJdAnalysisId.Value, out var ms)
                            ? ms
                            : (int?)null,
                        LatestRound = eval?.RoundNumber,
                        LatestVerdict = eval?.AiVerdict,
                    };
                })
                .ToList();

            return Ok(response);
        }
    }
}
