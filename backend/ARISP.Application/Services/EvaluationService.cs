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

        public async Task<Result<PaginatedResponse<EvaluationListItemResponse>>> GetEvaluationsAsync(
            Guid? jobPostingId, 
            string? status, 
            int page, 
            int pageSize, 
            CancellationToken ct = default)
        {
            var evaluations = await _unitOfWork.Repository<Evaluation>().GetAllAsync(ct);
            var hrReviews = await _unitOfWork.Repository<HrReview>().GetAllAsync(ct);
            var reviewedEvalIds = hrReviews.Select(r => r.EvaluationId).ToHashSet();

            // Filter by JobPostingId if specified
            if (jobPostingId.HasValue)
            {
                var applications = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(a => a.JobPostingId == jobPostingId.Value, ct);
                var appIds = applications.Select(a => a.Id).ToHashSet();
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
            var applicationsForEvals = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(a => appIdsInEvals.Contains(a.Id), ct);
            var appDict = applicationsForEvals.ToDictionary(a => a.Id);

            var jobIds = applicationsForEvals.Select(a => a.JobPostingId).Distinct().ToList();
            var jobs = await _unitOfWork.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id), ct);
            var jobDict = jobs.ToDictionary(j => j.Id);

            var hrDict = hrReviews.ToDictionary(r => r.EvaluationId);

            var responseList = paginatedEvals.Select(e =>
            {
                appDict.TryGetValue(e.ApplicationId, out var app);
                JobPosting? job = null;
                if (app != null)
                {
                    jobDict.TryGetValue(app.JobPostingId, out job);
                }
                hrDict.TryGetValue(e.Id, out var hr);

                return EvaluationListItemResponse.FromEntity(
                    e, 
                    app ?? new ARISP.Domain.Entities.Application { CandidateName = "Unknown", CandidateEmail = "Unknown" }, 
                    job ?? new JobPosting { Title = "Unknown Job" }, 
                    hr);
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

            var evaluations = await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.ApplicationId == applicationId, ct);
            var hrReviews = await _unitOfWork.Repository<HrReview>().GetAllAsync(ct);
            var hrDict = hrReviews.ToDictionary(r => r.EvaluationId);

            var responseList = evaluations.Select(e =>
            {
                hrDict.TryGetValue(e.Id, out var hr);
                return EvaluationListItemResponse.FromEntity(e, application, job, hr);
            }).ToList();

            return Result.Success(responseList);
        }
    }
}
