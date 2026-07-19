using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Commands;
using ARI.Application.Applications.Commands.SubmitApplication;
using ARI.Application.Applications.Queries;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
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
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public ApplicationsController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetApplicationById(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetApplicationByIdQuery(id), ct);
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

            var result = await _sender.Send(new UpdateApplicationStatusCommand(id, request.Status), ct);
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

            Guid? candidateAccountId = request.CandidateAccountId;
            if (candidateAccountId == null && User.Identity?.IsAuthenticated == true)
            {
                var subClaim = User.FindFirst("sub")?.Value;
                if (Guid.TryParse(subClaim, out var parsedId))
                {
                    candidateAccountId = parsedId;
                }
            }

            var result = await _sender.Send(new SubmitApplicationCommand(
                request.JobPostingId, candidateAccountId, request.CandidateEmail, request.CandidateName,
                request.CandidatePhone, cvBytes, request.CvFile.FileName, extension));

            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.ServerError
                    ? StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Error })
                    : BadRequest(new { message = result.Error });
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
            Guid? mineUid = null;
            if (mine)
            {
                if (_currentUserService.UserId is not { } uid || uid == Guid.Empty)
                    return Unauthorized(new { message = "Không xác định được người dùng." });
                mineUid = uid;
            }

            var result = await _sender.Send(new GetApplicationsQuery(mineUid), ct);
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(result.Value);
        }

        [HttpGet("practice-eligibility/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPracticeEligibility(Guid id, [FromQuery] int round, CancellationToken ct)
        {
            var roundNumber = round > 0 ? round : 1;
            var result = await _sender.Send(new GetPracticeEligibilityQuery(id, roundNumber), ct);
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
            var result = await _sender.Send(new SendInterviewInviteCommand(id, roundNumber), ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { message = "Đã gửi email mời phỏng vấn (chọn lịch) cho ứng viên." });
        }

        [HttpPost("{id}/accept")]
        [Authorize(Policy = "InternalStaff")] // Chỉ HR / Staff mới có quyền bấm duyệt hồ sơ
        public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new AcceptApplicationCommand(id), ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { message = "Đã duyệt hồ sơ ứng tuyển thành công và chuyển sang vòng 1." });
        }

        [HttpPost("{id}/reject")]
        [Authorize(Policy = "InternalStaff")] // Chỉ HR / Staff mới có quyền bấm từ chối
        public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new RejectApplicationCommand(id), ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { message = "Đã từ chối hồ sơ ứng tuyển và gửi thư cảm ơn cho ứng viên." });
        }
    }
}
