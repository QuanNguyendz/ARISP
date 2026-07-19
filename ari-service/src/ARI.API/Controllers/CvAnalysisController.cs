using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvAnalysis.Commands.AnalyzeCv;
using ARI.Application.CvAnalysis.Commands.ClearCvAnalysisCache;
using ARI.Application.CvAnalysis.Queries.GetCvAnalysis;
using ARI.Application.CvAnalysis.Queries.GetCvAnalysisByApplication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    public class AnalyzeCvRequest
    {
        public Guid JobPostingId { get; set; }
        public IFormFile CvFile { get; set; } = null!;
    }

    [ApiController]
    [Route("api/cv-analysis")]
    public class CvAnalysisController : ControllerBase
    {
        private readonly ISender _sender;

        public CvAnalysisController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost("analyze")]
        [Consumes("multipart/form-data")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> AnalyzeCv([FromForm] AnalyzeCvRequest request, CancellationToken ct)
        {
            if (request.CvFile == null || request.CvFile.Length == 0)
                return BadRequest(new { message = "File CV không hợp lệ." });

            var allowedExtensions = new[] { ".pdf", ".docx" };
            var extension = System.IO.Path.GetExtension(request.CvFile.FileName).ToLowerInvariant();
            if (!System.Linq.Enumerable.Contains(allowedExtensions, extension))
            {
                return BadRequest(new { message = "Chỉ chấp nhận file định dạng PDF, DOCX." });
            }

            using var cvStream = request.CvFile.OpenReadStream();
            var result = await _sender.Send(new AnalyzeCvCommand(request.JobPostingId, cvStream, request.CvFile.FileName), ct);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var subClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(subClaim, out var candidateId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
            }

            var result = await _sender.Send(new GetCvAnalysisQuery(id, candidateId), ct);
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.Forbidden
                    ? StatusCode(403, new { message = result.Error })
                    : NotFound(new { message = result.Error });
            }
            return Ok(result.Value);
        }

        [HttpGet("/api/applications/{applicationId}/cv-analysis")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetByApplicationId(Guid applicationId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetCvAnalysisByApplicationQuery(applicationId), ct);
            if (result.IsFailure) return NotFound(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpDelete("clear-cache")]
        [AllowAnonymous]
        public async Task<IActionResult> ClearCache(CancellationToken ct)
        {
            await _sender.Send(new ClearCvAnalysisCacheCommand(), ct);
            return Ok(new { message = "Đã xóa toàn bộ bộ nhớ đệm (cache) phân tích CV." });
        }
    }
}
