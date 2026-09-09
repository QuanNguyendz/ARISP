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
using ARI.Application.Emails;
using ARI.Application.HiringTeam;
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

    /// <summary>
    /// Body của POST /applications/{id}/reject — thư cảm ơn do nhân sự sửa ở trình soạn thảo
    /// (ADR-061). Bỏ trống → dùng mẫu.
    /// </summary>
    public class RejectApplicationRequest
    {
        public EmailOverride? EmailOverride { get; set; }
    }

    /// <summary>Body của POST /applications/{id}/hm-decision.</summary>
    public class HmDecisionRequest
    {
        /// <summary>approved | rejected</summary>
        [Required(ErrorMessage = "Vui lòng chọn quyết định.")]
        public string Decision { get; set; } = string.Empty;

        /// <summary>Bắt buộc khi từ chối.</summary>
        public string? Note { get; set; }

        /// <summary>
        /// Khung giờ Hiring Manager có mặt được cho vòng 1 (ADR-067) — gửi kèm khi duyệt.
        /// Recruiter chỉ được xếp ca nằm trong các khung này.
        /// </summary>
        public List<HmAvailabilityWindowRequest>? Availabilities { get; set; }
    }

    /// <summary>Một khung giờ rảnh của Hiring Manager.</summary>
    public class HmAvailabilityWindowRequest
    {
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>Body của POST /applications/{id}/hm-bypass.</summary>
    public class HmBypassRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập lý do vượt cổng duyệt.")]
        public string Reason { get; set; } = string.Empty;
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

        /// <summary>Ánh xạ mã lỗi phân quyền sang HTTP — 403 khác 404, đừng gộp làm một.</summary>
        private IActionResult MapFailure(string? errorCode, string? message) => errorCode switch
        {
            CommonErrorCodes.NotFound => NotFound(new { message }),
            CommonErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message }),
            _ => BadRequest(new { message }),
        };

        [HttpGet("{id}")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetApplicationById(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(
                new GetApplicationByIdQuery(id, _currentUserService.UserId, _currentUserService.Role), ct);
            if (result.IsFailure)
            {
                return MapFailure(result.ErrorCode, result.Error);
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

            var result = await _sender.Send(
                new UpdateApplicationStatusCommand(id, request.Status, _currentUserService.UserId, _currentUserService.Role), ct);
            if (result.IsFailure)
            {
                return MapFailure(result.ErrorCode, result.Error);
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
        /// Danh sách hồ sơ ứng tuyển — phạm vi do SERVER quyết định theo vai trò và quyền trên tin.
        ///
        /// <paramref name="mine"/> nay chỉ là BỘ LỌC GIAO DIỆN ("chỉ hiện tin tôi tạo"), thu hẹp
        /// thêm bên trong phạm vi đã được phép. Trước đây nó là cổng bảo mật duy nhất và do client
        /// tự khai — bỏ tham số đi là đọc được hồ sơ của toàn công ty.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetApplications([FromQuery] bool mine, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } uid || uid == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng." });

            var result = await _sender.Send(new GetApplicationsQuery(uid, _currentUserService.Role, mine), ct);
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

        /// <summary>Lịch sử thư đã gửi cho ứng viên này (ADR-061) — tab "Lịch sử email".</summary>
        [HttpGet("{id}/emails")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetEmails(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(
                new GetApplicationEmailsQuery(id, _currentUserService.UserId, _currentUserService.Role), ct);
            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }

        // ─────────── Cổng duyệt shortlist của Hiring Manager (ADR-061) ───────────

        /// <summary>
        /// Chủ tin gửi hồ sơ cho Hiring Manager duyệt. Hồ sơ chuyển sang <c>hm_review</c> và
        /// KHÔNG xếp lịch được cho tới khi cổng mở.
        /// </summary>
        [HttpPost("{id}/request-hm-approval")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> RequestHmApproval(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(
                new RequestHmApprovalCommand(id, _currentUserService.UserId, _currentUserService.Role), ct);
            return result.IsFailure
                ? MapFailure(result.ErrorCode, result.Error)
                : Ok(new { message = "Đã gửi hồ sơ cho Hiring Manager duyệt." });
        }

        /// <summary>Hiring Manager duyệt hoặc từ chối hồ sơ trong shortlist.</summary>
        [HttpPost("{id}/hm-decision")]
        [Authorize(Policy = "HiringDecision")]
        public async Task<IActionResult> HmDecision(Guid id, [FromBody] HmDecisionRequest request, CancellationToken ct)
        {
            var windows = (request.Availabilities ?? new List<HmAvailabilityWindowRequest>())
                .Select(w => new ARI.Application.Scheduling.HmAvailabilityWindowInput(w.StartTime, w.EndTime, w.Note))
                .ToList();

            var result = await _sender.Send(new HmDecideApplicationCommand(
                id, request.Decision, request.Note, _currentUserService.UserId, _currentUserService.Role, windows), ct);
            return result.IsFailure
                ? MapFailure(result.ErrorCode, result.Error)
                : Ok(new { message = "Đã ghi nhận quyết định của Hiring Manager." });
        }

        /// <summary>
        /// Quản trị viên vượt cổng duyệt (HM nghỉ / gấp). Lý do bắt buộc, ghi audit log và
        /// báo cho chính Hiring Manager bị vượt.
        /// </summary>
        [HttpPost("{id}/hm-bypass")]
        [Authorize(Policy = "HrManagement")]
        public async Task<IActionResult> HmBypass(Guid id, [FromBody] HmBypassRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new BypassHmApprovalCommand(
                id, request.Reason, _currentUserService.UserId, _currentUserService.Role), ct);
            return result.IsFailure
                ? MapFailure(result.ErrorCode, result.Error)
                : Ok(new { message = "Đã vượt cổng duyệt và thông báo cho Hiring Manager." });
        }

        [HttpPost("{id}/reject")]
        [Authorize(Policy = "InternalStaff")] // Chỉ HR / Staff mới có quyền bấm từ chối
        public async Task<IActionResult> Reject(
            Guid id, [FromBody] RejectApplicationRequest? request, CancellationToken ct)
        {
            var result = await _sender.Send(
                new RejectApplicationCommand(
                    id, _currentUserService.UserId, _currentUserService.Role, request?.EmailOverride), ct);
            if (result.IsFailure)
            {
                return MapFailure(result.ErrorCode, result.Error);
            }

            return Ok(new { message = "Đã từ chối hồ sơ ứng tuyển và gửi thư cảm ơn cho ứng viên." });
        }
    }
}
