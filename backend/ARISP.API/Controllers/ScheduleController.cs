using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Constants;
using ARISP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARISP.API.Controllers
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public ScheduleController(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        private bool IsAdmin => _currentUser.Role == AppRoles.SuperAdmin || _currentUser.Role == AppRoles.HrAdmin;

        /// <summary>Kiểm tra người dùng có quyền quản lý slot của job này không (chủ tin hoặc admin).</summary>
        private async Task<(bool ok, JobPosting? job)> CanManageAsync(Guid jobPostingId, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null) return (false, null);
            if (_currentUser.UserId is not { } uid || uid == Guid.Empty) return (false, job);
            return (IsAdmin || job.CreatedByUserId == uid, job);
        }

        /// <summary>Danh sách slot của một job (tùy chọn lọc theo vòng).</summary>
        [HttpGet("slots")]
        public async Task<IActionResult> GetSlots([FromQuery] Guid jobPostingId, [FromQuery] int? round, CancellationToken ct)
        {
            if (jobPostingId == Guid.Empty)
                return BadRequest(new { message = "jobPostingId là bắt buộc." });

            var (ok, job) = await CanManageAsync(jobPostingId, ct);
            if (job == null) return NotFound(new { message = "Không tìm thấy tin tuyển dụng." });
            if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền xem lịch của tin này." });

            var slots = await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(
                s => s.JobPostingId == jobPostingId && (!round.HasValue || s.RoundNumber == round.Value), ct);

            var result = slots.OrderBy(s => s.StartTime).Select(AvailabilitySlotResponse.FromEntity);
            return Ok(result);
        }

        /// <summary>Tạo một khung giờ phỏng vấn.</summary>
        [HttpPost("slots")]
        public async Task<IActionResult> CreateSlot([FromBody] CreateSlotRequest request, CancellationToken ct)
        {
            if (request.JobPostingId == Guid.Empty)
                return BadRequest(new { message = "jobPostingId là bắt buộc." });
            if (request.EndTime <= request.StartTime)
                return BadRequest(new { message = "Giờ kết thúc phải sau giờ bắt đầu." });
            if (request.StartTime <= DateTimeOffset.UtcNow)
                return BadRequest(new { message = "Khung giờ phải nằm trong tương lai." });
            if (request.Capacity < 1)
                return BadRequest(new { message = "Sức chứa (capacity) tối thiểu là 1." });
            if (request.RoundNumber < 1)
                return BadRequest(new { message = "RoundNumber phải >= 1." });

            var (ok, job) = await CanManageAsync(request.JobPostingId, ct);
            if (job == null) return NotFound(new { message = "Không tìm thấy tin tuyển dụng." });
            if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền tạo lịch cho tin này." });

            var slot = new AvailabilitySlot
            {
                JobPostingId = request.JobPostingId,
                RoundNumber = request.RoundNumber,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Ho_Chi_Minh" : request.Timezone,
                Capacity = request.Capacity,
                BookedCount = 0,
            };
            await _unitOfWork.Repository<AvailabilitySlot>().AddAsync(slot, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(AvailabilitySlotResponse.FromEntity(slot));
        }

        /// <summary>Xoá một khung giờ (chỉ khi chưa có ai đặt).</summary>
        [HttpDelete("slots/{id:guid}")]
        public async Task<IActionResult> DeleteSlot(Guid id, CancellationToken ct)
        {
            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(id, ct);
            if (slot == null) return NotFound(new { message = "Không tìm thấy khung giờ." });

            var (ok, _) = await CanManageAsync(slot.JobPostingId, ct);
            if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền xoá khung giờ này." });

            if (slot.BookedCount > 0)
                return BadRequest(new { message = "Không thể xoá khung giờ đã có ứng viên đặt lịch." });

            _unitOfWork.Repository<AvailabilitySlot>().Delete(slot);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new { message = "Đã xoá khung giờ.", id });
        }

        /// <summary>Cập nhật sức chứa của khung giờ (không nhỏ hơn số đã đặt).</summary>
        [HttpPatch("slots/{id:guid}/capacity")]
        public async Task<IActionResult> UpdateCapacity(Guid id, [FromBody] UpdateSlotCapacityRequest request, CancellationToken ct)
        {
            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(id, ct);
            if (slot == null) return NotFound(new { message = "Không tìm thấy khung giờ." });

            var (ok, _) = await CanManageAsync(slot.JobPostingId, ct);
            if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền sửa khung giờ này." });

            if (request.Capacity < 1)
                return BadRequest(new { message = "Sức chứa tối thiểu là 1." });
            if (request.Capacity < slot.BookedCount)
                return BadRequest(new { message = $"Sức chứa không được nhỏ hơn số đã đặt ({slot.BookedCount})." });

            slot.Capacity = request.Capacity;
            slot.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<AvailabilitySlot>().Update(slot);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(AvailabilitySlotResponse.FromEntity(slot));
        }
    }
}
