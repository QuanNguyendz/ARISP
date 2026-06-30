using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using ARISP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARISP.API.Controllers
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public CandidateScheduleController(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        /// <summary>Xác thực quyền truy cập hồ sơ: token lời mời hợp lệ hoặc candidate đăng nhập sở hữu hồ sơ.</summary>
        private async Task<(bool ok, ARISP.Domain.Entities.Application? app, string? error)> AuthorizeAsync(
            Guid applicationId, int round, string? token, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (app == null) return (false, null, "Không tìm thấy hồ sơ ứng tuyển.");

            // 1) Token lời mời
            if (!string.IsNullOrWhiteSpace(token))
            {
                var hash = ApplicationService.HashInviteToken(token);
                var invites = await _unitOfWork.Repository<InterviewInvite>().FindAsync(
                    i => i.ApplicationId == applicationId && i.RoundNumber == round && i.TokenHash == hash, ct);
                var invite = invites.FirstOrDefault();
                if (invite != null && invite.ExpiresAt > DateTimeOffset.UtcNow)
                    return (true, app, null);
                if (invite != null) return (false, app, "Lời mời đã hết hạn. Vui lòng liên hệ nhân sự để được gửi lại.");
            }

            // 2) Candidate đăng nhập sở hữu hồ sơ
            if (User?.Identity?.IsAuthenticated == true)
            {
                var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                               ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
                var byAccount = Guid.TryParse(subClaim, out var accId) && app.CandidateAccountId == accId;
                var byEmail = !string.IsNullOrEmpty(emailClaim) &&
                              string.Equals(app.CandidateEmail, emailClaim, StringComparison.OrdinalIgnoreCase);
                if (byAccount || byEmail) return (true, app, null);
            }

            return (false, app, "Bạn không có quyền truy cập lịch của hồ sơ này (thiếu token hợp lệ hoặc chưa đăng nhập).");
        }

        /// <summary>Khung giờ còn trống của hồ sơ cho một vòng (để ứng viên chọn).</summary>
        [HttpGet("schedule/{applicationId:guid}/slots")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOpenSlots(Guid applicationId, [FromQuery] int round, [FromQuery] string? token, CancellationToken ct)
        {
            var roundNumber = round > 0 ? round : 1;
            var (ok, app, error) = await AuthorizeAsync(applicationId, roundNumber, token, ct);
            if (app == null) return NotFound(new { message = error });
            if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new { message = error });

            var now = DateTimeOffset.UtcNow;
            var slots = await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(
                s => s.JobPostingId == app.JobPostingId && s.RoundNumber == roundNumber
                     && s.StartTime > now && s.BookedCount < s.Capacity, ct);

            var result = slots.OrderBy(s => s.StartTime).Select(AvailabilitySlotResponse.FromEntity);
            return Ok(result);
        }

        /// <summary>Đặt một khung giờ phỏng vấn cho hồ sơ + vòng.</summary>
        [HttpPost("schedule/{applicationId:guid}/book")]
        [AllowAnonymous]
        public async Task<IActionResult> Book(Guid applicationId, [FromBody] BookSlotRequest request, CancellationToken ct)
        {
            var roundNumber = request.Round > 0 ? request.Round : 1;
            var (ok, app, error) = await AuthorizeAsync(applicationId, roundNumber, request.Token, ct);
            if (app == null) return NotFound(new { message = error });
            if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new { message = error });

            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(request.SlotId, ct);
            if (slot == null) return NotFound(new { message = "Không tìm thấy khung giờ." });
            if (slot.JobPostingId != app.JobPostingId || slot.RoundNumber != roundNumber)
                return BadRequest(new { message = "Khung giờ không thuộc vòng phỏng vấn này." });
            if (slot.StartTime <= DateTimeOffset.UtcNow)
                return BadRequest(new { message = "Khung giờ đã ở quá khứ." });
            if (slot.BookedCount >= slot.Capacity)
                return BadRequest(new { message = "Khung giờ đã đầy. Vui lòng chọn khung giờ khác." });

            // Đã đặt lịch vòng này rồi?
            var existing = await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.ApplicationId == applicationId && b.RoundNumber == roundNumber && b.Status == "scheduled", ct);
            if (existing.Any())
                return BadRequest(new { message = "Bạn đã đặt lịch cho vòng này rồi." });

            // Không trùng khung giờ với booking khác của chính ứng viên (kể cả JD khác).
            var myAppIds = (await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(
                    a => (app.CandidateAccountId != null && a.CandidateAccountId == app.CandidateAccountId)
                         || a.CandidateEmail == app.CandidateEmail, ct))
                .Select(a => a.Id).ToHashSet();

            var myBookings = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => myAppIds.Contains(b.ApplicationId) && b.Status == "scheduled", ct)).ToList();

            if (myBookings.Count > 0)
            {
                var bookedSlotIds = myBookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
                var bookedSlots = await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(
                    s => bookedSlotIds.Contains(s.Id), ct);
                var conflict = bookedSlots.Any(s => slot.StartTime < s.EndTime && s.StartTime < slot.EndTime);
                if (conflict)
                    return BadRequest(new { message = "Bạn đã có một buổi phỏng vấn khác trùng khung giờ này. Vui lòng chọn giờ khác." });
            }

            // Chốt chỗ NGUYÊN TỬ chống overbooking: chỉ tăng khi còn chỗ (DB row-lock 1 câu lệnh).
            // Tránh race 2 ứng viên cùng giành slot cuối. Không mutate entity slot đang được EF theo dõi
            // (để SaveChanges không ghi đè đếm lần nữa).
            var incremented = await _unitOfWork.ExecuteSqlRawAsync(
                "UPDATE availability_slots SET booked_count = booked_count + 1, updated_at = {0} WHERE id = {1} AND booked_count < capacity",
                new object[] { DateTimeOffset.UtcNow, slot.Id }, ct);
            if (incremented == 0)
                return BadRequest(new { message = "Khung giờ vừa được đặt hết. Vui lòng chọn khung giờ khác." });

            var booking = new InterviewBooking
            {
                ApplicationId = applicationId,
                AvailabilitySlotId = slot.Id,
                RoundNumber = roundNumber,
                Status = "scheduled",
            };
            await _unitOfWork.Repository<InterviewBooking>().AddAsync(booking, ct);

            var invites = await _unitOfWork.Repository<InterviewInvite>().FindAsync(
                i => i.ApplicationId == applicationId && i.RoundNumber == roundNumber && i.ScheduledAt == null, ct);
            foreach (var inv in invites)
            {
                inv.ScheduledAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<InterviewInvite>().Update(inv);
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                // Bù trừ chỗ đã chiếm nếu lưu booking thất bại (vd trùng vòng do double-click —
                // chặn bởi unique index một-booking-scheduled/vòng).
                await _unitOfWork.ExecuteSqlRawAsync(
                    "UPDATE availability_slots SET booked_count = GREATEST(booked_count - 1, 0), updated_at = {0} WHERE id = {1}",
                    new object[] { DateTimeOffset.UtcNow, slot.Id }, ct);
                return BadRequest(new { message = "Không thể hoàn tất đặt lịch (có thể bạn đã đặt vòng này). Vui lòng tải lại và thử lại." });
            }

            // DTO phản ánh lần đặt vừa rồi (DTO độc lập, không bị EF theo dõi).
            var slotDto = AvailabilitySlotResponse.FromEntity(slot);
            slotDto.BookedCount += 1;

            // Notify Recruiter
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            if (job != null)
            {
                await _notificationService.PublishUserEventAsync(job.CreatedByUserId, "ReceiveSystemEvent", new { 
                    Type = "SlotBooked", 
                    ApplicationId = applicationId, 
                    JobId = job.Id, 
                    SlotId = slot.Id 
                }, ct);

                // Notify all candidates viewing the schedule to refresh their open slots
                await _notificationService.PublishGroupEventAsync("candidate", "ReceiveSystemEvent", new { 
                    Type = "SlotBooked", 
                    ApplicationId = applicationId,
                    SlotId = request.SlotId 
                }, ct);
            }

            return Ok(new
            {
                message = "Đặt lịch thành công. Bạn có thể luyện tập với phỏng vấn thử trước ngày hẹn.",
                bookingId = booking.Id,
                slot = slotDto,
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

            var myAppIds = (await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().FindAsync(
                    a => (accId != Guid.Empty && a.CandidateAccountId == accId)
                         || (emailClaim != null && a.CandidateEmail == emailClaim), ct))
                .Select(a => a.Id).ToHashSet();

            var upcoming = new List<AvailabilitySlotResponse>();
            var past = new List<AvailabilitySlotResponse>();

            if (myAppIds.Count > 0)
            {
                var bookings = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                        b => myAppIds.Contains(b.ApplicationId) && b.Status == "scheduled", ct)).ToList();
                var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
                if (slotIds.Count > 0)
                {
                    var slots = await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id), ct);
                    var now = DateTimeOffset.UtcNow;
                    foreach (var s in slots.OrderBy(s => s.StartTime))
                    {
                        var dto = AvailabilitySlotResponse.FromEntity(s);
                        (s.StartTime >= now ? upcoming : past).Add(dto);
                    }
                }
            }

            return Ok(new { upcomingSlots = upcoming, pastSlots = past });
        }
    }
}
