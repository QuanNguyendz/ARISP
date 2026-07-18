using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ARISP.Application.DTOs;
using ARISP.Application.Services;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "InternalStaff")]
    public class EvaluationsController : ControllerBase
    {
        private readonly EvaluationService _evaluationService;

        public EvaluationsController(EvaluationService evaluationService)
        {
            _evaluationService = evaluationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetEvaluations(
            [FromQuery] Guid? jobPostingId,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            // Chi tiết hóa việc kiểm tra điều kiện đầu vào
            if (page < 1)
            {
                return BadRequest("Số trang (page) không hợp lệ. Số trang bắt buộc phải lớn hơn hoặc bằng 1.");
            }

            if (pageSize < 1 || pageSize > 10)
            {
                return BadRequest("Kích thước trang (pageSize) không hợp lệ. Kích thước trang phải nằm trong khoảng từ 1 đến 10 phần tử.");
            }

            if (jobPostingId.HasValue && jobPostingId.Value == Guid.Empty)
            {
                return BadRequest("Mã vị trí tuyển dụng (jobPostingId) không được phép là Guid rỗng (Empty Guid).");
            }

            if (!string.IsNullOrEmpty(status))
            {
                var statusLower = status.ToLower();
                if (statusLower != "completed" && statusLower != "pending" && statusLower != "pass" && statusLower != "not_pass")
                {
                    return BadRequest($"Trạng thái lọc '{status}' không hợp lệ. Vui lòng sử dụng một trong các giá trị sau: 'completed' (đã hoàn thành đánh giá), 'pending' (chờ HR duyệt), 'pass' (đạt), hoặc 'not_pass' (không đạt).");
                }
            }

            var result = await _evaluationService.GetEvaluationsAsync(jobPostingId, status, page, pageSize, ct);
            if (result.IsFailure)
            {
                return BadRequest($"Đã xảy ra lỗi hệ thống khi tải danh sách đánh giá: {result.Error}");
            }

            return Ok(result.Value);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetEvaluationDetail(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Mã ID báo cáo đánh giá hoặc mã phiên phỏng vấn không được phép là Guid rỗng.");
            }

            var result = await _evaluationService.GetEvaluationDetailAsync(id, ct);
            if (result.IsFailure)
            {
                return NotFound($"Không tìm thấy báo cáo đánh giá hoặc phiên phỏng vấn có mã ID tương ứng: {id}");
            }

            return Ok(result.Value);
        }

        [HttpGet("session/{sessionId:guid}")]
        public async Task<IActionResult> GetEvaluationBySessionId(Guid sessionId, CancellationToken ct)
        {
            if (sessionId == Guid.Empty)
            {
                return BadRequest("Mã phiên phỏng vấn (sessionId) không được phép là Guid rỗng.");
            }

            var result = await _evaluationService.GetEvaluationDetailAsync(sessionId, ct);
            if (result.IsFailure)
            {
                return NotFound($"Không tìm thấy báo cáo đánh giá nào thuộc về phiên phỏng vấn (sessionId): {sessionId}");
            }

            return Ok(result.Value);
        }

        [HttpGet("application/{applicationId:guid}")]
        public async Task<IActionResult> GetEvaluationsByApplicationId(Guid applicationId, CancellationToken ct)
        {
            if (applicationId == Guid.Empty)
            {
                return BadRequest("Mã hồ sơ ứng tuyển (applicationId) không được phép là Guid rỗng.");
            }

            var result = await _evaluationService.GetEvaluationsByApplicationIdAsync(applicationId, ct);
            if (result.IsFailure)
            {
                return BadRequest($"Không thể tải danh sách đánh giá cho hồ sơ ứng tuyển (applicationId): {result.Error}");
            }

            return Ok(result.Value);
        }
    }
}
