using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Jobs.Queries.GetJobs
{
    /// <summary>Trang kết quả Job Board: { items, totalCount }.</summary>
    public record JobsPageDto(List<JobPostingListItemResponse> Items, int TotalCount);

    /// <summary>
    /// Danh sách job công khai trên Job Board kèm lọc, phân trang, và sắp xếp.
    /// <paramref name="CurrentUserId"/>: danh tính đang đăng nhập (nếu có) — dùng cho sort "relevance".
    /// </summary>
    public record GetJobsQuery(
        string? Search, string? Categories, string? EmploymentTypes, string? ExperienceLevels,
        string? WorkModes, string? Locations, string? Skills, string? Languages, string? SortBy,
        int? MinSalary, int? MaxSalary, bool? SalaryIsNegotiable,
        int Page, int PageSize, Guid? CurrentUserId) : IRequest<Result<JobsPageDto>>;

    public class GetJobsQueryHandler : IRequestHandler<GetJobsQuery, Result<JobsPageDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetJobsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<JobsPageDto>> Handle(GetJobsQuery request, CancellationToken ct)
        {
            var page = request.Page;
            var pageSize = request.PageSize;
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 8;

            var search = request.Search;
            var minSalary = request.MinSalary;
            var maxSalary = request.MaxSalary;
            var salaryIsNegotiable = request.SalaryIsNegotiable;

            var catList = JobsSupport.ParseCsv(request.Categories);
            var empList = JobsSupport.ParseCsv(request.EmploymentTypes);
            var expList = JobsSupport.ParseCsv(request.ExperienceLevels);
            var wmList = JobsSupport.ParseCsv(request.WorkModes);
            var locList = JobsSupport.ParseCsv(request.Locations);
            var skillList = JobsSupport.ParseCsv(request.Skills);
            var langList = JobsSupport.ParseCsv(request.Languages);

            // Bộ lọc dùng chung cho cả count lẫn lấy trang dữ liệu.
            IQueryable<JobPosting> ApplyFilters(IQueryable<JobPosting> q)
            {
                var query = q.Where(j => j.IsPublicListing && j.Status == "active" && (!j.ApplicationDeadline.HasValue || j.ApplicationDeadline.Value > DateTimeOffset.UtcNow));

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(j => j.Title.ToLower().Contains(s) ||
                                         (j.Department != null && j.Department.ToLower().Contains(s)) ||
                                         (j.Skills != null && j.Skills.Any(sk => sk.ToLower().Contains(s))));
                }

                if (catList.Any())
                {
                    var cats = catList.Select(c => c.ToLower()).ToList();
                    query = query.Where(j => j.JobCategory != null && cats.Contains(j.JobCategory.ToLower()));
                }

                if (empList.Any())
                {
                    var emps = empList.Select(e => e.ToLower()).ToList();
                    query = query.Where(j => j.EmploymentType != null && emps.Contains(j.EmploymentType.ToLower()));
                }

                if (expList.Any())
                {
                    var exps = expList.Select(e => e.ToLower()).ToList();
                    query = query.Where(j => j.ExperienceLevel != null && exps.Contains(j.ExperienceLevel.ToLower()));
                }

                if (wmList.Any())
                {
                    var wms = wmList.Select(w => w.ToLower()).ToList();
                    query = query.Where(j => j.WorkMode != null && wms.Contains(j.WorkMode.ToLower()));
                }

                if (locList.Any())
                {
                    var locs = locList.Select(l => l.ToLower()).ToList();
                    query = query.Where(j => j.Location != null && locs.Contains(j.Location.ToLower()));
                }

                if (skillList.Any())
                {
                    var sks = skillList.Select(s => s.ToLower()).ToList();
                    query = query.Where(j => j.Skills != null && j.Skills.Any(s => sks.Contains(s.ToLower())));
                }

                if (langList.Any())
                {
                    var langs = langList.Select(l => l.ToLower()).ToList();
                    query = query.Where(j => j.DetectedLanguage != null && langs.Contains(j.DetectedLanguage.ToLower()));
                }

                if (salaryIsNegotiable == true)
                {
                    query = query.Where(j => j.SalaryIsNegotiable == true || ((j.SalaryMin ?? 0) == 0 && (j.SalaryMax ?? 0) == 0));
                }
                else if (minSalary.HasValue || maxSalary.HasValue)
                {
                    query = query.Where(j => j.SalaryIsNegotiable != true && (j.SalaryMin != null || j.SalaryMax != null));
                    if (minSalary.HasValue)
                    {
                        query = query.Where(j => (j.SalaryCurrency == "USD" ? (j.SalaryMax ?? 0) * 25000 : (j.SalaryMax ?? 0)) >= minSalary.Value);
                    }
                    if (maxSalary.HasValue)
                    {
                        query = query.Where(j => (j.SalaryCurrency == "USD" ? (j.SalaryMin ?? 0) * 25000 : (j.SalaryMin ?? 0)) <= maxSalary.Value);
                    }
                }

                return query;
            }

            // 1. Lọc và lấy total count
            var totalCount = (await _unitOfWork.Repository<JobPosting>().QueryAsync<Guid>(
                q => ApplyFilters(q).Select(j => j.Id), ct)).Count;

            // Sắp xếp theo "độ phù hợp CV": cần danh tính ứng viên đang đăng nhập. Không có kỹ năng
            // (khách vãng lai / staff / hồ sơ trống) → rơi về "mới nhất". Chấm điểm + phân trang ở
            // bộ nhớ (tập tin active của 1 doanh nghiệp là nhỏ) để tránh sort collection ở SQL.
            var useRelevance = string.Equals(request.SortBy, "relevance", StringComparison.OrdinalIgnoreCase);
            var candidateSkillsLower = useRelevance ? await GetCandidateSkillsLowerAsync(request.CurrentUserId, ct) : new List<string>();
            if (useRelevance && candidateSkillsLower.Count == 0) useRelevance = false;

            // 2. Lấy dữ liệu phân trang và sắp xếp
            var items = await _unitOfWork.Repository<JobPosting>().QueryAsync(q =>
            {
                var query = ApplyFilters(q);

                // Độ phù hợp CV: không sort/phân trang ở SQL — chấm điểm & phân trang ở bộ nhớ bên dưới.
                if (useRelevance)
                {
                    return query.Select(j => JobPostingListItemResponse.FromEntity(j));
                }

                // Sắp xếp
                if (string.Equals(request.SortBy, "salary_desc", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(request.SortBy, "salary", StringComparison.OrdinalIgnoreCase))
                {
                    query = query
                        .OrderBy(j => (j.SalaryIsNegotiable == true || ((j.SalaryMin ?? 0) == 0 && (j.SalaryMax ?? 0) == 0)) ? 1 : 0)
                        .ThenByDescending(j => j.SalaryCurrency == "USD" ? (j.SalaryMax ?? 0) * 25000 : (j.SalaryMax ?? 0))
                        .ThenByDescending(j => j.SalaryCurrency == "USD" ? (j.SalaryMin ?? 0) * 25000 : (j.SalaryMin ?? 0));
                }
                else if (string.Equals(request.SortBy, "salary_asc", StringComparison.OrdinalIgnoreCase))
                {
                    query = query
                        .OrderBy(j => (j.SalaryIsNegotiable == true || ((j.SalaryMin ?? 0) == 0 && (j.SalaryMax ?? 0) == 0)) ? 1 : 0)
                        .ThenBy(j => j.SalaryCurrency == "USD" ? (j.SalaryMin ?? 0) * 25000 : (j.SalaryMin ?? 0))
                        .ThenBy(j => j.SalaryCurrency == "USD" ? (j.SalaryMax ?? 0) * 25000 : (j.SalaryMax ?? 0));
                }
                else
                {
                    query = query.OrderByDescending(j => j.PublishedAt ?? j.CreatedAt);
                }

                // Phân trang
                query = query.Skip((page - 1) * pageSize).Take(pageSize);

                return query.Select(j => JobPostingListItemResponse.FromEntity(j));
            }, ct);

            // Độ phù hợp CV: xếp theo số kỹ năng trùng giảm dần → tin mới nhất, rồi phân trang ở bộ nhớ.
            if (useRelevance)
            {
                var skillSet = new HashSet<string>(candidateSkillsLower);
                items = items
                    .OrderByDescending(j => j.Skills.Count(s => skillSet.Contains(s.ToLowerInvariant())))
                    .ThenByDescending(j => j.PublishedAt ?? j.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }

            return Result.Success(new JobsPageDto(items, totalCount));
        }

        /// <summary>
        /// Kỹ năng (lowercase, distinct) của ứng viên đang đăng nhập, lấy từ hồ sơ
        /// (<c>CandidateAccount.SkillsJson</c>). Rỗng nếu không phải ứng viên / chưa đăng nhập /
        /// hồ sơ chưa có kỹ năng — dùng cho sắp xếp "độ phù hợp CV".
        /// </summary>
        private async Task<List<string>> GetCandidateSkillsLowerAsync(Guid? candidateId, CancellationToken ct)
        {
            if (!candidateId.HasValue)
                return new List<string>();

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId.Value, ct);
            if (acc == null || string.IsNullOrWhiteSpace(acc.SkillsJson))
                return new List<string>();

            try
            {
                var skills = JsonSerializer.Deserialize<List<string>>(acc.SkillsJson) ?? new List<string>();
                return skills
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
