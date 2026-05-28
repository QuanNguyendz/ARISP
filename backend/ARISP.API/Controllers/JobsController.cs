using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAIProvider _aiProvider;

        public JobsController(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IAIProvider aiProvider)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _aiProvider = aiProvider;
        }

        [HttpPost]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> CreateJob([FromBody] CreateJobPostingRequest request)
        {
            var orgId = _currentUserService.OrganizationId ?? Guid.Empty;
            var userId = _currentUserService.UserId ?? Guid.Empty;

            if (orgId == Guid.Empty)
                return Unauthorized(new { message = "Organization scope missing." });

            // Auto detect language requirement from JD
            var detectedLang = await _aiProvider.DetectLanguageRequirementAsync(request.JobDescription, default);

            var job = new JobPosting
            {
                OrganizationId = orgId,
                CreatedByUserId = userId,
                Title = request.Title,
                Department = request.Department,
                JobDescription = request.JobDescription,
                InterviewMode = request.InterviewMode,
                Status = "draft",
                IsPublicListing = request.IsPublicListing,
                DetectedLanguage = detectedLang,
                LanguageRequirement = detectedLang == "vi" ? "Tiếng Việt" : "Yêu cầu ngôn ngữ " + detectedLang,
                ScoringRubric = request.ScoringRubric,
                PersonaName = request.PersonaName,
                PersonaVoiceId = request.PersonaVoiceId,
                PersonaStyle = request.PersonaStyle
            };

            await _unitOfWork.Repository<JobPosting>().AddAsync(job);
            await _unitOfWork.SaveChangesAsync();

            // Save multi-round configuration
            foreach (var round in request.RoundConfigs)
            {
                var config = new InterviewRoundConfig
                {
                    JobPostingId = job.Id,
                    RoundNumber = round.RoundNumber,
                    RoundType = round.RoundType,
                    InterviewLanguage = round.InterviewLanguage,
                    InterviewCodeTtlHours = round.InterviewCodeTtlHours,
                    MaxDurationMinutes = round.MaxDurationMinutes
                };
                await _unitOfWork.Repository<InterviewRoundConfig>().AddAsync(config);
            }
            await _unitOfWork.SaveChangesAsync();

            return Ok(job);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobs()
        {
            var jobs = await _unitOfWork.Repository<JobPosting>().GetAllAsync();
            var response = jobs.Select(j => new JobPostingResponse
            {
                Id = j.Id,
                Title = j.Title,
                Department = j.Department,
                JobDescription = j.JobDescription,
                InterviewMode = j.InterviewMode,
                Status = j.Status,
                IsPublicListing = j.IsPublicListing,
                DetectedLanguage = j.DetectedLanguage,
                LanguageRequirement = j.LanguageRequirement,
                LanguageConfirmed = j.LanguageConfirmed,
                CreatedAt = j.CreatedAt
            });

            return Ok(response);
        }

        [HttpPost("{id}/slots")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> AddAvailabilitySlots(Guid id, [FromBody] List<AvailabilitySlot> slots)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(id);
            if (job == null)
                return NotFound(new { message = "Job posting not found." });

            foreach (var slot in slots)
            {
                slot.JobPostingId = job.Id;
                await _unitOfWork.Repository<AvailabilitySlot>().AddAsync(slot);
            }
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "Availability slots configured successfully." });
        }
    }
}
