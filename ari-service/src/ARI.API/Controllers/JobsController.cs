using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Jobs.Commands.AnalyzeJd;
using ARI.Application.Jobs.Commands.CreateJob;
using ARI.Application.Jobs.Commands.CreateJobSlots;
using ARI.Application.Jobs.Commands.DeleteJob;
using ARI.Application.Jobs.Commands.UpdateJob;
using ARI.Application.Jobs.Commands.UpdateJobStatus;
using ARI.Application.Jobs.Commands.UpdateJobDisplay;
using ARI.Application.Jobs.Queries.GetAdminJobs;
using ARI.Application.Jobs.Queries.GetJobApplications;
using ARI.Application.Jobs.Queries.GetJobById;
using ARI.Application.Jobs.Queries.GetJobFacets;
using ARI.Application.Jobs.Queries.GetJobs;
using ARI.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public JobsController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        /// <summary>Map failure của Jobs feature → HTTP status (400/401/403/404/500) với body { message }.</summary>
        private IActionResult MapFailure(Result result)
        {
            return result.ErrorCode switch
            {
                CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                CommonErrorCodes.Unauthorized => Unauthorized(new { message = result.Error }),
                CommonErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
                CommonErrorCodes.ServerError => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };
        }

        /// <summary>HR tạo job posting kèm cấu hình vòng phỏng vấn.</summary>
        [HttpPost]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> CreateJob([FromBody] CreateJobPostingRequest request, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            var result = await _sender.Send(new CreateJobCommand(request, userId), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Danh sách job công khai trên Job Board kèm lọc, phân trang, và sắp xếp.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobs(
            [FromQuery] string? search,
            [FromQuery] string? categories,
            [FromQuery] string? employmentTypes,
            [FromQuery] string? experienceLevels,
            [FromQuery] string? workModes,
            [FromQuery] string? locations,
            [FromQuery] string? skills,
            [FromQuery] string? languages,
            [FromQuery] string? sortBy,
            [FromQuery] int? minSalary,
            [FromQuery] int? maxSalary,
            [FromQuery] bool? salaryIsNegotiable,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 8,
            CancellationToken ct = default)
        {
            // Danh tính đang đăng nhập (nếu có) — dùng cho sort "relevance" theo kỹ năng hồ sơ.
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? currentUserId = Guid.TryParse(sub, out var uid) ? uid : null;

            var result = await _sender.Send(new GetJobsQuery(
                search, categories, employmentTypes, experienceLevels, workModes, locations, skills, languages,
                sortBy, minSalary, maxSalary, salaryIsNegotiable, page, pageSize, currentUserId), ct);

            var value = result.Value;
            return Ok(new { items = value.Items, totalCount = value.TotalCount });
        }

        /// <summary>
        /// Bộ lọc khả dụng cho Job Board: chỉ trả về những giá trị thực sự có trong các tin
        /// đang active &amp; public, kèm số lượng (vd: "Junior (2)"). Dùng cho sidebar bộ lọc.
        /// </summary>
        [HttpGet("facets")]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobFacets(CancellationToken ct)
        {
            var result = await _sender.Send(new GetJobFacetsQuery(), ct);
            return Ok(result.Value);
        }

        /// <summary>Chi tiết job cho trang mô tả công việc (Hỗ trợ HR xem cả draft/paused).</summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobById(Guid id, CancellationToken ct)
        {
            var isStaff = User.Identity?.IsAuthenticated == true &&
                          (User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.HrAdmin) || User.IsInRole(AppRoles.Recruiter));

            var currentUserId = _currentUserService.UserId;
            var role = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;

            var result = await _sender.Send(new GetJobByIdQuery(id, isStaff, currentUserId, role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// Danh sách job dành cho HR (bao gồm cả draft, closed...).
        /// <paramref name="mine"/>=true: chỉ trả về tin do người đang đăng nhập tạo (dùng cho Recruiter workspace).
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetAdminJobs(CancellationToken ct, [FromQuery] bool mine = false)
        {
            Guid? mineUid = null;
            if (mine)
            {
                if (_currentUserService.UserId is not { } uid || uid == Guid.Empty)
                    return Unauthorized(new { message = "Không xác định được người dùng." });
                mineUid = uid;
            }

            var result = await _sender.Send(new GetAdminJobsQuery(mineUid), ct);
            return Ok(result.Value);
        }

        [HttpPost("{id:guid}/slots")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> AddAvailabilitySlots(Guid id, [FromBody] List<CreateAvailabilitySlotRequest> slots, CancellationToken ct)
        {
            var result = await _sender.Send(new CreateJobSlotsCommand(id, slots), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { message = "Availability slots configured successfully." });
        }

        /// <summary>
        /// HR cập nhật job posting kèm cấu hình vòng phỏng vấn.
        /// Chỉ cho phép người tạo hoặc SuperAdmin, HrAdmin chỉnh sửa. Không cho phép cập nhật khi archived.
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> UpdateJob(Guid id, [FromBody] CreateJobPostingRequest request, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            var result = await _sender.Send(new UpdateJobCommand(id, request, userId, _currentUserService.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// HR thực hiện xóa mềm (soft delete) tin tuyển dụng.
        /// Không cho phép xóa nếu có hồ sơ ứng tuyển (Application) đang hoạt động.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> DeleteJob(Guid id, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            var result = await _sender.Send(new DeleteJobCommand(id, userId, _currentUserService.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { message = "Job posting soft-deleted successfully.", jobId = id });
        }

        /// <summary>
        /// Cập nhật trạng thái Job dựa trên quy trình duyệt (Approval Workflow).
        /// </summary>
        /// <remarks>
        /// <b>Quy tắc chuyển trạng thái:</b>
        /// <br/>• <b>Recruiter (Người tạo):</b> Chỉ chuyển: <c>draft</c> → <c>pending</c> (Gửi duyệt), hoặc bài đang <c>active</c> → <c>closed</c> (Đóng).
        /// <br/>• <b>HrAdmin / SuperAdmin:</b> Được duyệt <c>pending</c> → <c>active</c> (Mở tin), từ chối → <c>rejected</c>, hoặc lưu trữ → <c>archived</c>.
        /// <br/>• <b>Nộp lại bài:</b> Bài bị từ chối (<c>rejected</c>) sửa xong chuyển lại thành <c>pending</c> để duyệt lại.
        /// <br/><br/>
        /// <i>Lưu ý:</i> Bắt buộc truyền <c>RejectionReason</c> khi từ chối bài viết (<c>status = "rejected"</c>).
        /// </remarks>
        [HttpPatch("{id:guid}/status")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> UpdateJobStatus(Guid id, [FromBody] UpdateJobStatusRequest request, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            var result = await _sender.Send(new UpdateJobStatusCommand(id, request, userId, _currentUserService.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// Cập nhật cờ hiển thị (Urgent, Public) của tin tuyển dụng.
        /// HR Admin có thể sửa cả 2 bất cứ lúc nào. Recruiter chỉ sửa được Urgent (và Public nếu tin còn là Draft).
        /// </summary>
        [HttpPatch("{id:guid}/display")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> UpdateJobDisplay(Guid id, [FromBody] UpdateJobDisplayRequest request, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng. Đăng nhập HR và gửi Bearer token." });

            var result = await _sender.Send(new UpdateJobDisplayCommand(id, request, userId, _currentUserService.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// Upload file JD (PDF/DOCX), phân tích bằng Gemini để trích xuất các trường auto-fill cho form tạo tin.
        /// File JD được lưu trữ; storageKey + metadata trả về để đính kèm khi submit job. (ADR-042)
        /// </summary>
        [HttpPost("analyze-jd")]
        [Authorize(Policy = "InternalStaff")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AnalyzeJd(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File JD không được để trống." });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "Kích thước file JD không được vượt quá 10MB." });

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowed = new[] { ".pdf", ".docx" };
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(allowed, ext) < 0)
                return BadRequest(new { message = "Định dạng không hợp lệ. Chỉ chấp nhận .pdf hoặc .docx" });

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var result = await _sender.Send(new AnalyzeJdCommand(bytes, file.FileName, ext), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>
        /// Danh sách ứng viên (Application) của MỘT job. Recruiter chỉ xem được job mình tạo;
        /// HrAdmin/SuperAdmin xem được mọi job. Phục vụ màn "kiểm soát ứng viên theo job".
        /// </summary>
        [HttpGet("{id:guid}/applications")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetJobApplications(Guid id, CancellationToken ct)
        {
            if (_currentUserService.UserId is not { } userId || userId == Guid.Empty)
                return Unauthorized(new { message = "Không xác định được người dùng." });

            var result = await _sender.Send(new GetJobApplicationsQuery(id, userId, _currentUserService.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }
    }
}
