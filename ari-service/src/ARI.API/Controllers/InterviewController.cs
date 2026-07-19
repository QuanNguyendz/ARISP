using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Interviews;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    [ApiController]
    [Route("api/interview")] // Đồng bộ chuẩn prefix số ít theo đúng yêu cầu đồng bộ hệ thống backend
    public class InterviewController : ControllerBase
    {
        private readonly ISender _sender;

        public InterviewController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// POST /api/interview/generate-code
        /// </summary>
        [HttpPost("generate-code")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GenerateInterviewCode([FromBody] GenerateCodeRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new GenerateInterviewCodeCommand(request.ApplicationId, request.RoundNumber, GetCurrentUserId()), ct);
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
        /// POST /api/interview/generate-code-batch
        /// </summary>
        [HttpPost("generate-code-batch")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GenerateInterviewCodeBatch([FromBody] GenerateBatchRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new GenerateInterviewCodeBatchCommand(request.ApplicationIds, request.RoundNumber, GetCurrentUserId()), ct);
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

            var result = await _sender.Send(new ValidateInterviewCodeCommand(request.Code), ct);
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
        /// GET /api/interview/codes?jobPostingId={id}
        /// </summary>
        [HttpGet("codes")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetCodesByJob([FromQuery] Guid jobPostingId, CancellationToken ct)
        {
            if (jobPostingId == Guid.Empty)
            {
                return BadRequest(new { message = "JobPostingId không hợp lệ." });
            }

            var result = await _sender.Send(new GetInterviewCodesByJobQuery(jobPostingId), ct);
            return Ok(result.Value);
        }

        /// <summary>
        /// GET /api/interview/sessions
        /// Danh sách phiên phỏng vấn cho HR (kèm ứng viên, vị trí, verdict).
        /// </summary>
        [HttpGet("sessions")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetSessions(CancellationToken ct)
        {
            var result = await _sender.Send(new GetHrInterviewSessionsQuery(), ct);
            return Ok(result.Value);
        }

        #region ================= EXISTED INTERVIEW SESSION ENDPOINTS =================

        [HttpPost("session/start")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
        {
            var result = await _sender.Send(new StartInterviewSessionCommand(request));
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        /// <summary>
        /// GET /api/interview/session/{id}/media-config
        /// Token Deepgram (STT) + HeyGen (avatar) + ngôn ngữ/voice để FE vào phòng phỏng vấn.
        /// </summary>
        [HttpGet("session/{id}/media-config")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> GetMediaConfig(Guid id, CancellationToken ct)
        {
            var (accountId, email) = GetCandidateIdentity();
            var result = await _sender.Send(new GetMediaConfigQuery(id, accountId, email), ct);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        /// <summary>
        /// POST /api/interview/session/{id}/tts  body { text }
        /// TTS câu hỏi → base64 PCM 24k cho FE đẩy vào LiveAvatar repeatAudio (ADR-044).
        /// </summary>
        [HttpPost("session/{id}/tts")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> GetSpeechAudio(Guid id, [FromBody] TtsRequest request, CancellationToken ct)
        {
            var (accountId, email) = GetCandidateIdentity();
            var result = await _sender.Send(new SynthesizeSpeechCommand(id, request?.Text ?? string.Empty, accountId, email), ct);
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(new { audio = result.Value });
        }

        [HttpPost("session/{id}/answer")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> SubmitAnswer(Guid id, [FromBody] SubmitAnswerRequest request)
        {
            var result = await _sender.Send(new SubmitAnswerCommand(id, request.QuestionId, request.Transcript, request.ResponseTimeMs));
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpPost("session/{id}/end")]
        [Authorize]
        public async Task<IActionResult> EndSession(Guid id, [FromQuery] string status = "completed")
        {
            var result = await _sender.Send(new EndInterviewSessionCommand(id, status));
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
            var result = await _sender.Send(new ConfirmHrReviewCommand(userId, request));
            if (result.IsFailure) return BadRequest(new { message = result.Error });
            return Ok(new { success = true });
        }

        #endregion

        private (Guid? accountId, string? email) GetCandidateIdentity()
        {
            var subClaim = User.FindFirst("sub")?.Value
                           ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst("email")?.Value
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            return (Guid.TryParse(subClaim, out var aid) ? aid : null, email);
        }

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
