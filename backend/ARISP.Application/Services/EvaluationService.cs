using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Common;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.Application.Services
{
    public class EvaluationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public EvaluationService(IUnitOfWork unitOfWork)
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

        public async Task<Result<PaginatedResponse<EvaluationListItemResponse>>> GetEvaluationsAsync(
            Guid? jobPostingId,
            string? status,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            List<EvalLite> evaluations = await _unitOfWork.Repository<Evaluation>()
                .QueryAsync(q => q.Select(e => new EvalLite
                {
                    Id = e.Id, SessionId = e.SessionId, ApplicationId = e.ApplicationId, RoundNumber = e.RoundNumber,
                    SessionType = e.SessionType, AiVerdict = e.AiVerdict, OverallScore = e.OverallScore,
                    CheatScore = e.CheatScore, CreatedAt = e.CreatedAt,
                }), ct);
            var hrReviews = await _unitOfWork.Repository<HrReview>()
                .QueryAsync(q => q.Select(r => new ReviewLite { EvaluationId = r.EvaluationId, FinalVerdict = r.FinalVerdict }), ct);
            var reviewedEvalIds = hrReviews.Select(r => r.EvaluationId).ToHashSet();

            // Filter by JobPostingId if specified
            if (jobPostingId.HasValue)
            {
                var appIds = (await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
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
                return Result.Success(new PaginatedResponse<EvaluationListItemResponse>
                {
                    Items = new List<EvaluationListItemResponse>(),
                    Total = totalItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalItems / pageSize)
                });
            }

            var appIdsInEvals = paginatedEvals.Select(e => e.ApplicationId).Distinct().ToList();
            var appDict = (await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
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

        public async Task<Result<EvaluationDetailResponse>> GetEvaluationDetailAsync(Guid id, CancellationToken ct = default)
        {
            // First check by evaluation ID
            var evaluation = await _unitOfWork.Repository<Evaluation>().GetByIdAsync(id, ct);
            
            // If not found, try search by SessionId
            if (evaluation == null)
            {
                var evals = await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.SessionId == id, ct);
                evaluation = evals.FirstOrDefault();
            }

            if (evaluation == null)
                return Result.Failure<EvaluationDetailResponse>("Evaluation not found.");

            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(evaluation.ApplicationId, ct);
            if (application == null)
                return Result.Failure<EvaluationDetailResponse>("Application associated with this evaluation was not found.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            if (job == null)
                return Result.Failure<EvaluationDetailResponse>("Job posting associated with this evaluation was not found.");

            var hrReviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => r.EvaluationId == evaluation.Id, ct);
            var hrReview = hrReviews.FirstOrDefault();

            var response = EvaluationDetailResponse.FromEntity(evaluation, application, job, hrReview);
            return Result.Success(response);
        }

        public async Task<Result<List<EvaluationListItemResponse>>> GetEvaluationsByApplicationIdAsync(Guid applicationId, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result.Failure<List<EvaluationListItemResponse>>("Application not found.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            if (job == null)
                return Result.Failure<List<EvaluationListItemResponse>>("Job posting associated with this application was not found.");

            var evaluations = (await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.ApplicationId == applicationId, ct)).ToList();
            var evalIds = evaluations.Select(e => e.Id).ToList();
            // Chỉ lấy HrReview của các đánh giá thuộc hồ sơ này (không quét toàn bảng).
            var hrDict = (await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId), ct))
                .ToDictionary(r => r.EvaluationId);

            var responseList = evaluations.Select(e =>
            {
                hrDict.TryGetValue(e.Id, out var hr);
                return EvaluationListItemResponse.FromEntity(e, application, job, hr);
            }).ToList();

            return Result.Success(responseList);
        }
    }
}
