using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using ARISP.Application.Common;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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

        public Guid? CandidateAccountId { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly ApplicationService _applicationService;
        private readonly IDocumentParserService _documentParserService;
        private readonly IFileStorageService _fileStorage;
        private readonly ICurrentUserService _currentUserService;
        private readonly IConfiguration _configuration;

        public ApplicationsController(ApplicationService applicationService, IDocumentParserService documentParserService, IFileStorageService fileStorage, ICurrentUserService currentUserService, IConfiguration configuration)
        {
            _applicationService = applicationService;
            _documentParserService = documentParserService;
            _fileStorage = fileStorage;
            _currentUserService = currentUserService;
            _configuration = configuration;
        }

        /// <summary>Base URL portal ứng viên (theo môi trường), fallback AdminFrontendUrl rồi localhost.</summary>
        private string CandidateBaseUrl =>
            _configuration["Frontend:CandidateBaseUrl"]
            ?? _configuration["Authentication:AdminFrontendUrl"]
            ?? "http://localhost:3000";

        [HttpGet("{id}")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetApplicationById(Guid id, CancellationToken ct)
        {
            var result = await _applicationService.GetApplicationByIdAsync(id, ct);
            if (result.IsFailure)
            {
                return NotFound(new { message = result.Error });
            }

            if (!string.IsNullOrEmpty(result.Value!.CvFileUrl))
                result.Value.CvFileUrl = await _fileStorage.GetUrlAsync(result.Value.CvFileUrl, ct);

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

            // Đọc toàn bộ nội dung file 1 lần vào memory để vừa hash, parse, vừa lưu storage.
            byte[] cvBytes;
            using (var ms = new MemoryStream())
            {
                await request.CvFile.CopyToAsync(ms);
                cvBytes = ms.ToArray();
            }

            // Compute CV Hash for Match Analysis Cache
            string cvFileHash;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hashBytes = md5.ComputeHash(cvBytes);
                cvFileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            // Extract CV text using the real document parser service (parse trước khi tốn công lưu).
            string cvText;
            try
            {
                using (var stream = new MemoryStream(cvBytes))
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
                return BadRequest(new { message = $"Không thể phân tích file CV: {ex.Message}" });
            }

            // Lưu file qua abstraction (Local cho dev / S3-compatible cho prod). DB lưu storageKey.
            var contentType = extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "text/plain"
            };
            string cvFileUrl;
            try
            {
                cvFileUrl = await _fileStorage.SaveAsync(cvBytes, request.CvFile.FileName, contentType);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Không thể lưu file CV: {ex.Message}" });
            }

            Guid? candidateAccountId = request.CandidateAccountId;
            if (candidateAccountId == null && User.Identity?.IsAuthenticated == true)
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
                CvText = cvText,
                CvFileHash = cvFileHash
            };

            var result = await _applicationService.SubmitApplicationAsync(serviceRequest, "job_board");
            if (result.IsFailure)
            {
                // Clean up file if db write fails
                await _fileStorage.DeleteAsync(cvFileUrl);
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        /// <summary>
        /// Danh sách hồ sơ ứng tuyển. <paramref name="mine"/>=true: chỉ ứng viên thuộc các tin do
        /// người đang đăng nhập tạo (Recruiter workspace). Mặc định: toàn bộ (HR/SA).
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetApplications([FromQuery] bool mine, CancellationToken ct)
        {
            Result<List<ApplicationResponse>> result;
            if (mine)
            {
                if (_currentUserService.UserId is not { } uid || uid == Guid.Empty)
                    return Unauthorized(new { message = "Không xác định được người dùng." });
                result = await _applicationService.GetApplicationsForCreatorAsync(uid, ct);
            }
            else
            {
                result = await _applicationService.GetAllApplicationsAsync(ct);
            }

            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            // Resolve storageKey -> URL client dùng được
            foreach (var app in result.Value!)
            {
                if (!string.IsNullOrEmpty(app.CvFileUrl))
                    app.CvFileUrl = await _fileStorage.GetUrlAsync(app.CvFileUrl, ct);
            }

            return Ok(result.Value);
        }

        [HttpGet("practice-eligibility/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPracticeEligibility(Guid id, [FromQuery] int round, CancellationToken ct)
        {
            var roundNumber = round > 0 ? round : 1;
            var result = await _applicationService.CheckPracticeEligibilityAsync(id, roundNumber, ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { eligible = result.Value });
        }

        [HttpPost("{id}/send-invite")]
        [Authorize(Policy = "InternalStaff")] // Chỉ HR / Staff mới có quyền bấm gửi link mời
        public async Task<IActionResult> SendInvite(Guid id, [FromQuery] int round, CancellationToken ct)
        {
            var roundNumber = round > 0 ? round : 1;
            var result = await _applicationService.SendInterviewInviteAsync(id, CandidateBaseUrl, roundNumber, ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { message = "Đã gửi email mời phỏng vấn (chọn lịch) cho ứng viên." });
        }

    }
}