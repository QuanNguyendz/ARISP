using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.Application.Dashboard.Queries.GetHrDashboard
{
    /// <summary>
    /// Tổng quan tuyển dụng: KPI, phễu tuyển dụng, ứng viên gần đây, analytics.
    ///
    /// Phạm vi theo vai trò (<see cref="JobAccess.ScopedJobIdsAsync"/>). Trước đây màn này gom
    /// TOÀN BỘ tin và hồ sơ của công ty cho mọi <c>InternalStaff</c> — kèm tên ứng viên gần đây,
    /// bảng hiệu suất từng recruiter và danh sách tin chờ duyệt của người khác.
    /// </summary>
    public record GetHrDashboardQuery(Guid? UserId, string? Role) : IRequest<Result<HrDashboardResponse>>;

    public class GetHrDashboardQueryHandler : IRequestHandler<GetHrDashboardQuery, Result<HrDashboardResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetHrDashboardQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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
            public DateTimeOffset? ApplicationDeadline { get; set; }
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

        public async Task<Result<HrDashboardResponse>> Handle(GetHrDashboardQuery request, CancellationToken ct)
        {
            // Lọc phạm vi ngay tại HAI nguồn (tin + hồ sơ): mọi con số phía dưới đều dẫn xuất từ
            // hai tập này, nên lọc ở đây là toàn bộ dashboard tự khớp phạm vi — không phải rải
            // điều kiện vào từng phép đếm và bỏ sót một cái.
            var scope = await JobAccess.ScopedJobIdsAsync(_unitOfWork, request.UserId, request.Role, ct);

            // Projection ở tầng SQL — chỉ kéo đúng cột cần.
            var jobs = await _unitOfWork.Repository<JobPosting>()
                .QueryAsync(q => (scope == null ? q : q.Where(j => scope.Contains(j.Id))).Select(j => new JobLite
                {
                    Id = j.Id, Title = j.Title, Department = j.Department, Status = j.Status,
                    CreatedByUserId = j.CreatedByUserId, Vacancies = j.Vacancies, CreatedAt = j.CreatedAt, ApplicationDeadline = j.ApplicationDeadline,
                }), ct);

            var apps = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => (scope == null ? q : q.Where(a => scope.Contains(a.JobPostingId))).Select(a => new AppLite
                {
                    Id = a.Id, JobPostingId = a.JobPostingId, Status = a.Status,
                    CvJdAnalysisId = a.CvJdAnalysisId, CandidateName = a.CandidateName, CreatedAt = a.CreatedAt,
                }), ct);

            // Phiên/đánh giá/duyệt bám theo hồ sơ đã lọc ở trên.
            var visibleAppIds = apps.Select(a => a.Id).ToHashSet();

            var sessionAppIds = (await _unitOfWork.Repository<InterviewSession>()
                .QueryAsync(q => q.Select(s => s.ApplicationId), ct))
                .Where(id => scope == null || visibleAppIds.Contains(id)).ToList();

            var evaluations = (await _unitOfWork.Repository<Evaluation>()
                .QueryAsync(q => q.Select(e => new EvalLite
                {
                    Id = e.Id, ApplicationId = e.ApplicationId, RoundNumber = e.RoundNumber, AiVerdict = e.AiVerdict,
                }), ct))
                .Where(e => scope == null || visibleAppIds.Contains(e.ApplicationId)).ToList();

            var visibleEvalIds = evaluations.Select(e => e.Id).ToHashSet();
            var reviews = (await _unitOfWork.Repository<HrReview>()
                .QueryAsync(q => q.Select(r => new ReviewLite { EvaluationId = r.EvaluationId, FinalVerdict = r.FinalVerdict }), ct))
                .Where(r => scope == null || visibleEvalIds.Contains(r.EvaluationId)).ToList();

            var reviewedEvalIds = reviews.Select(r => r.EvaluationId).ToHashSet();
            var pendingReviews = evaluations.Count(e => !reviewedEvalIds.Contains(e.Id));
            var hired = reviews.Count(r => string.Equals(r.FinalVerdict, "pass", StringComparison.OrdinalIgnoreCase));

            var appsWithSession = sessionAppIds.ToHashSet();
            var appsWithEval = evaluations.Select(e => e.ApplicationId).ToHashSet();

            // KPI + phễu
            var response = new HrDashboardResponse
            {
                ActiveJobs = jobs.Count(j => j.Status == "active" && (!j.ApplicationDeadline.HasValue || j.ApplicationDeadline.Value > DateTimeOffset.UtcNow)),
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

            // Điểm match CV–JD + tên người tạo tin
            var analysisIds = apps.Where(a => a.CvJdAnalysisId.HasValue).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var creatorIds = jobs.Select(j => j.CreatedByUserId).Distinct().ToList();

            var scoreByAnalysisId = analysisIds.Count == 0
                ? new Dictionary<Guid, int>()
                : (await _unitOfWork.Repository<CvJdAnalysis>()
                        .QueryAsync(q => q.Where(c => analysisIds.Contains(c.Id)).Select(c => new { c.Id, c.MatchScore }), ct))
                    .ToDictionary(c => c.Id, c => c.MatchScore);

            var creatorNameById = creatorIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<User>()
                        .QueryAsync(q => q.Where(u => creatorIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email }), ct))
                    .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName);

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
            // Đếm CẢ `pass` LẪN `hired`: ứng viên nhận việc rời khỏi `pass` sang `hired` (ADR-061),
            // nên chỉ đếm `pass` sẽ làm chỉ số của một tin tuyển thành công **tụt về 0** đúng lúc nó
            // thành công. Màn "Phân công & tải tuyển dụng" đã sửa cùng lỗi này, chỗ đó sót lại.
            var passByJob = apps
                .Where(a => ApplicationStatuses.Is(a.Status, ApplicationStatuses.Pass)
                            || ApplicationStatuses.Is(a.Status, ApplicationStatuses.Hired))
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
                    Status = j.Status == "active" && j.ApplicationDeadline.HasValue && j.ApplicationDeadline.Value <= DateTimeOffset.UtcNow ? "closed" : j.Status,
                    ApplicationDeadline = j.ApplicationDeadline,
                })
                .ToList();

            var pendingJobsAll = jobs.Where(j => j.Status == "pending").OrderByDescending(j => j.CreatedAt).ToList();
            response.PendingJobsCount = pendingJobsAll.Count;
            response.PendingJobs = pendingJobsAll
                .Take(5)
                .Select(j => new PendingJobDto { Id = j.Id, Title = j.Title, CreatedByName = CreatorName(j.CreatedByUserId) })
                .ToList();

            return Result.Success(response);
        }
    }
}
