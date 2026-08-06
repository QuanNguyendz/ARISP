using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.OnlineTest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Ngân hàng câu hỏi trắc nghiệm (Online Test) cho HR/Recruiter — CRUD câu hỏi per Job Posting
    /// và cấu hình điểm sàn. Recruiter chỉ thao tác trên job mình tạo; HrAdmin/SuperAdmin mọi job.
    /// </summary>
    [ApiController]
    [Route("api/online-test")]
    [Authorize(Policy = "InternalStaff")]
    public class OnlineTestController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public OnlineTestController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        private IActionResult MapFailure(Result result)
        {
            return result.ErrorCode switch
            {
                CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                CommonErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
                CommonErrorCodes.Conflict => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };
        }

        /// <summary>Ngân hàng câu hỏi + điểm sàn của một job (kèm đáp án đúng cho HR).</summary>
        [HttpGet("jobs/{jobId:guid}")]
        public async Task<IActionResult> GetBank(Guid jobId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetOnlineTestBankQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Thêm một câu hỏi trắc nghiệm cho job.</summary>
        [HttpPost("jobs/{jobId:guid}/questions")]
        public async Task<IActionResult> CreateQuestion(Guid jobId, [FromBody] UpsertOnlineTestQuestionRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new CreateOnlineTestQuestionCommand(jobId, request, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Nhập hàng loạt câu hỏi từ file Excel (.xlsx) theo file mẫu.</summary>
        [HttpPost("jobs/{jobId:guid}/questions/import")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportQuestions(Guid jobId, IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File không được để trống." });
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Kích thước file không được vượt quá 5MB." });

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (ext != ".xlsx")
                return BadRequest(new { message = "Định dạng không hợp lệ. Chỉ chấp nhận file .xlsx (Excel)." });

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var result = await _sender.Send(
                new ImportOnlineTestQuestionsCommand(jobId, bytes, file.FileName, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Tải file Excel mẫu để nhập câu hỏi (đúng thứ tự cột hệ thống đọc).</summary>
        [HttpGet("jobs/{jobId:guid}/questions/template")]
        public async Task<IActionResult> DownloadTemplate(Guid jobId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetOnlineTestImportTemplateQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            var file = result.Value;
            return File(file.Content, file.ContentType, file.FileName);
        }

        /// <summary>Sửa nội dung / phương án / đáp án đúng của một câu hỏi.</summary>
        [HttpPut("questions/{id:guid}")]
        public async Task<IActionResult> UpdateQuestion(Guid id, [FromBody] UpsertOnlineTestQuestionRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new UpdateOnlineTestQuestionCommand(id, request, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Xoá một câu hỏi khỏi ngân hàng.</summary>
        [HttpDelete("questions/{id:guid}")]
        public async Task<IActionResult> DeleteQuestion(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new DeleteOnlineTestQuestionCommand(id, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { message = "Đã xoá câu hỏi.", id });
        }

        /// <summary>Cập nhật cấu hình bài thi: điểm sàn (%), số câu mỗi bài, thời lượng (phút).</summary>
        [HttpPut("jobs/{jobId:guid}/settings")]
        public async Task<IActionResult> UpdateSettings(Guid jobId, [FromBody] UpdateOnlineTestSettingsRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new UpdateOnlineTestSettingsCommand(
                jobId, request.PassScore, request.QuestionsPerTest, request.DurationMinutes, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Kết quả bài thi trắc nghiệm của một hồ sơ ứng tuyển (null nếu chưa nộp).</summary>
        [HttpGet("applications/{applicationId:guid}/result")]
        public async Task<IActionResult> GetResult(Guid applicationId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetOnlineTestResultForStaffQuery(applicationId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Bảng tổng hợp điểm bài thi trắc nghiệm của toàn bộ ứng viên đã thi trong một job.</summary>
        [HttpGet("jobs/{jobId:guid}/results")]
        public async Task<IActionResult> GetJobResults(Guid jobId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetOnlineTestResultsByJobQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Tải bảng điểm dưới dạng Excel (.xlsx).</summary>
        [HttpGet("jobs/{jobId:guid}/results/export")]
        public async Task<IActionResult> ExportJobResults(Guid jobId, CancellationToken ct)
        {
            var result = await _sender.Send(new ExportOnlineTestResultsQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            var file = result.Value;
            return File(file.Content, file.ContentType, file.FileName);
        }
    }
}
