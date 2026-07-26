using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Scheduling;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>Body: HR gán 1 khung giờ trong kho cho 1 hồ sơ ứng viên (ADR-048).</summary>
    public class AssignSlotRequest
    {
        public Guid ApplicationId { get; set; }
        public Guid SlotId { get; set; }
        public int Round { get; set; } = 1;
    }

    /// <summary>
    /// Quản lý khung giờ phỏng vấn (Availability Slots) cho Recruiter/HR + gán lịch cho ứng viên.
    /// Recruiter chỉ thao tác trên slot của job mình tạo; HrAdmin/SuperAdmin mọi job.
    /// Ứng viên KHÔNG tự chọn lịch — HR gán trực tiếp qua POST /api/schedules/assign (ADR-048).
    /// </summary>
    [ApiController]
    [Route("api/schedules")]
    [Authorize(Policy = "InternalStaff")]
    public class ScheduleController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public ScheduleController(ISender sender, ICurrentUserService currentUser)
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
                _ => BadRequest(new { message = result.Error }),
            };
        }

        /// <summary>Danh sách slot của một job (tùy chọn lọc theo vòng).</summary>
        [HttpGet("slots")]
        public async Task<IActionResult> GetSlots([FromQuery] Guid jobPostingId, [FromQuery] int? round, CancellationToken ct)
        {
            var result = await _sender.Send(new GetAvailabilitySlotsQuery(jobPostingId, round, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Tạo một khung giờ phỏng vấn.</summary>
        [HttpPost("slots")]
        public async Task<IActionResult> CreateSlot([FromBody] CreateSlotRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new CreateSlotCommand(request, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>Xoá một khung giờ (chỉ khi chưa có ai đặt).</summary>
        [HttpDelete("slots/{id:guid}")]
        public async Task<IActionResult> DeleteSlot(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new DeleteSlotCommand(id, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new { message = "Đã xoá khung giờ.", id });
        }

        /// <summary>Cập nhật sức chứa của khung giờ (không nhỏ hơn số đã đặt).</summary>
        [HttpPatch("slots/{id:guid}/capacity")]
        public async Task<IActionResult> UpdateCapacity(Guid id, [FromBody] UpdateSlotCapacityRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new UpdateSlotCapacityCommand(id, request.Capacity, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(result.Value);
        }

        /// <summary>HR gán 1 khung giờ trong kho cho 1 ứng viên (ấn định lịch phỏng vấn thật).</summary>
        [HttpPost("assign")]
        public async Task<IActionResult> AssignSlot([FromBody] AssignSlotRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(
                new AssignSlotCommand(request.ApplicationId, request.SlotId, request.Round, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure) return MapFailure(result);
            return Ok(new
            {
                message = "Đã xếp lịch phỏng vấn cho ứng viên.",
                bookingId = result.Value.BookingId,
                slot = result.Value.Slot,
            });
        }
    }
}
