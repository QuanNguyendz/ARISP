using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public JobsController(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
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
                SalaryCurrency = request.SalaryCurrency,
                SalaryIsNegotiable = request.SalaryIsNegotiable,
                EmploymentType = request.EmploymentType,
                ExperienceLevel = request.ExperienceLevel,
                Skills = request.Skills ?? new List<string>(),
                JobCategory = request.JobCategory?.ToLower(),
                ApplicationDeadline = request.ApplicationDeadline,
                IsUrgent = request.IsUrgent
            };

            await _unitOfWork.Repository<JobPosting>().AddAsync(job, ct);
            await _unitOfWork.SaveChangesAsync(ct);

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

            return Ok(JobPostingResponse.FromEntity(job, roundDtos));
        }

        /// <summary>Danh sách job công khai trên Job Board (không cần đăng nhập).</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobs(CancellationToken ct)
        {
            var jobs = await _unitOfWork.Repository<JobPosting>().FindAsync(
                j => j.IsPublicListing && j.Status == "active",
                ct);

            var response = jobs
                .OrderByDescending(j => j.PublishedAt ?? j.CreatedAt)
                .Select(j => JobPostingListItemResponse.FromEntity(j));

            return Ok(response);
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
            return Ok(JobPostingResponse.FromEntity(job, roundDtos));
        }

        /// <summary>Danh sách toàn bộ job dành cho HR (bao gồm cả draft, closed...).</summary>
        [HttpGet("admin")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetAdminJobs(CancellationToken ct)
        {
            var jobs = await _unitOfWork.Repository<JobPosting>().GetAllAsync(ct);

            var response = jobs
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => JobPostingListItemResponse.FromEntity(j));

            return Ok(response);
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
    }
}
