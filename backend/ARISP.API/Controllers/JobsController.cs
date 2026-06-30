using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using ARISP.Domain.Entities;
using ARISP.Domain.Constants;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private const string HrRoles = "super_admin,hr_admin,recruiter";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDocumentParserService _documentParser;
        private readonly IFileStorageService _fileStorage;
        private readonly IGeminiProvider _geminiProvider;
        private readonly ApplicationService _applicationService;
        private readonly IJdStampService _jdStampService;
        private readonly IRagIngestionService _ragIngestion;
        private readonly INotificationService _notificationService;
        private readonly ILogger<JobsController> _logger;

        public JobsController(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            IDocumentParserService documentParser,
            IFileStorageService fileStorage,
            IGeminiProvider geminiProvider,
            ApplicationService applicationService,
            IJdStampService jdStampService,
            IRagIngestionService ragIngestion,
            INotificationService notificationService,
            ILogger<JobsController> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _documentParser = documentParser;
            _fileStorage = fileStorage;
            _geminiProvider = geminiProvider;
            _applicationService = applicationService;
            _jdStampService = jdStampService;
            _ragIngestion = ragIngestion;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>HR tạo job posting kèm cấu hình vòng phỏng vấn.</summary>
        [HttpPost]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> CreateJob([FromBody] CreateJobPostingRequest request, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { message = "Title is required." });

            if (string.IsNullOrWhiteSpace(request.JobDescription))
                return BadRequest(new { message = "JobDescription is required." });

            if (request.Title?.Length > 200)
                return BadRequest(new { message = "Title cannot exceed 200 characters." });

            var allowedModes = new[] { "remote", "onsite", "both" };
            if (!allowedModes.Contains(request.InterviewMode))
                return BadRequest(new { message = "InterviewMode must be 'remote', 'onsite', or 'both'." });

            if (request.InterviewMode != "remote" && string.IsNullOrWhiteSpace(request.Location))
                return BadRequest(new { message = "Location is required when InterviewMode is not 'remote'." });

            if (request.SalaryMin < 0 || request.SalaryMax < 0)
                return BadRequest(new { message = "Salary cannot be negative." });

            if (request.SalaryMin.HasValue && request.SalaryMax.HasValue && request.SalaryMax < request.SalaryMin)
                return BadRequest(new { message = "SalaryMax cannot be less than SalaryMin." });

            if (request.SalaryIsNegotiable)
            {
                if (request.SalaryMin.HasValue || request.SalaryMax.HasValue)
                    return BadRequest(new { message = "SalaryMin and SalaryMax must be null when SalaryIsNegotiable is true." });
            }

            var allowedCategories = new[] { "backend", "frontend", "devops", "qa", "data", "ai_ml", "mobile", "pm", "designer", "other" };
            if (!string.IsNullOrWhiteSpace(request.JobCategory) && !allowedCategories.Contains(request.JobCategory.ToLower()))
                return BadRequest(new { message = $"JobCategory is invalid. Must be one of: {string.Join(", ", allowedCategories)}" });

            if (request.ApplicationDeadline.HasValue && request.ApplicationDeadline.Value <= DateTimeOffset.UtcNow)
                return BadRequest(new { message = "ApplicationDeadline must be in the future." });

            if (request.RescheduleDeadlineHours < 0)
                return BadRequest(new { message = "RescheduleDeadlineHours cannot be negative." });

            if (request.InviteTokenTtlHours <= 0)
                return BadRequest(new { message = "InviteTokenTtlHours must be greater than 0." });

            if (request.RoundConfigs == null || request.RoundConfigs.Count == 0)
                return BadRequest(new { message = "At least one interview round configuration is required." });

            foreach (var round in request.RoundConfigs)
            {
                if (round.RoundNumber <= 0) return BadRequest(new { message = "RoundNumber must be > 0." });
                if (round.MaxDurationMinutes <= 0) return BadRequest(new { message = "MaxDurationMinutes must be > 0." });
                if (round.InterviewCodeTtlHours <= 0) return BadRequest(new { message = "InterviewCodeTtlHours must be > 0." });
            }

            var creator = await _unitOfWork.Repository<User>().GetByIdAsync(userId, ct);
            if (creator == null)
                return Unauthorized(new { message = "User not found for the current token." });

            var detectedLang = JobDescriptionLanguageDetector.Detect(request.JobDescription);

            var job = new JobPosting
            {
                CreatedByUserId = userId,
                Title = request.Title!.Trim(),
                Department = request.Department?.Trim(),
                JobDescription = request.JobDescription!.Trim(),
                JdFileUrl = request.JdFileUrl,
                JdFileName = request.JdFileName,
                JdFileFormat = request.JdFileFormat,
                InterviewMode = request.InterviewMode,
                Status = "draft",
                IsPublicListing = request.IsPublicListing,
                DetectedLanguage = detectedLang,
                LanguageRequirement = !string.IsNullOrWhiteSpace(request.LanguageRequirement) 
                                        ? request.LanguageRequirement 
                                        : (detectedLang == "vi" ? "Tiếng Việt" : "Yêu cầu ngôn ngữ " + detectedLang),
                RescheduleDeadlineHours = request.RescheduleDeadlineHours,
                InviteTokenTtlHours = request.InviteTokenTtlHours,
                ScoringRubric = request.ScoringRubric.HasValue ? request.ScoringRubric.Value.GetRawText() : null,
                PersonaName = request.PersonaName,
                PersonaVoiceId = request.PersonaVoiceId,
                PersonaStyle = request.PersonaStyle,
                Location = request.Location,
                WorkMode = request.WorkMode,
                SalaryMin = request.SalaryMin,
                SalaryMax = request.SalaryMax,
                SalaryCurrency = string.IsNullOrWhiteSpace(request.SalaryCurrency) ? "VND" : request.SalaryCurrency,
                SalaryIsNegotiable = request.SalaryIsNegotiable,
                EmploymentType = request.EmploymentType,
                ExperienceLevel = request.ExperienceLevel,
                Skills = request.Skills ?? new List<string>(),
                JobCategory = request.JobCategory?.ToLower(),
                ApplicationDeadline = request.ApplicationDeadline,
                IsUrgent = request.IsUrgent,
                Vacancies = request.Vacancies.HasValue && request.Vacancies.Value > 0 ? request.Vacancies : null
            };

            await _unitOfWork.Repository<JobPosting>().AddAsync(job, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Ingest JD vào RAG (chunk+embed+pgvector) để retrieve khi phỏng vấn. Không chặn
            // tạo job nếu RAG service lỗi — chỉ ghi log (có thể re-ingest sau).
            if (!string.IsNullOrWhiteSpace(job.JobDescription))
            {
                try
                {
                    await _ragIngestion.IngestAsync("jd", job.Id, job.JobDescription, ct: ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RAG ingest JD thất bại cho job {JobId} — bỏ qua, có thể re-ingest sau.", job.Id);
                }
            }

            var roundDtos = new List<RoundConfigDto>();
            foreach (var round in request.RoundConfigs.OrderBy(r => r.RoundNumber))
            {
                var config = new InterviewRoundConfig
                {
                    JobPostingId = job.Id,
                    RoundNumber = round.RoundNumber,
                    RoundType = round.RoundType,
                    InterviewLanguage = round.InterviewLanguage ?? detectedLang,
                    InterviewCodeTtlHours = round.InterviewCodeTtlHours,
                    MaxDurationMinutes = round.MaxDurationMinutes
                };
                await _unitOfWork.Repository<InterviewRoundConfig>().AddAsync(config, ct);
                roundDtos.Add(RoundConfigDto.FromEntity(config));
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // Gửi thông báo SignalR cho Recruiter vừa tạo job
            await _notificationService.PublishUserEventAsync(userId, "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = job.Status, Title = job.Title }, ct);

            return Ok(JobPostingResponse.FromEntity(job, roundDtos));
        }

        private static List<string> ParseCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim())
                      .ToList();
        }

        /// <summary>Danh sách job công khai trên Job Board kèm lọc, phân trang, và sắp xếp.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobs(
            [FromQuery] string? search,
            [FromQuery] string? categories,
            [FromQuery] string? employmentTypes,
            [FromQuery] string? experienceLevels,
            [FromQuery] string? workModes,
            [FromQuery] string? locations,
            [FromQuery] string? skills,
            [FromQuery] string? languages,
            [FromQuery] string? sortBy,
            [FromQuery] int? minSalary,
            [FromQuery] int? maxSalary,
            [FromQuery] bool? salaryIsNegotiable,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 8,
            CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 8;

            var catList = ParseCsv(categories);
            var empList = ParseCsv(employmentTypes);
            var expList = ParseCsv(experienceLevels);
            var wmList = ParseCsv(workModes);
            var locList = ParseCsv(locations);
            var skillList = ParseCsv(skills);
            var langList = ParseCsv(languages);

            // 1. Lọc và lấy total count
            var totalCount = (await _unitOfWork.Repository<JobPosting>().QueryAsync<Guid>(q =>
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

                return query.Select(j => j.Id);
            }, ct)).Count;

            // 2. Lấy dữ liệu phân trang và sắp xếp
            var items = await _unitOfWork.Repository<JobPosting>().QueryAsync(q =>
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

                // Sắp xếp
                if (string.Equals(sortBy, "salary_desc", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sortBy, "salary", StringComparison.OrdinalIgnoreCase))
                {
                    query = query
                        .OrderBy(j => (j.SalaryIsNegotiable == true || ((j.SalaryMin ?? 0) == 0 && (j.SalaryMax ?? 0) == 0)) ? 1 : 0)
                        .ThenByDescending(j => j.SalaryCurrency == "USD" ? (j.SalaryMax ?? 0) * 25000 : (j.SalaryMax ?? 0))
                        .ThenByDescending(j => j.SalaryCurrency == "USD" ? (j.SalaryMin ?? 0) * 25000 : (j.SalaryMin ?? 0));
                }
                else if (string.Equals(sortBy, "salary_asc", StringComparison.OrdinalIgnoreCase))
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

            return Ok(new { items, totalCount });
        }

        /// <summary>
        /// Bộ lọc khả dụng cho Job Board: chỉ trả về những giá trị thực sự có trong các tin
        /// đang active &amp; public, kèm số lượng (vd: "Junior (2)"). Dùng cho sidebar bộ lọc.
        /// </summary>
        [HttpGet("facets")]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobFacets(CancellationToken ct)
        {
            var jobs = (await _unitOfWork.Repository<JobPosting>().FindAsync(
                j => j.IsPublicListing && j.Status == "active" && (!j.ApplicationDeadline.HasValue || j.ApplicationDeadline.Value > DateTimeOffset.UtcNow),
                ct)).ToList();

            var facets = new JobFacetsResponse
            {
                TotalJobs = jobs.Count,
                Categories = BuildFacet(jobs.Select(j => j.JobCategory), CategoryLabels, CategoryOrder),
                EmploymentTypes = BuildFacet(jobs.Select(j => j.EmploymentType), EmploymentTypeLabels, EmploymentTypeOrder),
                ExperienceLevels = BuildFacet(jobs.Select(j => j.ExperienceLevel), ExperienceLevelLabels, ExperienceLevelOrder),
                WorkModes = BuildFacet(jobs.Select(j => j.WorkMode), WorkModeLabels, WorkModeOrder),
                Languages = BuildFacet(jobs.Select(j => j.DetectedLanguage), LanguageLabels, null),
                Locations = BuildFacet(jobs.Select(j => j.Location), null, null),
                Skills = BuildFacet(jobs.SelectMany(j => j.Skills ?? new List<string>()), null, null)
            };

            return Ok(facets);
        }

        // ===== Label maps & ordering cho facets (giá trị thô -> nhãn hiển thị) =====
        private static readonly Dictionary<string, string> CategoryLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["backend"] = "Backend", ["frontend"] = "Frontend", ["devops"] = "DevOps / Infra",
            ["qa"] = "QA / Testing", ["data"] = "Data", ["ai_ml"] = "AI / ML",
            ["mobile"] = "Mobile", ["pm"] = "Project Manager", ["designer"] = "Designer", ["other"] = "Khác"
        };
        private static readonly List<string> CategoryOrder = new()
        { "backend", "frontend", "devops", "qa", "data", "ai_ml", "mobile", "pm", "designer", "other" };

        private static readonly Dictionary<string, string> EmploymentTypeLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["full_time"] = "Full-time", ["part_time"] = "Part-time",
            ["contract"] = "Hợp đồng", ["internship"] = "Thực tập", ["freelance"] = "Freelance"
        };
        private static readonly List<string> EmploymentTypeOrder = new()
        { "full_time", "part_time", "contract", "internship", "freelance" };

        private static readonly Dictionary<string, string> ExperienceLevelLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["intern"] = "Intern / Fresher", ["fresher"] = "Intern / Fresher", ["junior"] = "Junior",
            ["middle"] = "Middle", ["senior"] = "Senior", ["lead"] = "Lead / Manager", ["manager"] = "Lead / Manager"
        };
        private static readonly List<string> ExperienceLevelOrder = new()
        { "intern", "fresher", "junior", "middle", "senior", "lead", "manager" };

        private static readonly Dictionary<string, string> WorkModeLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["onsite"] = "Onsite", ["hybrid"] = "Hybrid", ["remote"] = "Remote"
        };
        private static readonly List<string> WorkModeOrder = new() { "onsite", "hybrid", "remote" };

        private static readonly Dictionary<string, string> LanguageLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["vi"] = "Tiếng Việt", ["en"] = "English"
        };

        /// <summary>
        /// Gom nhóm + đếm theo giá trị thô (bỏ rỗng/null), gắn nhãn hiển thị và sắp xếp.
        /// Nếu có <paramref name="order"/> thì sắp theo thứ tự đó; nếu không thì sắp giảm dần theo count.
        /// </summary>
        private static List<JobFacetItem> BuildFacet(
            IEnumerable<string?> values,
            Dictionary<string, string>? labels,
            List<string>? order)
        {
            var grouped = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Select(g => new JobFacetItem
                {
                    // Khi có bảng nhãn (enum thô) -> chuẩn hoá value về lowercase để FE so khớp ổn định.
                    // Không có nhãn (location, skill) -> giữ nguyên giá trị gốc.
                    Value = labels != null ? g.Key.ToLowerInvariant() : g.Key,
                    Label = labels != null && labels.TryGetValue(g.Key, out var lbl) ? lbl : g.Key,
                    Count = g.Count()
                });

            // Gom nhóm theo Label hiển thị để tránh bị lặp (vd: "Intern / Fresher" cho cả 'intern' và 'fresher')
            if (labels != null)
            {
                grouped = grouped
                    .GroupBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new JobFacetItem
                    {
                        Value = string.Join(",", g.Select(x => x.Value).Distinct()),
                        Label = g.Key,
                        Count = g.Sum(x => x.Count)
                    });
            }

            var list = grouped.ToList();

            if (order != null)
            {
                return list
                    .OrderBy(item => {
                        var firstVal = item.Value.Split(',')[0];
                        var idx = order.IndexOf(firstVal);
                        return idx < 0 ? int.MaxValue : idx;
                    })
                    .ThenByDescending(item => item.Count)
                    .ToList();
            }

            return list
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Chi tiết job cho trang mô tả công việc (Hỗ trợ HR xem cả draft/paused).</summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobById(Guid id, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(id, ct);
            if (job == null)
                return NotFound(new { message = "Job posting not found." });

            var isStaff = User.Identity?.IsAuthenticated == true &&
                          (User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.HrAdmin) || User.IsInRole(AppRoles.Recruiter));

            if (!isStaff && (job.Status != "active" || !job.IsPublicListing))
                return NotFound(new { message = "Job posting not found." });

            var rounds = await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(
                r => r.JobPostingId == id,
                ct);

            var roundDtos = rounds.OrderBy(r => r.RoundNumber).Select(RoundConfigDto.FromEntity).ToList();
            var jobResponse = JobPostingResponse.FromEntity(job, roundDtos);

            // Staff: resolve storageKey của file JD -> URL dùng được (để xem/tải file JD gốc + bản đã đóng dấu)
            if (isStaff)
            {
                if (!string.IsNullOrEmpty(jobResponse.JdFileUrl))
                    jobResponse.JdFileUrl = await _fileStorage.GetUrlAsync(jobResponse.JdFileUrl, ct);
                if (!string.IsNullOrEmpty(jobResponse.SignedJdFileUrl))
                    jobResponse.SignedJdFileUrl = await _fileStorage.GetUrlAsync(jobResponse.SignedJdFileUrl, ct);
            }

            return Ok(jobResponse);
        }

        /// <summary>
        /// Danh sách job dành cho HR (bao gồm cả draft, closed...).
        /// <paramref name="mine"/>=true: chỉ trả về tin do người đang đăng nhập tạo (dùng cho Recruiter workspace).
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetAdminJobs(CancellationToken ct, [FromQuery] bool mine = false)
        {
            Guid? mineUid = null;
            if (mine)
            {
                if (_currentUserService.UserId is not { } uid || uid == Guid.Empty)
                    return Unauthorized(new { message = "Không xác định được người dùng." });
                mineUid = uid;
            }

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
            var countByJob = (await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
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

            return Ok(jobList);
        }

        [HttpPost("{id:guid}/slots")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> AddAvailabilitySlots(Guid id, [FromBody] List<CreateAvailabilitySlotRequest> slots, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(id, ct);
            if (job == null)
                return NotFound(new { message = "Job posting not found." });

            foreach (var slotDto in slots)
            {
                var slot = new AvailabilitySlot
                {
                    JobPostingId = job.Id,
                    RoundNumber = slotDto.RoundNumber,
                    StartTime = slotDto.StartTime,
                    EndTime = slotDto.EndTime,
                    Timezone = slotDto.Timezone,
                    Capacity = slotDto.Capacity,
                    BookedCount = 0
                };
                await _unitOfWork.Repository<AvailabilitySlot>().AddAsync(slot, ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new { message = "Availability slots configured successfully." });
        }
        /// <summary>
        /// HTTP PUT /api/jobs/{id}
        /// HR cập nhật job posting kèm cấu hình vòng phỏng vấn.
        /// Chỉ cho phép người tạo hoặc SuperAdmin, HrAdmin chỉnh sửa.
        /// Không cho phép cập nhật khi status là archived.
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> UpdateJob(Guid id, [FromBody] CreateJobPostingRequest request, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(id, ct);
            if (job == null)
                return NotFound(new { message = "Không tìm thấy tin tuyển dụng." });

            // 1. Validate ownership & roles
            var isAuthorized = _currentUserService.Role == AppRoles.SuperAdmin ||
                               _currentUserService.Role == AppRoles.HrAdmin ||
                               job.CreatedByUserId == userId;
            if (!isAuthorized)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Bạn không có quyền cập nhật tin tuyển dụng này." });
            }

            // 2. Không cho update nếu Status == "archived"
            if (string.Equals(job.Status, "archived", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Không thể cập nhật tin tuyển dụng đã lưu trữ (archived)." });
            }

            // 3. Validation logic (Đồng bộ với CreateJob)
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { message = "Title is required." });

            if (string.IsNullOrWhiteSpace(request.JobDescription))
                return BadRequest(new { message = "JobDescription is required." });

            if (request.Title?.Length > 200)
                return BadRequest(new { message = "Title cannot exceed 200 characters." });

            var allowedModes = new[] { "remote", "onsite", "both" };
            if (!allowedModes.Contains(request.InterviewMode))
                return BadRequest(new { message = "InterviewMode must be 'remote', 'onsite', or 'both'." });

            if (request.InterviewMode != "remote" && string.IsNullOrWhiteSpace(request.Location))
                return BadRequest(new { message = "Location is required when InterviewMode is not 'remote'." });

            if (request.SalaryMin < 0 || request.SalaryMax < 0)
                return BadRequest(new { message = "Salary cannot be negative." });

            if (request.SalaryMin.HasValue && request.SalaryMax.HasValue && request.SalaryMax < request.SalaryMin)
                return BadRequest(new { message = "SalaryMax cannot be less than SalaryMin." });

            if (request.SalaryIsNegotiable && (request.SalaryMin.HasValue || request.SalaryMax.HasValue))
            {
                return BadRequest(new { message = "SalaryMin and SalaryMax must be null when SalaryIsNegotiable is true." });
            }

            var allowedCategories = new[] { "backend", "frontend", "devops", "qa", "data", "ai_ml", "mobile", "pm", "designer", "other" };
            if (!string.IsNullOrWhiteSpace(request.JobCategory) && !allowedCategories.Contains(request.JobCategory.ToLower()))
                return BadRequest(new { message = $"JobCategory is invalid. Must be one of: {string.Join(", ", allowedCategories)}" });

            // Chỉ check deadline trong tương lai nếu deadline bị thay đổi
            if (request.ApplicationDeadline.HasValue &&
                request.ApplicationDeadline.Value <= DateTimeOffset.UtcNow &&
                request.ApplicationDeadline.Value != job.ApplicationDeadline)
            {
                return BadRequest(new { message = "ApplicationDeadline must be in the future." });
            }

            if (request.RescheduleDeadlineHours < 0)
                return BadRequest(new { message = "RescheduleDeadlineHours cannot be negative." });

            if (request.InviteTokenTtlHours <= 0)
                return BadRequest(new { message = "InviteTokenTtlHours must be greater than 0." });

            if (request.RoundConfigs == null || request.RoundConfigs.Count == 0)
                return BadRequest(new { message = "At least one interview round configuration is required." });

            foreach (var round in request.RoundConfigs)
            {
                if (round.RoundNumber <= 0) return BadRequest(new { message = "RoundNumber must be > 0." });
                if (round.MaxDurationMinutes <= 0) return BadRequest(new { message = "MaxDurationMinutes must be > 0." });
                if (round.InterviewCodeTtlHours <= 0) return BadRequest(new { message = "InterviewCodeTtlHours must be > 0." });
            }

            var detectedLang = JobDescriptionLanguageDetector.Detect(request.JobDescription);

            // 4. Update fields
            job.Title = request.Title.Trim();
            job.Department = request.Department?.Trim();
            job.JobDescription = request.JobDescription.Trim();
            // Chỉ ghi đè file JD khi request có gửi (tránh xoá file cũ khi edit không đổi JD)
            if (!string.IsNullOrWhiteSpace(request.JdFileUrl))
            {
                job.JdFileUrl = request.JdFileUrl;
                job.JdFileName = request.JdFileName;
                job.JdFileFormat = request.JdFileFormat;
            }
            job.InterviewMode = request.InterviewMode;
            job.IsPublicListing = request.IsPublicListing;
            job.DetectedLanguage = detectedLang;
            job.LanguageRequirement = !string.IsNullOrWhiteSpace(request.LanguageRequirement)
                                        ? request.LanguageRequirement
                                        : (detectedLang == "vi" ? "Tiếng Việt" : "Yêu cầu ngôn ngữ " + detectedLang);
            job.RescheduleDeadlineHours = request.RescheduleDeadlineHours;
            job.InviteTokenTtlHours = request.InviteTokenTtlHours;
            job.ScoringRubric = request.ScoringRubric.HasValue ? request.ScoringRubric.Value.GetRawText() : null;
            job.PersonaName = request.PersonaName;
            job.PersonaVoiceId = request.PersonaVoiceId;
            job.PersonaStyle = request.PersonaStyle;
            job.Location = request.Location;
            job.WorkMode = request.WorkMode;
            job.SalaryMin = request.SalaryMin;
            job.SalaryMax = request.SalaryMax;
            job.SalaryCurrency = string.IsNullOrWhiteSpace(request.SalaryCurrency) ? "VND" : request.SalaryCurrency;
            job.SalaryIsNegotiable = request.SalaryIsNegotiable;
            job.EmploymentType = request.EmploymentType;
            job.ExperienceLevel = request.ExperienceLevel;
            job.Skills = request.Skills ?? new List<string>();
            job.JobCategory = request.JobCategory?.ToLower();
            job.ApplicationDeadline = request.ApplicationDeadline;
            job.IsUrgent = request.IsUrgent;
            job.Vacancies = request.Vacancies.HasValue && request.Vacancies.Value > 0 ? request.Vacancies : null;
            job.UpdatedAt = DateTimeOffset.UtcNow; // Ghi nhận thời gian update nếu entity hỗ trợ

            _unitOfWork.Repository<JobPosting>().Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            // 5. Re-create InterviewRoundConfig nếu có sự thay đổi
            var existingRounds = await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == id, ct);
            var existingList = existingRounds.OrderBy(r => r.RoundNumber).ToList();
            var requestList = request.RoundConfigs.OrderBy(r => r.RoundNumber).ToList();

            bool roundsChanged = existingList.Count != requestList.Count;
            if (!roundsChanged)
            {
                for (int i = 0; i < existingList.Count; i++)
                {
                    var ext = existingList[i];
                    var req = requestList[i];
                    if (ext.RoundNumber != req.RoundNumber ||
                        ext.RoundType != req.RoundType ||
                        ext.InterviewLanguage != (req.InterviewLanguage ?? detectedLang) ||
                        ext.InterviewCodeTtlHours != req.InterviewCodeTtlHours ||
                        ext.MaxDurationMinutes != req.MaxDurationMinutes)
                    {
                        roundsChanged = true;
                        break;
                    }
                }
            }

            var finalRoundDtos = new List<RoundConfigDto>();
            if (roundsChanged)
            {
                // Delete các config cũ bằng cách lặp qua từng phần tử
                foreach (var round in existingRounds)
                {
                    _unitOfWork.Repository<InterviewRoundConfig>().Delete(round);
                }
                await _unitOfWork.SaveChangesAsync(ct);

                // Add các config mới
                foreach (var round in request.RoundConfigs.OrderBy(r => r.RoundNumber))
                {
                    var config = new InterviewRoundConfig
                    {
                        JobPostingId = job.Id,
                        RoundNumber = round.RoundNumber,
                        RoundType = round.RoundType,
                        InterviewLanguage = round.InterviewLanguage ?? detectedLang,
                        InterviewCodeTtlHours = round.InterviewCodeTtlHours,
                        MaxDurationMinutes = round.MaxDurationMinutes
                    };
                    await _unitOfWork.Repository<InterviewRoundConfig>().AddAsync(config, ct);
                    finalRoundDtos.Add(RoundConfigDto.FromEntity(config));
                }
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else
            {
                finalRoundDtos = existingList.Select(RoundConfigDto.FromEntity).ToList();
            }

            // Gửi thông báo SignalR cho Recruiter vừa update job (nếu có)
            await _notificationService.PublishUserEventAsync(userId, "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = job.Status, Title = job.Title }, ct);

            // Nếu Job đang active (đang hiển thị công khai), thì thông báo cho tất cả ứng viên để cập nhật Job Board
            if (job.Status == "active")
            {
                await _notificationService.PublishAllEventAsync("ReceivePublicJobUpdate", new { JobId = job.Id, Status = job.Status }, ct);
            }

            return Ok(JobPostingResponse.FromEntity(job, finalRoundDtos));
        }

        /// <summary>
        /// HTTP DELETE /api/jobs/{id}
        /// HR thực hiện xóa mềm (soft delete) tin tuyển dụng.
        /// Không cho phép xóa nếu có hồ sơ ứng tuyển (Application) đang hoạt động.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> DeleteJob(Guid id, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(id, ct);
            if (job == null)
                return NotFound(new { message = "Không tìm thấy tin tuyển dụng." });

            // 1. Validate ownership & roles
            var isAuthorized = _currentUserService.Role == AppRoles.SuperAdmin ||
                               _currentUserService.Role == AppRoles.HrAdmin ||
                               job.CreatedByUserId == userId;
            if (!isAuthorized)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Bạn không có quyền xóa tin tuyển dụng này." });
            }

            // 2. Validate theo Spec: Loại trừ hồ sơ đã fail ("not_pass"), đã rút ("withdrawn") và đã pass hoàn toàn ("pass")
            var activeApps = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(
                a => a.JobPostingId == id && a.Status != "not_pass" && a.Status != "withdrawn" && a.Status != "pass",
                ct);

            if (activeApps.Any())
            {
                return BadRequest(new
                {
                    message = "Cannot delete job with active applications. Không thể xóa tin tuyển dụng này vì đang có hồ sơ ứng tuyển đang hoạt động."
                });
            }

            // 3. Thực hiện XÓA MỀM (Soft Delete) theo đúng Spec thiết kế
            job.DeletedAt = DateTimeOffset.UtcNow;
            job.Status = "archived"; // Đồng bộ chuyển trạng thái thành lưu trữ

            _unitOfWork.Repository<JobPosting>().Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            // Gửi thông báo SignalR cho Recruiter vừa xóa job
            await _notificationService.PublishUserEventAsync(userId, "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = job.Status, Title = job.Title }, ct);

            // Thông báo cập nhật danh sách Job công khai cho Candidates
            await _notificationService.PublishAllEventAsync("ReceivePublicJobUpdate", new { JobId = job.Id, Status = "archived" }, ct);

            return Ok(new { message = "Job posting soft-deleted successfully.", jobId = id });
        }

        /// <summary>
        /// Cập nhật trạng thái Job dựa trên quy trình duyệt (Approval Workflow).
        /// </summary>
        /// <remarks>
        /// <b>Quy tắc chuyển trạng thái:</b>
        /// <br/>• <b>Recruiter (Người tạo):</b> Chỉ chuyển: <c>draft</c> → <c>pending</c> (Gửi duyệt), hoặc bài đang <c>active</c> → <c>closed</c> (Đóng).
        /// <br/>• <b>HrAdmin / SuperAdmin:</b> Được duyệt <c>pending</c> → <c>active</c> (Mở tin), từ chối → <c>rejected</c>, hoặc lưu trữ → <c>archived</c>.
        /// <br/>• <b>Nộp lại bài:</b> Bài bị từ chối (<c>rejected</c>) sửa xong chuyển lại thành <c>pending</c> để duyệt lại.
        /// <br/><br/>
        /// <i>Lưu ý:</i> Bắt buộc truyền <c>RejectionReason</c> khi từ chối bài viết (<c>status = "rejected"</c>).
        /// </remarks>
        [HttpPatch("{id:guid}/status")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] UpdateJobStatusRequest request, CancellationToken ct)
        {
            // 1. Xác thực người dùng
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            if (string.IsNullOrWhiteSpace(request.Status))
                return BadRequest(new { message = "Status is required." });

            var targetStatus = request.Status.Trim().ToLowerInvariant();
            var allowedStatuses = new[] { "draft", "pending", "active", "rejected", "closed", "archived" };

            if (!allowedStatuses.Contains(targetStatus))
                return BadRequest(new { message = $"Trạng thái không hợp lệ. Sử dụng một trong: {string.Join(", ", allowedStatuses)}." });

            if (targetStatus == "draft")
                return BadRequest(new { message = "Không thể chuyển trạng thái về 'draft'. 'draft' chỉ dùng khi tạo hoặc chỉnh sửa nháp ban đầu." });

            // 2. Lấy thông tin Job tuyển dụng
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(id, ct);
            if (job == null)
                return NotFound(new { message = "Job posting not found." });

            var currentStatus = job.Status?.Trim().ToLowerInvariant();

            // 3. Phân quyền kiểm tra cơ bản
            var isSuperOrHrAdmin = _currentUserService.Role == AppRoles.SuperAdmin || _currentUserService.Role == AppRoles.HrAdmin;
            var isOwner = job.CreatedByUserId == userId;

            if (!isSuperOrHrAdmin && !isOwner)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Bạn không có quyền thay đổi trạng thái tin tuyển dụng này." });
            }

            // Nếu trạng thái không thay đổi, không cần xử lý tiếp
            if (currentStatus == targetStatus)
            {
                return BadRequest(new { message = $"Tin tuyển dụng hiện tại đã ở trạng thái '{targetStatus}' rồi." });
            }

            // Chặn mọi hành động nếu Job đã bị lưu trữ (archived)
            if (currentStatus == "archived")
            {
                return BadRequest(new { message = "Không thể thay đổi trạng thái của tin tuyển dụng đã lưu trữ (archived)." });
            }

            // 4. --- VALIDATE STATE TRANSITIONS & ROLES WORKFLOW ---

            // CASE A: Hành động từ chối (Chuyển sang 'rejected') -> Bắt buộc phải là Admin và phải có lý do
            if (targetStatus == "rejected")
            {
                if (!isSuperOrHrAdmin)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chỉ HrAdmin hoặc SuperAdmin mới có quyền từ chối duyệt bài." });

                if (currentStatus != "pending")
                    return BadRequest(new { message = "Chỉ có thể từ chối (rejected) những bài viết đang ở trạng thái chờ duyệt (pending)." });

                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                    return BadRequest(new { message = "Vui lòng cung cấp lý do từ chối duyệt bài (RejectionReason)." });

                job.RejectionReason = request.RejectionReason.Trim();

                // Thông báo kết quả TỪ CHỐI về người tạo tin (thường là Recruiter).
                await AddJobDecisionNotificationAsync(job, userId, reviewerName: null, approved: false, reason: job.RejectionReason, ct);
            }

            // CASE B: Hành động Phê duyệt public bài (Chuyển sang 'active') -> Chỉ Admin mới được duyệt
            if (targetStatus == "active")
            {
                if (!isSuperOrHrAdmin)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chỉ HrAdmin hoặc SuperAdmin mới có quyền kích hoạt/phê duyệt bài viết." });

                // Admin có thể kích hoạt từ bài pending, hoặc tái kích hoạt lại bài đã closed
                if (currentStatus != "pending" && currentStatus != "closed")
                    return BadRequest(new { message = "Chỉ có thể kích hoạt (active) từ trạng thái chờ duyệt (pending) hoặc đã đóng (closed)." });

                // Kiểm tra xem hạn nộp đã hết chưa, tránh trường hợp bài hết hạn mà kích hoạt lại bừa bãi
                if (job.ApplicationDeadline.HasValue && job.ApplicationDeadline.Value <= DateTimeOffset.UtcNow)
                {
                    return BadRequest(new { message = "Hạn nộp hồ sơ của Job này đã ở quá khứ. Hãy cập nhật lại gia hạn Deadline trước khi chuyển sang Active." });
                }

                // Điền ngày public đầu tiên nếu chưa có
                if (job.PublishedAt == null) job.PublishedAt = DateTimeOffset.UtcNow;

                // Duyệt thành công thì xóa bỏ vết lý do từ chối cũ (nếu có trước đó)
                job.RejectionReason = null;

                // Ghi nhận phê duyệt + đóng dấu duyệt lên file JD — chỉ khi duyệt từ trạng thái
                // chờ duyệt (pending → active), không áp dụng khi tái kích hoạt bài đã closed.
                if (currentStatus == "pending")
                {
                    var approver = await _unitOfWork.Repository<User>().GetByIdAsync(userId, ct);
                    var approverName = approver != null
                        ? (string.IsNullOrWhiteSpace(approver.FullName) ? approver.Email : approver.FullName)
                        : "HR Leader";

                    job.ApprovedByUserId = userId;
                    job.ApprovedAt = DateTimeOffset.UtcNow;
                    job.ApproverName = approverName;

                    // Thông báo kết quả ĐƯỢC DUYỆT về người tạo tin (thường là Recruiter).
                    await AddJobDecisionNotificationAsync(job, userId, approverName, approved: true, reason: null, ct);

                    // Đóng dấu duyệt lên file JD. PDF: vẽ dấu lên file gốc. DOCX: render nội dung JD
                    // thành PDF mới rồi đóng dấu (mất định dạng gốc nhưng giữ nội dung + bằng chứng duyệt).
                    // Thất bại khi đóng dấu KHÔNG được chặn việc duyệt tin.
                    var fmt = (job.JdFileFormat ?? string.Empty).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(job.JdFileUrl) && (fmt == "pdf" || fmt == "docx"))
                    {
                        try
                        {
                            byte[]? stamped = null;
                            var original = await _fileStorage.ReadAllBytesAsync(job.JdFileUrl, ct);

                            if (fmt == "pdf")
                            {
                                if (original != null && original.Length > 0)
                                    stamped = await _jdStampService.StampApprovalAsync(original, approverName, job.ApprovedAt.Value, ct);
                            }
                            else // docx
                            {
                                var bodyText = job.JobDescription ?? string.Empty;
                                if (original != null && original.Length > 0)
                                {
                                    try
                                    {
                                        using var ms = new MemoryStream(original);
                                        var parsed = (await _documentParser.ParseDocumentAsync(ms, ".docx"))?.Replace("\0", string.Empty);
                                        if (!string.IsNullOrWhiteSpace(parsed)) bodyText = parsed;
                                    }
                                    catch (Exception exParse)
                                    {
                                        _logger.LogWarning(exParse, "Parse DOCX để đóng dấu thất bại, dùng JobDescription. Job {JobId}", job.Id);
                                    }
                                }
                                stamped = await _jdStampService.StampApprovalFromTextAsync(job.Title, bodyText, approverName, job.ApprovedAt.Value, ct);
                            }

                            if (stamped != null && stamped.Length > 0)
                            {
                                // File đã đóng dấu luôn là PDF, kể cả khi gốc là DOCX.
                                var baseName = Path.GetFileNameWithoutExtension(job.JdFileName ?? "JD") + ".pdf";
                                var signedName = AppendSuffix(baseName, "-da-duyet");
                                job.SignedJdFileUrl = await _fileStorage.SaveAsync(stamped, signedName, "application/pdf", ct);
                            }
                        }
                        catch (Exception exStamp)
                        {
                            _logger.LogWarning(exStamp, "Đóng dấu duyệt JD thất bại cho job {JobId} — vẫn duyệt tin.", job.Id);
                        }
                    }
                }
            }

            // CASE C: Hành động Gửi duyệt bài (Chuyển sang 'pending') -> Thường dành cho Recruiter/Owner nộp bài
            if (targetStatus == "pending")
            {
                if (!isOwner)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chỉ Recruiter/Owner mới có quyền gửi duyệt bài." });

                if (currentStatus != "draft" && currentStatus != "rejected")
                    return BadRequest(new { message = "Chỉ có thể gửi duyệt (pending) khi bài viết đang là bản nháp (draft) hoặc bị từ chối (rejected)." });

                // Khi sửa xong nộp lại, xóa tạm lý do từ chối cũ để chờ kết quả mới
                job.RejectionReason = null;
            }

            // CASE D: Đóng bài tuyển dụng (Chuyển sang 'closed') -> Đủ người hoặc hết hạn bài
            if (targetStatus == "closed")
            {
                if (!isOwner && !isSuperOrHrAdmin)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chỉ Recruiter/Owner hoặc HrAdmin/SuperAdmin mới có quyền đóng bài." });

                if (currentStatus != "active")
                    return BadRequest(new { message = "Chỉ có thể đóng (closed) một tin tuyển dụng đang hoạt động (active)." });
            }

            // CASE E: Lưu trữ / Xóa mềm bài tuyển dụng (Chuyển sang 'archived')
            if (targetStatus == "archived")
            {
                // Kiểm tra xem có hồ sơ ứng tuyển nào đang xử lý dở dang không
                var activeApps = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(
                    a => a.JobPostingId == id && a.Status != "not_pass" && a.Status != "withdrawn" && a.Status != "pass",
                    ct);

                if (activeApps.Any())
                {
                    return BadRequest(new { message = "Không thể chuyển tin tuyển dụng sang lưu trữ (archived) khi đang có hồ sơ ứng tuyển đang hoạt động." });
                }

                job.DeletedAt = DateTimeOffset.UtcNow;
            }

            // 5. Đồng bộ cập nhật vào database
            job.Status = targetStatus;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<JobPosting>().Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            // Lấy lại danh sách rounds trả về cho đồng bộ cấu trúc Response
            var rds = await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == id, ct);
            var roundDtos = rds.OrderBy(r => r.RoundNumber).Select(RoundConfigDto.FromEntity).ToList();

            var statusResponse = JobPostingResponse.FromEntity(job, roundDtos);
            // Resolve storageKey -> URL dùng được cho file JD gốc + bản đã đóng dấu (người gọi là staff).
            if (!string.IsNullOrEmpty(statusResponse.JdFileUrl))
                statusResponse.JdFileUrl = await _fileStorage.GetUrlAsync(statusResponse.JdFileUrl, ct);
            if (!string.IsNullOrEmpty(statusResponse.SignedJdFileUrl))
                statusResponse.SignedJdFileUrl = await _fileStorage.GetUrlAsync(statusResponse.SignedJdFileUrl, ct);

            // Gửi thông báo SignalR tương ứng
            if (targetStatus == "pending")
            {
                // Thông báo cho HR Admin biết có tin chờ duyệt
                await _notificationService.PublishGroupEventAsync("hr_admin", "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = "pending", Title = job.Title }, ct);
            }
            else if (targetStatus == "active" || targetStatus == "rejected")
            {
                // Thông báo cho người tạo tin (Recruiter) biết kết quả duyệt
                await _notificationService.PublishUserEventAsync(job.CreatedByUserId, "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = targetStatus, Title = job.Title }, ct);
            }

            // Nếu trạng thái ảnh hưởng đến trang Candidate Public Job Board (active, closed, archived) thì broadcast
            if (targetStatus == "active" || targetStatus == "closed" || targetStatus == "archived")
            {
                await _notificationService.PublishAllEventAsync("ReceivePublicJobUpdate", new { JobId = job.Id, Status = targetStatus }, ct);
            }

            return Ok(statusResponse);
        }

        /// <summary>
        /// Gửi thông báo kết quả duyệt/từ chối tin về cho NGƯỜI TẠO tin (thường là Recruiter).
        /// Bỏ qua nếu người duyệt cũng chính là người tạo. Chỉ stage qua AddAsync — được persist
        /// CÙNG transaction với cập nhật trạng thái tin tại <see cref="UpdateJobStatus"/> (1 SaveChanges).
        /// </summary>
        private async Task AddJobDecisionNotificationAsync(
            JobPosting job, Guid actorUserId, string? reviewerName, bool approved, string? reason, CancellationToken ct)
        {
            if (job.CreatedByUserId == actorUserId) return; // người duyệt cũng là người tạo → không tự thông báo

            var creator = await _unitOfWork.Repository<User>().GetByIdAsync(job.CreatedByUserId, ct);
            if (creator == null) return;

            if (string.IsNullOrWhiteSpace(reviewerName))
            {
                var actor = await _unitOfWork.Repository<User>().GetByIdAsync(actorUserId, ct);
                reviewerName = actor != null
                    ? (string.IsNullOrWhiteSpace(actor.FullName) ? actor.Email : actor.FullName)
                    : "HR Admin";
            }

            // Link tới trang chi tiết tin theo workspace của người tạo.
            var isRecruiter = string.Equals(creator.Role, "recruiter", StringComparison.OrdinalIgnoreCase);
            var link = isRecruiter ? $"/recruiter/my-jobs/{job.Id}" : $"/hr/jobs/{job.Id}";
            var now = DateTimeOffset.UtcNow;

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = job.CreatedByUserId,
                // Ticks ở khóa chống trùng → mỗi lần duyệt/từ chối là một sự kiện riêng, hỗ trợ nhiều vòng nộp lại.
                DedupKey = $"{(approved ? "job_approved" : "job_rejected")}:{job.Id}:{now.Ticks}",
                Type = approved ? "approved" : "rejected",
                Title = approved ? "Tin tuyển dụng đã được duyệt" : "Tin tuyển dụng bị từ chối",
                Body = approved
                    ? $"\"{job.Title}\" đã được {reviewerName} phê duyệt và đăng công khai."
                    : $"\"{job.Title}\" bị {reviewerName} từ chối. Lý do: {reason}",
                Link = link,
                CreatedAt = now,
                UpdatedAt = now,
            }, ct);
        }

        /// <summary>Chèn hậu tố vào tên file trước phần mở rộng. VD: "JD.pdf" + "-da-duyet" => "JD-da-duyet.pdf".</summary>
        private static string AppendSuffix(string fileName, string suffix)
        {
            var ext = Path.GetExtension(fileName);
            var name = Path.GetFileNameWithoutExtension(fileName);
            return $"{name}{suffix}{ext}";
        }

        /// <summary>
        /// Upload file JD (PDF/DOCX), phân tích bằng Gemini để trích xuất các trường auto-fill cho form tạo tin.
        /// File JD được lưu trữ; storageKey + metadata trả về để đính kèm khi submit job. (ADR-042)
        /// </summary>
        [HttpPost("analyze-jd")]
        [Authorize(Policy = "InternalStaff")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AnalyzeJd(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File JD không được để trống." });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "Kích thước file JD không được vượt quá 10MB." });

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowed = new[] { ".pdf", ".docx" };
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(allowed, ext) < 0)
                return BadRequest(new { message = "Định dạng không hợp lệ. Chỉ chấp nhận .pdf hoặc .docx" });

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            // Parse text (dùng làm fallback cho Gemini, nhất là với DOCX không gửi inline được)
            string jdText;
            try
            {
                using var stream = new MemoryStream(bytes);
                jdText = (await _documentParser.ParseDocumentAsync(stream, ext))?.Replace("\0", string.Empty) ?? string.Empty;
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Không thể đọc nội dung file JD: {ex.Message}" });
            }

            var contentType = ext == ".pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            // Lưu file JD qua abstraction (Local dev / R2 prod). DB lưu storageKey.
            string storageKey;
            try
            {
                storageKey = await _fileStorage.SaveAsync(bytes, file.FileName, contentType, ct);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Không thể lưu file JD: {ex.Message}" });
            }

            // Gọi Gemini trích xuất (PDF gửi inline, DOCX dùng fallback text)
            var pdfBytes = ext == ".pdf" ? bytes : null;
            var extraction = await _geminiProvider.ExtractJobFromJdAsync(pdfBytes, ext == ".pdf" ? "application/pdf" : null, jdText, ct);

            if (extraction.IsFailure)
            {
                // Vẫn trả file đã lưu để người dùng tạo tin thủ công, kèm cảnh báo phân tích thất bại.
                return Ok(new AnalyzeJdResponse
                {
                    IsValidJd = false,
                    JdFileUrl = storageKey,
                    JdFileName = file.FileName,
                    JdFileFormat = ext.TrimStart('.'),
                    JobDescription = string.IsNullOrWhiteSpace(jdText) ? null : jdText
                });
            }

            var data = extraction.Value;
            var response = new AnalyzeJdResponse
            {
                IsValidJd = data.IsValidJd,
                JdFileUrl = storageKey,
                JdFileName = file.FileName,
                JdFileFormat = ext.TrimStart('.'),
                Title = data.Title,
                Department = data.Department,
                JobDescription = !string.IsNullOrWhiteSpace(data.JobDescription) ? data.JobDescription : (string.IsNullOrWhiteSpace(jdText) ? null : jdText),
                JobCategory = data.JobCategory,
                ExperienceLevel = data.ExperienceLevel,
                EmploymentType = data.EmploymentType,
                WorkMode = data.WorkMode,
                Location = data.Location,
                Skills = data.Skills ?? new List<string>(),
                LanguageRequirement = data.LanguageRequirement,
                SalaryMin = data.SalaryMin,
                SalaryMax = data.SalaryMax
            };

            return Ok(response);
        }

        /// <summary>
        /// Danh sách ứng viên (Application) của MỘT job. Recruiter chỉ xem được job mình tạo;
        /// HrAdmin/SuperAdmin xem được mọi job. Phục vụ màn "kiểm soát ứng viên theo job".
        /// </summary>
        [HttpGet("{id:guid}/applications")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetJobApplications(Guid id, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng." });

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(id, ct);
            if (job == null)
                return NotFound(new { message = "Không tìm thấy tin tuyển dụng." });

            var isAdmin = _currentUserService.Role == AppRoles.SuperAdmin || _currentUserService.Role == AppRoles.HrAdmin;
            if (!isAdmin && job.CreatedByUserId != userId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền xem ứng viên của tin tuyển dụng này." });

            var result = await _applicationService.GetApplicationsByJobAsync(id, ct);
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            // Resolve storageKey -> URL client dùng được (local: relative, R2: presigned)
            foreach (var app in result.Value!)
            {
                if (!string.IsNullOrEmpty(app.CvFileUrl))
                    app.CvFileUrl = await _fileStorage.GetUrlAsync(app.CvFileUrl, ct);
            }

            return Ok(result.Value);
        }
    }
}
