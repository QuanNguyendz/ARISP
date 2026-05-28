using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ARISP.Application.DTOs;
using ARISP.Application.Services;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InterviewController : ControllerBase
    {
        private readonly InterviewService _interviewService;

        public InterviewController(InterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        [HttpPost("session/start")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> StartSession([FromHeader(Name = "X-Organization-Id")] string orgIdStr, [FromBody] StartSessionRequest request)
        {
            if (!Guid.TryParse(orgIdStr, out var orgId))
            {
                orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            }

            var result = await _interviewService.StartSessionAsync(orgId, request);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        [HttpPost("session/{id}/answer")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> SubmitAnswer(Guid id, [FromBody] SubmitAnswerRequest request)
        {
            var result = await _interviewService.SubmitAnswerAsync(id, request.QuestionId, request.Transcript, request.ResponseTimeMs);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        [HttpPost("session/{id}/end")]
        [Authorize]
        public async Task<IActionResult> EndSession(Guid id, [FromQuery] string status = "completed")
        {
            var result = await _interviewService.EndSessionAsync(id, status);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { success = true });
        }

        [HttpPost("review/confirm")]
        [Authorize(Policy = "HrManagement")]
        public async Task<IActionResult> ConfirmReview([FromHeader(Name = "X-User-Id")] string userIdStr, [FromBody] ConfirmReviewRequest request)
        {
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                userId = Guid.Parse("22222222-2222-2222-2222-222222222222"); // Recruiter default
            }

            var result = await _interviewService.SubmitHrReviewAsync(userId, request);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { success = true });
        }
    }
}
