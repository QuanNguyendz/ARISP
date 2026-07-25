using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.OnlineTest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Cổng ứng viên — làm bài thi trắc nghiệm online (Online Test). Ứng viên đăng nhập Portal
    /// lấy đề (ẩn đáp án), nộp bài → hệ thống tự chấm theo điểm sàn của job.
    /// </summary>
    [ApiController]
    [Route("api/portal/online-test")]
    [Authorize(Policy = "CandidateOnly")]
    public class CandidateOnlineTestController : ControllerBase
    {
        private readonly ISender _sender;

        public CandidateOnlineTestController(ISender sender)
        {
            _sender = sender;
        }

        private bool TryGetCandidateId(out Guid candidateId)
        {
            candidateId = Guid.Empty;
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(subClaim) && Guid.TryParse(subClaim, out candidateId);
        }

        private string? GetEmailClaim() =>
            User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;

        private IActionResult MapFailure(Result result)
        {
            return result.ErrorCode switch
            {
                CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                CommonErrorCodes.Forbidden => Forbid(),
                CommonErrorCodes.Conflict => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };
        }

        /// <summary>Lấy đề thi trắc nghiệm cho hồ sơ của ứng viên (kèm trạng thái đã nộp nếu có).</summary>
        [HttpGet("{applicationId:guid}")]
        public async Task<IActionResult> GetTest(Guid applicationId, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(new GetCandidateOnlineTestQuery(applicationId, candidateId, GetEmailClaim()), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Nộp bài thi trắc nghiệm — hệ thống tự chấm và trả kết quả đạt/không đạt.</summary>
        [HttpPost("{applicationId:guid}/submit")]
        public async Task<IActionResult> Submit(Guid applicationId, [FromBody] SubmitOnlineTestRequest request, CancellationToken ct)
        {
            if (!TryGetCandidateId(out var candidateId))
                return Unauthorized(new { message = "Không xác định được danh tính ứng viên." });

            var result = await _sender.Send(
                new SubmitOnlineTestCommand(applicationId, candidateId, GetEmailClaim(), request.Answers), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }
    }
}
