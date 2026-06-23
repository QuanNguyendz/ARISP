using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceScopeFactory _scopeFactory;

        public DashboardController(IUnitOfWork unitOfWork, IServiceScopeFactory scopeFactory)
        {
            _unitOfWork = unitOfWork;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Chạy 1 truy vấn trong DI scope riêng (DbContext + connection riêng) để có thể
        /// đọc song song nhiều bảng — biến tổng thời gian round-trip thành max thay vì sum.
        /// </summary>
        private async Task<T> RunScopedAsync<T>(Func<IUnitOfWork, Task<T>> work)
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await work(uow);
        }

        // ===== Projection nhẹ: chỉ cột cần cho dashboard (tránh nạp text/JSON lớn) =====
        private sealed class JobLite
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Department { get; set; }
            public string Status { get; set; } = string.Empty;
            public Guid CreatedByUserId { get; set; }
            public int? Vacancies { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
        }

        private sealed class AppLite
        {
            public Guid Id { get; set; }
            public Guid JobPostingId { get; set; }
            public string Status { get; set; } = string.Empty;
            public Guid? CvJdAnalysisId { get; set; }
            public string CandidateName { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; }
        }

        private sealed class EvalLite
        {
            public Guid Id { get; set; }
            public Guid ApplicationId { get; set; }
            public int RoundNumber { get; set; }
            public string? AiVerdict { get; set; }
        }

        private sealed class ReviewLite
        {
            public Guid EvaluationId { get; set; }
            public string? FinalVerdict { get; set; }
        }

        /// <summary>
        /// GET /api/dashboard/hr
        /// Tổng quan tuyển dụng cho HR: KPI, phễu tuyển dụng, ứng viên gần đây.
        /// </summary>
        [HttpGet("hr")]
        public async Task<IActionResult> GetHrOverview(CancellationToken ct)
        {
            // Projection ở tầng SQL — chỉ kéo đúng cột cần. Tránh nạp cột text/JSON lớn
            // (Application.CvText, Evaluation.criterion_scores/question_analyses/…) gây timeout đọc stream.
            // 5 bảng độc lập đọc SONG SONG (mỗi cái 1 scope/connection riêng) → latency = max thay vì sum.
            var jobsTask = RunScopedAsync(uow => uow.Repository<JobPosting>()
                .QueryAsync(q => q.Select(j => new JobLite
                {
                    Id = j.Id, Title = j.Title, Department = j.Department, Status = j.Status,
                    CreatedByUserId = j.CreatedByUserId, Vacancies = j.Vacancies, CreatedAt = j.CreatedAt,
                }), ct));
            var appsTask = RunScopedAsync(uow => uow.Repository<ARISP.Domain.Entities.Application>()
                .QueryAsync(q => q.Select(a => new AppLite
                {
                    Id = a.Id, JobPostingId = a.JobPostingId, Status = a.Status,
                    CvJdAnalysisId = a.CvJdAnalysisId, CandidateName = a.CandidateName, CreatedAt = a.CreatedAt,
                }), ct));
            var sessionTask = RunScopedAsync(uow => uow.Repository<InterviewSession>()
                .QueryAsync(q => q.Select(s => s.ApplicationId), ct));
            var evalTask = RunScopedAsync(uow => uow.Repository<Evaluation>()
                .QueryAsync(q => q.Select(e => new EvalLite
                {
                    Id = e.Id, ApplicationId = e.ApplicationId, RoundNumber = e.RoundNumber, AiVerdict = e.AiVerdict,
                }), ct));
            var reviewTask = RunScopedAsync(uow => uow.Repository<HrReview>()
                .QueryAsync(q => q.Select(r => new ReviewLite { EvaluationId = r.EvaluationId, FinalVerdict = r.FinalVerdict }), ct));

            await Task.WhenAll(jobsTask, appsTask, sessionTask, evalTask, reviewTask);
            var jobs = jobsTask.Result;
            var apps = appsTask.Result;
            var sessionAppIds = sessionTask.Result;
            var evaluations = evalTask.Result;
            var reviews = reviewTask.Result;

            var reviewedEvalIds = reviews.Select(r => r.EvaluationId).ToHashSet();
            var pendingReviews = evaluations.Count(e => !reviewedEvalIds.Contains(e.Id));
            var hired = reviews.Count(r => string.Equals(r.FinalVerdict, "pass", StringComparison.OrdinalIgnoreCase));

            var appsWithSession = sessionAppIds.ToHashSet();
            var appsWithEval = evaluations.Select(e => e.ApplicationId).ToHashSet();

            // KPI + phễu
            var response = new HrDashboardResponse
            {
                ActiveJobs = jobs.Count(j => j.Status == "active"),
                DraftJobs = jobs.Count(j => j.Status == "draft"),
                TotalApplications = apps.Count,
                AiInterviews = sessionAppIds.Count,
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

            // Điểm match CV–JD + tên người tạo tin: 2 truy vấn phụ thuộc, chạy SONG SONG với nhau.
            var analysisIds = apps.Where(a => a.CvJdAnalysisId.HasValue).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var creatorIds = jobs.Select(j => j.CreatedByUserId).Distinct().ToList();

            var scoreTask = analysisIds.Count == 0
                ? Task.FromResult(new Dictionary<Guid, int>())
                : RunScopedAsync(async uow => (await uow.Repository<CvJdAnalysis>()
                        .QueryAsync(q => q.Where(c => analysisIds.Contains(c.Id)).Select(c => new { c.Id, c.MatchScore }), ct))
                    .ToDictionary(c => c.Id, c => c.MatchScore));
            var creatorTask = RunScopedAsync(async uow => (await uow.Repository<User>()
                    .QueryAsync(q => q.Where(u => creatorIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email }), ct))
                .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName));

            await Task.WhenAll(scoreTask, creatorTask);
            var scoreByAnalysisId = scoreTask.Result;
            var creatorNameById = creatorTask.Result;

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

            // ===== Phân tích (tính sẵn để FE chỉ gọi 1 request) =====
            string? CreatorName(Guid id) => creatorNameById.TryGetValue(id, out var n) ? n : null;

            int? ScoreOf(AppLite a) =>
                a.CvJdAnalysisId.HasValue && scoreByAnalysisId.TryGetValue(a.CvJdAnalysisId.Value, out var s) ? s : (int?)null;

            var applicantsByJob = apps.GroupBy(a => a.JobPostingId).ToDictionary(g => g.Key, g => g.Count());
            var passByJob = apps
                .Where(a => string.Equals(a.Status, "pass", StringComparison.OrdinalIgnoreCase))
                .GroupBy(a => a.JobPostingId)
                .ToDictionary(g => g.Key, g => g.Count());
            int PassOf(Guid jobId) => passByJob.TryGetValue(jobId, out var c) ? c : 0;
            int ApplicantsOf(Guid jobId) => applicantsByJob.TryGetValue(jobId, out var c) ? c : 0;

            // Phân bố điểm match CV–JD
            var scores = apps.Select(ScoreOf).Where(s => s.HasValue).Select(s => s!.Value).ToList();
            var ranges = new (string Label, int Min, int Max)[]
            {
                ("85–100", 85, 101), ("75–84", 75, 85), ("65–74", 65, 75), ("50–64", 50, 65), ("Dưới 50", 0, 50),
            };
            var analytics = new HrAnalyticsDto
            {
                AnalyzedCount = scores.Count,
                AvgMatch = scores.Count > 0 ? (int)Math.Round(scores.Average()) : (int?)null,
                MatchBuckets = ranges.Select(r => new MatchBucketDto
                {
                    Label = r.Label,
                    Count = scores.Count(s => s >= r.Min && s < r.Max),
                }).ToList(),
            };

            // Xu hướng ứng tuyển 14 ngày (theo ngày UTC)
            var today = DateTimeOffset.UtcNow.Date;
            var countsByDay = apps
                .GroupBy(a => a.CreatedAt.UtcDateTime.Date)
                .ToDictionary(g => g.Key, g => g.Count());
            for (var i = 13; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                analytics.Trend.Add(new TrendPointDto
                {
                    Label = $"{d.Day}/{d.Month}",
                    Count = countsByDay.TryGetValue(d, out var c) ? c : 0,
                });
            }

            // Hiệu suất Recruiter
            analytics.Recruiters = jobs
                .GroupBy(j => j.CreatedByUserId)
                .Select(g => new RecruiterStatDto
                {
                    Name = CreatorName(g.Key) ?? "—",
                    Jobs = g.Count(),
                    Applicants = g.Sum(j => ApplicantsOf(j.Id)),
                    Hired = g.Sum(j => PassOf(j.Id)),
                })
                .OrderByDescending(r => r.Applicants)
                .Take(6)
                .ToList();

            // Lấp đầy chỉ tiêu — CHỈ tính trên các tin CÓ đặt chỉ tiêu, để tử số (đã tuyển) khớp
            // mẫu số (tổng chỉ tiêu) và khớp danh sách từng tin bên dưới. Tránh đếm cả ứng viên pass
            // ở các tin không đặt chỉ tiêu (gây lệch kiểu "2/5" trong khi tin duy nhất là "0/5").
            var quotaJobs = jobs.Where(j => j.Vacancies > 0).ToList();
            analytics.TotalVacancies = quotaJobs.Sum(j => j.Vacancies!.Value);
            analytics.TotalHired = quotaJobs.Sum(j => PassOf(j.Id));
            analytics.JobsWithQuota = quotaJobs
                .Select(j => new VacancyJobDto { Id = j.Id, Title = j.Title, Vacancies = j.Vacancies!.Value, Hired = PassOf(j.Id) })
                .OrderByDescending(v => (double)v.Hired / v.Vacancies)
                .Take(5)
                .ToList();

            response.Analytics = analytics;

            // Top tin theo số ứng viên + tin chờ duyệt (cho zone ưu tiên & widget Tin tuyển dụng)
            response.TopJobs = jobs
                .OrderByDescending(j => ApplicantsOf(j.Id))
                .Take(6)
                .Select(j => new DashboardJobDto
                {
                    Id = j.Id,
                    Title = j.Title,
                    Department = j.Department,
                    CreatedByName = CreatorName(j.CreatedByUserId),
                    ApplicantCount = ApplicantsOf(j.Id),
                    Status = j.Status,
                })
                .ToList();

            var pendingJobsAll = jobs.Where(j => j.Status == "pending").OrderByDescending(j => j.CreatedAt).ToList();
            response.PendingJobsCount = pendingJobsAll.Count;
            response.PendingJobs = pendingJobsAll
                .Take(5)
                .Select(j => new PendingJobDto { Id = j.Id, Title = j.Title, CreatedByName = CreatorName(j.CreatedByUserId) })
                .ToList();

            return Ok(response);
        }
    }
}
