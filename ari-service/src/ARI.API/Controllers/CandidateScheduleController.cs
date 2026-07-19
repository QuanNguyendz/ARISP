using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Scheduling;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    public class BookSlotRequest
    {
        public Guid SlotId { get; set; }
        public int Round { get; set; } = 1;
        public string? Token { get; set; }
    }

    /// <summary>
    /// Ứng viên chọn lịch phỏng vấn trên thiết bị cá nhân. Truy cập bằng token lời mời
    /// (InterviewInvite, từ email) HOẶC đã đăng nhập Candidate Portal (JWT) và sở hữu hồ sơ.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class CandidateScheduleController : ControllerBase
    {
        private readonly ISender _sender;

        public CandidateScheduleController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Danh tính candidate từ claims (nếu đã đăng nhập) — dùng cho xác thực quyền truy cập hồ sơ.</summary>
        private (Guid? accountId, string? email) GetCandidateIdentity()
        {
            if (User?.Identity?.IsAuthenticated != true) return (null, null);
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
            return (Guid.TryParse(subClaim, out var accId) ? accId : null, emailClaim);
        }

        private IActionResult MapFailure(Result result)
        {
            return result.ErrorCode switch
            {
                CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                CommonErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };
        }

        /// <summary>Khung giờ còn trống của hồ sơ cho một vòng (để ứng viên chọn).</summary>
        [HttpGet("schedule/{applicationId:guid}/slots")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOpenSlots(Guid applicationId, [FromQuery] int round, [FromQuery] string? token, CancellationToken ct)
        {
            var roundNumber = round > 0 ? round : 1;
            var (accountId, email) = GetCandidateIdentity();

            var result = await _sender.Send(new GetOpenSlotsQuery(applicationId, roundNumber, token, accountId, email), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Đặt một khung giờ phỏng vấn cho hồ sơ + vòng.</summary>
        [HttpPost("schedule/{applicationId:guid}/book")]
        [AllowAnonymous]
        public async Task<IActionResult> Book(Guid applicationId, [FromBody] BookSlotRequest request, CancellationToken ct)
        {
            var roundNumber = request.Round > 0 ? request.Round : 1;
            var (accountId, email) = GetCandidateIdentity();

            var result = await _sender.Send(new BookSlotCommand(applicationId, request.SlotId, roundNumber, request.Token, accountId, email), ct);
            if (result.IsFailure) return MapFailure(result);

            var value = result.Value;
            return Ok(new
            {
                message = "Đặt lịch thành công. Bạn có thể luyện tập với phỏng vấn thử trước ngày hẹn.",
                bookingId = value.BookingId,
                slot = value.Slot,
            });
        }

        /// <summary>Lịch phỏng vấn của ứng viên đang đăng nhập (sắp tới / đã qua).</summary>
        [HttpGet("candidate/schedule")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> GetMySchedule(CancellationToken ct)
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
            Guid.TryParse(subClaim, out var accId);

            var result = await _sender.Send(new GetCandidateScheduleQuery(accId, emailClaim), ct);
            var value = result.Value;
            return Ok(new { upcomingSlots = value.UpcomingSlots, pastSlots = value.PastSlots });
        }
    }
}
