using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using ARISP.Application.DTOs;
using ARISP.Application.Services;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/interview")] // Đồng bộ chuẩn prefix số ít theo đúng yêu cầu đồng bộ hệ thống backend
    public class InterviewController : ControllerBase
    {
        private readonly InterviewService _interviewService;
        private readonly InterviewCodeService _interviewCodeService;
        private readonly IConfiguration _configuration;

        public InterviewController(InterviewService interviewService, InterviewCodeService interviewCodeService, IConfiguration configuration)
        {
            _interviewService = interviewService;
            _interviewCodeService = interviewCodeService;
            _configuration = configuration;
        }

        private string CandidateBaseUrl =>
            _configuration["Frontend:CandidateBaseUrl"]
            ?? _configuration["Authentication:AdminFrontendUrl"]
            ?? "http://localhost:3000";

        /// <summary>
        /// POST /api/interview/generate-code
        /// </summary>
        [HttpPost("generate-code")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GenerateInterviewCode([FromBody] GenerateCodeRequest request, CancellationToken ct)
        {
            var hrUserId = GetCurrentUserId();

            var result = await _interviewCodeService.GenerateCodeAsync(request.ApplicationId, request.RoundNumber, hrUserId, ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new
            {
                code = result.Value.Code,
                expiresAt = result.Value.ExpiresAt,
                applicationId = result.Value.ApplicationId
            });
        }

        /// <summary>
        /// POST /api/interview/generate-code-batch (MỚI BỔ SUNG)
        /// </summary>
        [HttpPost("generate-code-batch")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GenerateInterviewCodeBatch([FromBody] GenerateBatchRequest request, CancellationToken ct)
        {
            var hrUserId = GetCurrentUserId();

            var result = await _interviewCodeService.GenerateBatchAsync(request.ApplicationIds, request.RoundNumber, hrUserId, ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            // Map định dạng trả về mảng danh sách: [ { code, applicationId }, ... ]
            var response = result.Value.Select(c => new
            {
                code = c.Code,
                applicationId = c.ApplicationId
            });

            return Ok(response);
        }

        /// <summary>
        /// POST /api/interview/validate-code
        /// </summary>
        [HttpPost("validate-code")]
        [AllowAnonymous] // Kiosk công cộng không cần Token Đăng nhập
        public async Task<IActionResult> ValidateInterviewCode([FromBody] ValidateCodeRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { message = "Mã phỏng vấn không được để trống.", valid = false });
            }

            var result = await _interviewCodeService.ValidateCodeAsync(request.Code, ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error, valid = false });
            }

            if (!result.Value.Valid)
            {
                return Ok(new { valid = false, message = "Mã phỏng vấn không hợp lệ, đã sử dụng hoặc hết hạn." });
            }

            return Ok(new
            {
                valid = true,
                sessionId = result.Value.SessionId
            });
        }

        /// <summary>
        /// GET /api/interview/codes?jobPostingId={id} (MỚI BỔ SUNG)
        /// </summary>
        [HttpGet("codes")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetCodesByJob([FromQuery] Guid jobPostingId, CancellationToken ct)
        {
            if (jobPostingId == Guid.Empty)
            {
                return BadRequest(new { message = "JobPostingId không hợp lệ." });
            }

            var list = await _interviewCodeService.GetCodesByJobAsync(jobPostingId, ct);
            return Ok(list);
        }

        /// <summary>
        /// GET /api/interview/sessions
        /// Danh sách phiên phỏng vấn cho HR (kèm ứng viên, vị trí, verdict).
        /// </summary>
        [HttpGet("sessions")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetSessions(CancellationToken ct)
        {
            var list = await _interviewService.GetSessionsForHrAsync(ct);
            return Ok(list);
        }

        #region ================= EXISTED INTERVIEW SESSION ENDPOINTS =================

        [HttpPost("session/start")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
        {
            var result = await _interviewService.StartSessionAsync(request);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpPost("session/{id}/answer")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> SubmitAnswer(Guid id, [FromBody] SubmitAnswerRequest request)
        {
            var result = await _interviewService.SubmitAnswerAsync(id, request.QuestionId, request.Transcript, request.ResponseTimeMs);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpPost("session/{id}/end")]
        [Authorize]
        public async Task<IActionResult> EndSession(Guid id, [FromQuery] string status = "completed")
        {
            var result = await _interviewService.EndSessionAsync(id, status);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(new { success = true });
        }

        [HttpPost("review/confirm")]
        [Authorize(Policy = "HrManagement")]
        public async Task<IActionResult> ConfirmReview([FromHeader(Name = "X-User-Id")] string userIdStr, [FromBody] ConfirmReviewRequest request)
        {
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            }
            var result = await _interviewService.SubmitHrReviewAsync(userId, request, CandidateBaseUrl);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(new { success = true });
        }

        #endregion

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var parsedId))
            {
                return parsedId;
            }
            return Guid.Parse("22222222-2222-2222-2222-222222222222"); // Mặc định phòng hờ
        }
    }
}
