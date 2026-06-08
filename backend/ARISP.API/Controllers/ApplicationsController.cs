using System;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ARISP.Application.DTOs;
using ARISP.Application.Services;

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

        public ApplicationsController(ApplicationService applicationService)
        {
            _applicationService = applicationService;
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

            // Extract or simulate CV text using parser stub
            var cvText = await ParseCvFileAsync(request.CvFile, request.CandidateName, request.CandidateEmail, request.CandidatePhone, request.JobPostingId);

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

        [HttpGet("{id}/practice-eligibility")]
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

        private async Task<string> ParseCvFileAsync(IFormFile cvFile, string name, string email, string? phone, Guid jobId)
        {
            var extension = Path.GetExtension(cvFile.FileName)?.ToLower();
            if (extension == ".txt")
            {
                using var reader = new StreamReader(cvFile.OpenReadStream());
                return await reader.ReadToEndAsync();
            }

            // Return simulated parsed text for PDF / DOCX files
            return $"--- TRÍCH XUẤT THÔNG TIN CV (STUB) ---\n" +
                   $"Họ và tên: {name}\n" +
                   $"Email: {email}\n" +
                   $"Số điện thoại: {phone ?? "Không có"}\n" +
                   $"Mã Job Posting: {jobId}\n" +
                   $"Tên file gốc: {cvFile.FileName}\n" +
                   $"Ngày trích xuất: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss zzz}\n" +
                   $"-------------------------------------\n" +
                   $"TỔNG QUAN NĂNG LỰC:\n" +
                   $"Lập trình viên backend có kinh nghiệm thực tế phát triển các hệ thống API doanh nghiệp bằng công nghệ C# .NET và AI.\n\n" +
                   $"KỸ NĂNG CHUYÊN MÔN:\n" +
                   $"- Ngôn ngữ lập trình: C#, SQL, JavaScript\n" +
                   $"- Frameworks: .NET 8, ASP.NET Core Web API, EF Core, SignalR, WebRTC\n" +
                   $"- Cơ sở dữ liệu: PostgreSQL (Supabase/pgvector), Redis\n" +
                   $"- Công nghệ AI: Tích hợp RAG, OpenAI API (GPT-4o)\n" +
                   $"- Công cụ & Quy trình: Git, Docker, CI/CD Github Actions\n\n" +
                   $"KINH NGHIỆM LÀM VIỆC:\n" +
                   $"Backend Engineer | FPT Software (2024 - Hiện tại)\n" +
                   $"- Tham gia xây dựng các dịch vụ backend phục vụ quản lý nhân sự và phỏng vấn AI.\n" +
                   $"- Thiết kế và tối ưu cơ sở dữ liệu Postgres, tích hợp embedding vector để tìm kiếm ứng viên.\n\n" +
                   $"HỌC VẤN:\n" +
                   $"Đại học Bách Khoa Hà Nội | Chuyên ngành Công nghệ thông tin";
        }
    }
}
