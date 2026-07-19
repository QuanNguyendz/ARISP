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
    /// <summary>
    /// Quản lý khung giờ phỏng vấn (Availability Slots) cho Recruiter/HR.
    /// Recruiter chỉ thao tác trên slot của job mình tạo; HrAdmin/SuperAdmin mọi job.
    /// (Phần đặt lịch của ứng viên nằm ở các endpoint /api/schedule/* — Phase B2.)
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
    }
}
