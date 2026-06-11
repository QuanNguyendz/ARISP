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
                SalaryCurrency = string.IsNullOrWhiteSpace(request.SalaryCurrency) ? "VND" : request.SalaryCurrency,
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

            // --- Cập nhật trạng thái (Status) dựa trên Business Logic ---
            if (request.ApplicationDeadline.HasValue && request.ApplicationDeadline.Value > DateTimeOffset.UtcNow)
            {
                // Nếu tin đang bị expired (hết hạn) mà được gia hạn deadline mới -> tự động Active lại
                if (string.Equals(job.Status, "expired", StringComparison.OrdinalIgnoreCase))
                {
                    job.Status = "active";
                }
            }

            // Nếu tin đang là bản nháp (draft), khi HR cập nhật thông tin chuẩn chỉnh -> chuyển thành Active công khai
            if (string.Equals(job.Status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                job.Status = "active"; // Hoặc đổi thành "pending" nếu hệ thống có bước duyệt bài
            }

            // 4. Update fields
            job.Title = request.Title.Trim();
            job.Department = request.Department?.Trim();
            job.JobDescription = request.JobDescription.Trim();
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

            return Ok(new { message = "Job posting soft-deleted successfully.", jobId = id });
        }
    }
}
