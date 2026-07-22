using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CandidatePortal
{
    /// <summary>Thẻ job đã lưu — property khớp shape anonymous cũ (subset JobPostingListItemResponse + SavedAt).</summary>
    public record SavedJobItemDto(
        Guid Id, string Title, string? Department, string? Location, string? WorkMode,
        string? EmploymentType, string? ExperienceLevel, string? JobCategory, List<string> Skills,
        bool IsUrgent, decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency, bool SalaryIsNegotiable,
        DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt, DateTimeOffset SavedAt);

    // ============================================================
    // GET /api/portal/saved-jobs
    // ============================================================

    public record GetSavedJobsQuery(Guid CandidateId) : IRequest<Result<List<SavedJobItemDto>>>;

    public class GetSavedJobsQueryHandler : IRequestHandler<GetSavedJobsQuery, Result<List<SavedJobItemDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetSavedJobsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<SavedJobItemDto>>> Handle(GetSavedJobsQuery request, CancellationToken ct)
        {
            var saved = (await _unitOfWork.Repository<SavedJob>()
                .FindAsync(s => s.CandidateAccountId == request.CandidateId, ct)).ToList();
            if (saved.Count == 0)
                return Result.Success(new List<SavedJobItemDto>());

            var savedAtByJob = saved
                .GroupBy(s => s.JobPostingId)
                .ToDictionary(g => g.Key, g => g.Max(s => s.CreatedAt));

            var jobIds = savedAtByJob.Keys.ToList();
            var jobs = await _unitOfWork.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id), ct);

            // Chỉ hiển thị tin còn mở để ứng viên có thể ứng tuyển.
            var response = jobs
                .Where(j => j.Status == "active" && j.IsPublicListing)
                .Select(j =>
                {
                    var dto = JobPostingListItemResponse.FromEntity(j);
                    return new SavedJobItemDto(
                        dto.Id, dto.Title, dto.Department, dto.Location, dto.WorkMode,
                        dto.EmploymentType, dto.ExperienceLevel, dto.JobCategory, dto.Skills,
                        dto.IsUrgent, dto.SalaryMin, dto.SalaryMax, dto.SalaryCurrency, dto.SalaryIsNegotiable,
                        dto.CreatedAt, dto.PublishedAt,
                        savedAtByJob.TryGetValue(j.Id, out var at) ? at : j.CreatedAt);
                })
                .OrderByDescending(j => j.SavedAt)
                .ToList();

            return Result.Success(response);
        }
    }

    // ============================================================
    // GET /api/portal/saved-jobs/ids
    // ============================================================

    public record GetSavedJobIdsQuery(Guid CandidateId) : IRequest<Result<List<Guid>>>;

    public class GetSavedJobIdsQueryHandler : IRequestHandler<GetSavedJobIdsQuery, Result<List<Guid>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetSavedJobIdsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<Guid>>> Handle(GetSavedJobIdsQuery request, CancellationToken ct)
        {
            var ids = (await _unitOfWork.Repository<SavedJob>()
                .FindAsync(s => s.CandidateAccountId == request.CandidateId, ct))
                .Select(s => s.JobPostingId)
                .Distinct()
                .ToList();

            return Result.Success(ids);
        }
    }

    // ============================================================
    // POST /api/portal/saved-jobs/{jobPostingId} (idempotent)
    // ============================================================

    public record SaveJobCommand(Guid CandidateId, Guid JobPostingId) : IRequest<Result>;

    public class SaveJobCommandHandler : IRequestHandler<SaveJobCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public SaveJobCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(SaveJobCommand request, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
            if (job == null)
                return Result.Failure("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            var existing = (await _unitOfWork.Repository<SavedJob>()
                .FindAsync(s => s.CandidateAccountId == request.CandidateId && s.JobPostingId == request.JobPostingId, ct))
                .FirstOrDefault();

            if (existing == null)
            {
                await _unitOfWork.Repository<SavedJob>().AddAsync(new SavedJob
                {
                    CandidateAccountId = request.CandidateId,
                    JobPostingId = request.JobPostingId
                }, ct);
                await _unitOfWork.SaveChangesAsync();
            }

            return Result.Success();
        }
    }

    // ============================================================
    // DELETE /api/portal/saved-jobs/{jobPostingId} (idempotent)
    // ============================================================

    public record UnsaveJobCommand(Guid CandidateId, Guid JobPostingId) : IRequest<Result>;

    public class UnsaveJobCommandHandler : IRequestHandler<UnsaveJobCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UnsaveJobCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UnsaveJobCommand request, CancellationToken ct)
        {
            var existing = (await _unitOfWork.Repository<SavedJob>()
                .FindAsync(s => s.CandidateAccountId == request.CandidateId && s.JobPostingId == request.JobPostingId, ct))
                .ToList();

            if (existing.Count > 0)
            {
                foreach (var s in existing)
                    _unitOfWork.Repository<SavedJob>().Delete(s);
                await _unitOfWork.SaveChangesAsync();
            }

            return Result.Success();
        }
    }
}
