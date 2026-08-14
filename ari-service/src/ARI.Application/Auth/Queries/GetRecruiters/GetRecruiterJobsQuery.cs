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

namespace ARI.Application.Auth.Queries.GetRecruiters
{
    /// <summary>
    /// Tin một Recruiter đang phụ trách — dùng ở hộp thoại chuyển giao để HR Lead chọn đúng tin
    /// cần chuyển, kèm số hồ sơ để thấy chuyển đi là chuyển bao nhiêu việc.
    /// </summary>
    public record GetRecruiterJobsQuery(Guid RecruiterId) : IRequest<Result<List<RecruiterJobBriefDto>>>;

    public class GetRecruiterJobsQueryHandler
        : IRequestHandler<GetRecruiterJobsQuery, Result<List<RecruiterJobBriefDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetRecruiterJobsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<RecruiterJobBriefDto>>> Handle(GetRecruiterJobsQuery request, CancellationToken ct)
        {
            var jobs = (await _unitOfWork.Repository<JobPosting>()
                .FindAsync(j => j.DeletedAt == null && j.CreatedByUserId == request.RecruiterId, ct)).ToList();

            if (jobs.Count == 0)
                return Result.Success(new List<RecruiterJobBriefDto>());

            var jobIds = jobs.Select(j => j.Id).ToHashSet();
            var applications = (await _unitOfWork.Repository<Domain.Entities.Application>()
                .FindAsync(a => a.DeletedAt == null && jobIds.Contains(a.JobPostingId), ct)).ToList();

            var countByJob = applications
                .GroupBy(a => a.JobPostingId)
                .ToDictionary(g => g.Key, g => g.Count());

            return Result.Success(jobs
                .Select(j => new RecruiterJobBriefDto
                {
                    Id = j.Id,
                    Title = j.Title,
                    Status = j.Status,
                    Candidates = countByJob.TryGetValue(j.Id, out var c) ? c : 0,
                    CreatedAt = j.CreatedAt
                })
                // Tin đang tuyển lên trước — đó là thứ cần chuyển gấp khi ai đó quá tải.
                .OrderByDescending(j => j.Status == "active")
                .ThenByDescending(j => j.Candidates)
                .ThenBy(j => j.Title)
                .ToList());
        }
    }
}
