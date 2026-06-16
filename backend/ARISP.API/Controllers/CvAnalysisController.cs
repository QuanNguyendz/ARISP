using System;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARISP.API.Controllers
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
        private readonly CvJdAnalysisService _analysisService;

        public CvAnalysisController(CvJdAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        [HttpPost("analyze")]
        [Consumes("multipart/form-data")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> AnalyzeCv([FromForm] AnalyzeCvRequest request, CancellationToken ct)
        {
            if (request.CvFile == null || request.CvFile.Length == 0)
                return BadRequest(new { message = "File CV không hợp lệ." });

            var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".txt" };
            var extension = System.IO.Path.GetExtension(request.CvFile.FileName).ToLowerInvariant();
            if (!System.Linq.Enumerable.Contains(allowedExtensions, extension))
            {
                return BadRequest(new { message = "Chỉ chấp nhận file định dạng PDF, DOC, DOCX, TXT." });
            }

            using var cvStream = request.CvFile.OpenReadStream();
            var result = await _analysisService.AnalyzeAndCacheAsync(request.JobPostingId, cvStream, request.CvFile.FileName, ct);
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

            var hasPermission = await _analysisService.CheckCandidateOwnershipAsync(id, candidateId, ct);
            if (!hasPermission)
            {
                return StatusCode(403, new { message = "Bạn không có quyền xem bản đánh giá này, hoặc bạn chưa hoàn tất việc nộp đơn." });
            }

            var result = await _analysisService.GetAnalysisByIdAsync(id, ct);
            if (result.IsFailure) return NotFound(new { message = result.Error });
            return Ok(result.Value);
        }

        [HttpGet("/api/applications/{applicationId}/cv-analysis")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetByApplicationId(Guid applicationId, CancellationToken ct)
        {
            var result = await _analysisService.GetAnalysisByApplicationIdAsync(applicationId, ct);
            if (result.IsFailure) return NotFound(new { message = result.Error });
            return Ok(result.Value);
        }
        [HttpDelete("clear-cache")]
        [AllowAnonymous]
        public async Task<IActionResult> ClearCache(CancellationToken ct)
        {
            await _analysisService.ClearAllCacheAsync(ct);
            return Ok(new { message = "Đã xóa toàn bộ bộ nhớ đệm (cache) phân tích CV." });
        }
    }
}
