using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARISP.API.Controllers
{
    public class SubmitApplicationFormRequest
    {
        [Required(ErrorMessage = "JobPostingId là bắt buộc.")]
        public Guid JobPostingId { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Địa chỉ Email không hợp lệ.")]
        public string CandidateEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên là bắt buộc.")]
        public string CandidateName { get; set; } = string.Empty;

        public string? CandidatePhone { get; set; }

        [Required(ErrorMessage = "File CV là bắt buộc.")]
        public IFormFile CvFile { get; set; } = null!;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly ApplicationService _applicationService;
        private readonly IDocumentParserService _documentParserService;

        public ApplicationsController(ApplicationService applicationService, IDocumentParserService documentParserService)
        {
            _applicationService = applicationService;
            _documentParserService = documentParserService;
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetApplicationById(Guid id, CancellationToken ct)
        {
            var result = await _applicationService.GetApplicationByIdAsync(id, ct);
            if (result.IsFailure)
            {
                return NotFound(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        [HttpPatch("{id}/status")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> UpdateApplicationStatus(Guid id, [FromBody] UpdateApplicationStatusRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _applicationService.UpdateApplicationStatusAsync(id, request.Status, ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitApplication([FromForm] SubmitApplicationFormRequest request)
        {
            if (request.CvFile == null || request.CvFile.Length == 0)
            {
                return BadRequest(new { message = "File CV không được để trống." });
            }

            // Limit file size to 10MB to avoid server overload
            if (request.CvFile.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { message = "Kích thước file CV không được vượt quá 10MB." });
            }

            // Verify safe extension (.pdf, .docx, .txt)
            var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
            var extension = Path.GetExtension(request.CvFile.FileName)?.ToLower();
            if (string.IsNullOrEmpty(extension) || Array.IndexOf(allowedExtensions, extension) < 0)
            {
                return BadRequest(new { message = "Định dạng file không hợp lệ. Chỉ chấp nhận .pdf, .docx, .txt" });
            }

            // Ensure local uploads directory exists
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Generate unique filename to avoid collision
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.CvFile.CopyToAsync(stream);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Không thể lưu file CV: {ex.Message}" });
            }

            var cvFileUrl = $"/uploads/{uniqueFileName}";

            // Extract CV text using the real document parser service
            string cvText;
            try
            {
                using (var stream = request.CvFile.OpenReadStream())
                {
                    cvText = await _documentParserService.ParseDocumentAsync(stream, extension);
                }

                if (!string.IsNullOrEmpty(cvText))
                {
                    cvText = cvText.Replace("\0", string.Empty);
                }
            }
            catch (Exception ex)
            {
                // Clean up file if parsing fails
                if (System.IO.File.Exists(filePath))
                {
                    try { System.IO.File.Delete(filePath); } catch { /* Ignore cleanup error */ }
                }
                return BadRequest(new { message = $"Không thể phân tích file CV: {ex.Message}" });
            }

            Guid? candidateAccountId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var subClaim = User.FindFirst("sub")?.Value;
                if (Guid.TryParse(subClaim, out var parsedId))
                {
                    candidateAccountId = parsedId;
                }
            }

            var serviceRequest = new SubmitApplicationRequest
            {
                JobPostingId = request.JobPostingId,
                CandidateAccountId = candidateAccountId,
                CandidateEmail = request.CandidateEmail,
                CandidateName = request.CandidateName,
                CandidatePhone = request.CandidatePhone,
                CvFileUrl = cvFileUrl,
                CvText = cvText
            };

            var result = await _applicationService.SubmitApplicationAsync(serviceRequest, "job_board");
            if (result.IsFailure)
            {
                // Clean up file if db write fails
                if (System.IO.File.Exists(filePath))
                {
                    try { System.IO.File.Delete(filePath); } catch { /* Ignore cleanup error */ }
                }
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        [HttpGet]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetApplications()
        {
            var result = await _applicationService.GetAllApplicationsAsync();
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        [HttpGet("practice-eligibility/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPracticeEligibility(Guid id)
        {
            var result = await _applicationService.CheckPracticeEligibilityAsync(id);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { eligible = result.Value });
        }

        [HttpPost("{id}/send-invite")]
        [Authorize(Policy = "InternalStaff")] // Chỉ HR / Staff mới có quyền bấm gửi link mời
        public async Task<IActionResult> SendInvite(Guid id, CancellationToken ct)
        {
            var result = await _applicationService.SendInterviewInviteAsync(id, ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { message = "Gửi Magic Link mời phỏng vấn/làm bài test thành công!" });
        }

    }
}