using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Scheduling;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Ứng viên xem lịch phỏng vấn đã được nhân sự xếp; xác nhận lịch hoặc từ chối kèm lý do.
    /// Từ ADR-048, ứng viên KHÔNG tự chọn lịch — HR gán qua /api/schedules/assign; ứng viên chỉ
    /// phản hồi (confirm/decline) để nhân sự sắp lịch khác khi bận.
    /// </summary>
    [ApiController]
    [Route("api")]
    [Authorize(Policy = "CandidateOnly")]
    public class CandidateScheduleController : ControllerBase
    {
        private readonly ISender _sender;

        public CandidateScheduleController(ISender sender)
        {
            _sender = sender;
        }

        private (Guid accId, string? email) Identity()
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
            Guid.TryParse(subClaim, out var accId);
            return (accId, emailClaim);
        }

        private IActionResult MapFailure(Result result) => result.ErrorCode switch
        {
            CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
            CommonErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
            _ => BadRequest(new { message = result.Error }),
        };

        /// <summary>Lịch phỏng vấn của ứng viên đang đăng nhập (sắp tới / đã qua / chờ xếp lại).</summary>
        [HttpGet("candidate/schedule")]
        public async Task<IActionResult> GetMySchedule(CancellationToken ct)
        {
            var (accId, email) = Identity();
            var result = await _sender.Send(new GetCandidateScheduleQuery(accId, email), ct);
            var value = result.Value;
            return Ok(new
            {
                upcoming = value.Upcoming,
                past = value.Past,
                awaitingReschedule = value.AwaitingReschedule,
            });
        }

        /// <summary>Ứng viên xác nhận sẽ tham dự khung giờ đã được xếp.</summary>
        [HttpPost("candidate/schedule/{bookingId:guid}/confirm")]
        public async Task<IActionResult> Confirm(Guid bookingId, CancellationToken ct)
        {
            var (accId, email) = Identity();
            var result = await _sender.Send(new ConfirmScheduleCommand(bookingId, accId, email), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { message = "Đã xác nhận lịch phỏng vấn." });
        }

        /// <summary>Ứng viên bận, từ chối lịch kèm lý do để nhân sự xếp lịch khác.</summary>
        [HttpPost("candidate/schedule/{bookingId:guid}/decline")]
        public async Task<IActionResult> Decline(Guid bookingId, [FromBody] DeclineScheduleRequest request, CancellationToken ct)
        {
            var (accId, email) = Identity();
            var result = await _sender.Send(new DeclineScheduleCommand(bookingId, request?.Reason ?? string.Empty, accId, email), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { message = "Đã gửi lý do từ chối. Nhân sự sẽ xếp lịch khác cho bạn." });
        }

        /// <summary>Ứng viên ẩn (xoá khỏi danh sách) một lịch đã bị huỷ/từ chối. Không đụng luồng xếp lại của nhân sự.</summary>
        [HttpDelete("candidate/schedule/{bookingId:guid}")]
        public async Task<IActionResult> Dismiss(Guid bookingId, CancellationToken ct)
        {
            var (accId, email) = Identity();
            var result = await _sender.Send(new DismissDeclinedScheduleCommand(bookingId, accId, email), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { message = "Đã xoá lịch khỏi danh sách." });
        }
    }
}
