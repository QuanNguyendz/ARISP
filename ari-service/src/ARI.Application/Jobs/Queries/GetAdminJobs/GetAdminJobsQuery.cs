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

namespace ARI.Application.Jobs.Queries.GetAdminJobs
{
    /// <summary>
    /// Danh sách job dành cho HR (bao gồm cả draft, closed...).
    /// <paramref name="MineUserId"/> != null: chỉ trả về tin do người đó tạo (Recruiter workspace).
    /// </summary>
    public record GetAdminJobsQuery(Guid? MineUserId) : IRequest<Result<List<JobPostingListItemResponse>>>;

    public class GetAdminJobsQueryHandler : IRequestHandler<GetAdminJobsQuery, Result<List<JobPostingListItemResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAdminJobsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<JobPostingListItemResponse>>> Handle(GetAdminJobsQuery request, CancellationToken ct)
        {
            var mineUid = request.MineUserId;

            // Projection thẳng sang DTO danh sách — KHÔNG kéo JobDescription/ScoringRubric/persona/JD file.
            var jobList = await _unitOfWork.Repository<JobPosting>().QueryAsync(q =>
            {
                var query = mineUid.HasValue ? q.Where(j => j.CreatedByUserId == mineUid.Value) : q;
                return query
                    .OrderByDescending(j => j.CreatedAt)
                    .Select(j => new JobPostingListItemResponse
                    {
                        Id = j.Id,
                        Title = j.Title,
                        Department = j.Department,
                        InterviewMode = j.InterviewMode,
                        Status = j.Status,
                        DetectedLanguage = j.DetectedLanguage,
                        LanguageRequirement = j.LanguageRequirement,
                        CreatedAt = j.CreatedAt,
                        PublishedAt = j.PublishedAt,
                        Location = j.Location,
                        WorkMode = j.WorkMode,
                        EmploymentType = j.EmploymentType,
                        ExperienceLevel = j.ExperienceLevel,
                        JobCategory = j.JobCategory,
                        IsUrgent = j.IsUrgent ?? false,
                        Vacancies = j.Vacancies,
                        Skills = j.Skills,
                        SalaryMin = j.SalaryMin,
                        SalaryMax = j.SalaryMax,
                        SalaryCurrency = j.SalaryCurrency,
                        SalaryIsNegotiable = j.SalaryIsNegotiable ?? false,
                        CreatedByUserId = j.CreatedByUserId,
                        RejectionReason = j.RejectionReason,
                    });
            }, ct);

            // Đếm ứng viên theo tin bằng SQL GROUP BY (không nạp Application/CvText).
            var jobIds = jobList.Select(j => j.Id).ToList();
            var countByJob = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .QueryAsync(q => q.Where(a => jobIds.Contains(a.JobPostingId))
                        .GroupBy(a => a.JobPostingId)
                        .Select(g => new { JobId = g.Key, Count = g.Count() }), ct))
                .ToDictionary(x => x.JobId, x => x.Count);

            // Tên người tạo tin (batch, chỉ cột cần)
            var creatorIds = jobList.Select(j => j.CreatedByUserId).Distinct().ToList();
            var creatorNameById = (await _unitOfWork.Repository<User>()
                    .QueryAsync(q => q.Where(u => creatorIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email }), ct))
                .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName);

            foreach (var dto in jobList)
            {
                dto.Skills ??= new List<string>();
                dto.ApplicantCount = countByJob.TryGetValue(dto.Id, out var c) ? c : 0;
                dto.CreatedByName = creatorNameById.TryGetValue(dto.CreatedByUserId, out var name) ? name : null;
            }

            return Result.Success(jobList);
        }
    }
}
