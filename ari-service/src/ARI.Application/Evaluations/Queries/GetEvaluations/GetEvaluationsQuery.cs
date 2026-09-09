using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Evaluations.Queries.GetEvaluations
{
    /// <summary>
    /// Danh sách báo cáo đánh giá AI. <paramref name="UserId"/>/<paramref name="Role"/> KHÔNG phải
    /// tham số phụ: trước đây query này không nhận danh tính nào cả, nên bất kỳ ai qua được policy
    /// <c>InternalStaff</c> đều phân trang được TOÀN BỘ báo cáo phỏng vấn của công ty — kèm tên,
    /// email ứng viên và điểm số. Đây là lỗ rò dữ liệu lớn nhất trước ADR-061.
    /// </summary>
    public record GetEvaluationsQuery(
        Guid? JobPostingId,
        string? Status,
        int Page,
        int PageSize,
        Guid? UserId,
        string? Role) : IRequest<Result<PaginatedResponse<EvaluationListItemResponse>>>;

    public class GetEvaluationsQueryHandler
        : IRequestHandler<GetEvaluationsQuery, Result<PaginatedResponse<EvaluationListItemResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetEvaluationsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ===== Projection nhẹ cho danh sách đánh giá: KHÔNG kéo cột JSON lớn của Evaluation
        // (criterion_scores/question_analyses/reasoning/cheat_signals/language_assessment). =====
        private sealed class EvalLite
        {
            public Guid Id { get; set; }
            public Guid SessionId { get; set; }
            public Guid ApplicationId { get; set; }
            public int RoundNumber { get; set; }
            public string SessionType { get; set; } = string.Empty;
            public string AiVerdict { get; set; } = string.Empty;
            public decimal? OverallScore { get; set; }
            public decimal? CheatScore { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
        }
        private sealed class ReviewLite
        {
            public Guid EvaluationId { get; set; }
            public string FinalVerdict { get; set; } = string.Empty;
        }
        private sealed class EvalAppLite
        {
            public Guid Id { get; set; }
            public Guid JobPostingId { get; set; }
            public string CandidateName { get; set; } = string.Empty;
            public string CandidateEmail { get; set; } = string.Empty;
        }

        private static PaginatedResponse<EvaluationListItemResponse> EmptyPage(int page, int pageSize, int total = 0) => new()
        {
            Items = new List<EvaluationListItemResponse>(),
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
        };

        public async Task<Result<PaginatedResponse<EvaluationListItemResponse>>> Handle(
            GetEvaluationsQuery request, CancellationToken ct)
        {
            var (jobPostingId, status, page, pageSize) = (request.JobPostingId, request.Status, request.Page, request.PageSize);

            // Phạm vi do server tính: null = quản trị viên (không giới hạn), tập rỗng = không thấy gì.
            var scope = await JobAccess.ScopedJobIdsAsync(_unitOfWork, request.UserId, request.Role, ct);
            if (scope != null && jobPostingId.HasValue && !scope.Contains(jobPostingId.Value))
                return Result.Success(EmptyPage(page, pageSize));

            // Đánh giá buổi thử là riêng tư của ứng viên — không vào danh sách của nhân sự nội bộ (ADR-051).
            List<EvalLite> evaluations = await _unitOfWork.Repository<Evaluation>()
                .QueryAsync(q => q.Where(e => e.SessionType != "practice").Select(e => new EvalLite
                {
                    Id = e.Id, SessionId = e.SessionId, ApplicationId = e.ApplicationId, RoundNumber = e.RoundNumber,
                    SessionType = e.SessionType, AiVerdict = e.AiVerdict, OverallScore = e.OverallScore,
                    CheatScore = e.CheatScore, CreatedAt = e.CreatedAt,
                }), ct);

            // Lọc theo phạm vi TRƯỚC khi phân trang: lọc sau sẽ ra những trang trống lỗ chỗ và
            // tổng số đếm được vẫn là tổng của cả công ty.
            if (scope != null)
            {
                var visibleAppIds = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                        .QueryAsync(q => q.Where(a => scope.Contains(a.JobPostingId)).Select(a => a.Id), ct))
                    .ToHashSet();
                evaluations = evaluations.Where(e => visibleAppIds.Contains(e.ApplicationId)).ToList();
            }
            var hrReviews = await _unitOfWork.Repository<HrReview>()
                .QueryAsync(q => q.Select(r => new ReviewLite { EvaluationId = r.EvaluationId, FinalVerdict = r.FinalVerdict }), ct);
            var reviewedEvalIds = hrReviews.Select(r => r.EvaluationId).ToHashSet();

            // Filter by JobPostingId if specified
            if (jobPostingId.HasValue)
            {
                var appIds = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                        .QueryAsync(q => q.Where(a => a.JobPostingId == jobPostingId.Value).Select(a => a.Id), ct))
                    .ToHashSet();
                evaluations = evaluations.Where(e => appIds.Contains(e.ApplicationId)).ToList();
            }

            // Filter by Status if specified
            if (!string.IsNullOrEmpty(status))
            {
                var statusLower = status.ToLower();
                if (statusLower == "completed")
                {
                    evaluations = evaluations.Where(e => reviewedEvalIds.Contains(e.Id)).ToList();
                }
                else if (statusLower == "pending")
                {
                    evaluations = evaluations.Where(e => !reviewedEvalIds.Contains(e.Id)).ToList();
                }
                else if (statusLower == "pass" || statusLower == "not_pass")
                {
                    evaluations = evaluations.Where(e =>
                        (reviewedEvalIds.Contains(e.Id) && hrReviews.First(r => r.EvaluationId == e.Id).FinalVerdict.ToLower() == statusLower) ||
                        (!reviewedEvalIds.Contains(e.Id) && e.AiVerdict.ToLower() == statusLower)
                    ).ToList();
                }
            }

            var totalItems = evaluations.Count();
            var paginatedEvals = evaluations
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            if (!paginatedEvals.Any())
            {
                return Result.Success(EmptyPage(page, pageSize, totalItems));
            }

            var appIdsInEvals = paginatedEvals.Select(e => e.ApplicationId).Distinct().ToList();
            var appDict = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .QueryAsync(q => q.Where(a => appIdsInEvals.Contains(a.Id)).Select(a => new EvalAppLite
                    {
                        Id = a.Id, JobPostingId = a.JobPostingId, CandidateName = a.CandidateName, CandidateEmail = a.CandidateEmail,
                    }), ct))
                .ToDictionary(a => a.Id);

            var jobIds = appDict.Values.Select(a => a.JobPostingId).Distinct().ToList();
            var jobTitleById = (await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id)).Select(j => new { j.Id, j.Title }), ct))
                .ToDictionary(j => j.Id, j => j.Title);

            var hrDict = hrReviews.ToDictionary(r => r.EvaluationId);

            var responseList = paginatedEvals.Select(e =>
            {
                appDict.TryGetValue(e.ApplicationId, out var app);
                string? jobTitle = null;
                if (app != null) jobTitleById.TryGetValue(app.JobPostingId, out jobTitle);
                hrDict.TryGetValue(e.Id, out var hr);

                return new EvaluationListItemResponse
                {
                    Id = e.Id,
                    SessionId = e.SessionId,
                    ApplicationId = e.ApplicationId,
                    RoundNumber = e.RoundNumber,
                    SessionType = e.SessionType,
                    AiVerdict = e.AiVerdict,
                    OverallScore = e.OverallScore,
                    CheatScore = e.CheatScore,
                    CreatedAt = e.CreatedAt,
                    CandidateName = app?.CandidateName ?? "Unknown",
                    CandidateEmail = app?.CandidateEmail ?? "Unknown",
                    JobTitle = jobTitle ?? "Unknown Job",
                    Status = hr != null ? "completed" : "pending",
                    FinalVerdict = hr != null ? hr.FinalVerdict : e.AiVerdict,
                };
            }).ToList();

            return Result.Success(new PaginatedResponse<EvaluationListItemResponse>
            {
                Items = responseList,
                Total = totalItems,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalItems / pageSize)
            });
        }
    }
}
