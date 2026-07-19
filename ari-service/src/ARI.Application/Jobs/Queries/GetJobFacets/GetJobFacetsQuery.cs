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

namespace ARI.Application.Jobs.Queries.GetJobFacets
{
    /// <summary>
    /// Bộ lọc khả dụng cho Job Board: chỉ trả về những giá trị thực sự có trong các tin
    /// đang active &amp; public, kèm số lượng (vd: "Junior (2)"). Dùng cho sidebar bộ lọc.
    /// </summary>
    public record GetJobFacetsQuery : IRequest<Result<JobFacetsResponse>>;

    public class GetJobFacetsQueryHandler : IRequestHandler<GetJobFacetsQuery, Result<JobFacetsResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetJobFacetsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<JobFacetsResponse>> Handle(GetJobFacetsQuery request, CancellationToken ct)
        {
            var jobs = (await _unitOfWork.Repository<JobPosting>().FindAsync(
                j => j.IsPublicListing && j.Status == "active" && (!j.ApplicationDeadline.HasValue || j.ApplicationDeadline.Value > DateTimeOffset.UtcNow),
                ct)).ToList();

            var facets = new JobFacetsResponse
            {
                TotalJobs = jobs.Count,
                Categories = JobsSupport.BuildFacet(jobs.Select(j => j.JobCategory), JobsSupport.CategoryLabels, JobsSupport.CategoryOrder),
                EmploymentTypes = JobsSupport.BuildFacet(jobs.Select(j => j.EmploymentType), JobsSupport.EmploymentTypeLabels, JobsSupport.EmploymentTypeOrder),
                ExperienceLevels = JobsSupport.BuildFacet(jobs.Select(j => j.ExperienceLevel), JobsSupport.ExperienceLevelLabels, JobsSupport.ExperienceLevelOrder),
                WorkModes = JobsSupport.BuildFacet(jobs.Select(j => j.WorkMode), JobsSupport.WorkModeLabels, JobsSupport.WorkModeOrder),
                Languages = JobsSupport.BuildFacet(jobs.Select(j => j.DetectedLanguage), JobsSupport.LanguageLabels, null),
                Locations = JobsSupport.BuildFacet(jobs.Select(j => j.Location), null, null),
                Skills = JobsSupport.BuildFacet(jobs.SelectMany(j => j.Skills ?? new List<string>()), null, null)
            };

            return Result.Success(facets);
        }
    }
}
