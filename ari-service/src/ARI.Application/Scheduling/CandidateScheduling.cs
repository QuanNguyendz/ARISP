using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Scheduling
{
    // ============================================================
    // GET /api/schedule/{applicationId}/slots — khung giờ còn trống (ứng viên chọn)
    // ============================================================

    public record GetOpenSlotsQuery(Guid ApplicationId, int Round, string? Token, Guid? AccountId, string? Email)
        : IRequest<Result<List<AvailabilitySlotResponse>>>;

    public class GetOpenSlotsQueryHandler : IRequestHandler<GetOpenSlotsQuery, Result<List<AvailabilitySlotResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetOpenSlotsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<AvailabilitySlotResponse>>> Handle(GetOpenSlotsQuery request, CancellationToken ct)
        {
            var (ok, app, error) = await SchedulingSupport.AuthorizeCandidateAsync(
                _unitOfWork, request.ApplicationId, request.Round, request.Token, request.AccountId, request.Email, ct);
            if (app == null) return Result.Failure<List<AvailabilitySlotResponse>>(error!, CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<List<AvailabilitySlotResponse>>(error!, CommonErrorCodes.Forbidden);

            var now = DateTimeOffset.UtcNow;
            var slots = await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(
                s => s.JobPostingId == app.JobPostingId && s.RoundNumber == request.Round
                     && s.StartTime > now && s.BookedCount < s.Capacity, ct);

            return Result.Success(slots.OrderBy(s => s.StartTime).Select(AvailabilitySlotResponse.FromEntity).ToList());
        }
    }

    // ============================================================
    // POST /api/schedule/{applicationId}/book — đặt khung giờ
    // ============================================================

    public record BookSlotResultDto(Guid BookingId, AvailabilitySlotResponse Slot);

    public record BookSlotCommand(Guid ApplicationId, Guid SlotId, int Round, string? Token, Guid? AccountId, string? Email)
        : IRequest<Result<BookSlotResultDto>>;

    public class BookSlotCommandHandler : IRequestHandler<BookSlotCommand, Result<BookSlotResultDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public BookSlotCommandHandler(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        public async Task<Result<BookSlotResultDto>> Handle(BookSlotCommand request, CancellationToken ct)
        {
            var roundNumber = request.Round;
            var applicationId = request.ApplicationId;

            var (ok, app, error) = await SchedulingSupport.AuthorizeCandidateAsync(
                _unitOfWork, applicationId, roundNumber, request.Token, request.AccountId, request.Email, ct);
            if (app == null) return Result.Failure<BookSlotResultDto>(error!, CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<BookSlotResultDto>(error!, CommonErrorCodes.Forbidden);

            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(request.SlotId, ct);
            if (slot == null) return Result.Failure<BookSlotResultDto>("Không tìm thấy khung giờ.", CommonErrorCodes.NotFound);
            if (slot.JobPostingId != app.JobPostingId || slot.RoundNumber != roundNumber)
                return Result.Failure<BookSlotResultDto>("Khung giờ không thuộc vòng phỏng vấn này.");
            if (slot.StartTime <= DateTimeOffset.UtcNow)
                return Result.Failure<BookSlotResultDto>("Khung giờ đã ở quá khứ.");
            if (slot.BookedCount >= slot.Capacity)
                return Result.Failure<BookSlotResultDto>("Khung giờ đã đầy. Vui lòng chọn khung giờ khác.");

            // Đã đặt lịch vòng này rồi?
            var existing = await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.ApplicationId == applicationId && b.RoundNumber == roundNumber && b.Status == "scheduled", ct);
            if (existing.Any())
                return Result.Failure<BookSlotResultDto>("Bạn đã đặt lịch cho vòng này rồi.");

            // Không trùng khung giờ với booking khác của chính ứng viên (kể cả JD khác).
            var myAppIds = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>().FindAsync(
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
                    return Result.Failure<BookSlotResultDto>("Bạn đã có một buổi phỏng vấn khác trùng khung giờ này. Vui lòng chọn giờ khác.");
            }

            // Chốt chỗ NGUYÊN TỬ chống overbooking: chỉ tăng khi còn chỗ (DB row-lock 1 câu lệnh).
            // Tránh race 2 ứng viên cùng giành slot cuối. Không mutate entity slot đang được EF theo dõi
            // (để SaveChanges không ghi đè đếm lần nữa).
            var incremented = await _unitOfWork.ExecuteSqlRawAsync(
                "UPDATE availability_slots SET booked_count = booked_count + 1, updated_at = {0} WHERE id = {1} AND booked_count < capacity",
                new object[] { DateTimeOffset.UtcNow, slot.Id }, ct);
            if (incremented == 0)
                return Result.Failure<BookSlotResultDto>("Khung giờ vừa được đặt hết. Vui lòng chọn khung giờ khác.");

            var booking = new InterviewBooking
            {
                ApplicationId = applicationId,
                AvailabilitySlotId = slot.Id,
                RoundNumber = roundNumber,
                Status = "scheduled",
            };
            await _unitOfWork.Repository<InterviewBooking>().AddAsync(booking, ct);

            // Đặt lịch buổi phỏng vấn thật = ứng viên đã vào giai đoạn phỏng vấn thật → chuyển
            // "screening" (đang sàng lọc) sang "interview" (đang phỏng vấn). Nhờ vậy status KHÔNG
            // còn là "sàng lọc" khi đã có lịch/mã On-site (ADR-015). Vòng 2+ vốn đã ở "interview".
            if (string.Equals(app.Status, "screening", StringComparison.OrdinalIgnoreCase))
            {
                app.Status = "interview";
                _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);
            }

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
                return Result.Failure<BookSlotResultDto>("Không thể hoàn tất đặt lịch (có thể bạn đã đặt vòng này). Vui lòng tải lại và thử lại.");
            }

            // DTO phản ánh lần đặt vừa rồi (DTO độc lập, không bị EF theo dõi).
            var slotDto = AvailabilitySlotResponse.FromEntity(slot);
            slotDto.BookedCount += 1;

            // Notify Recruiter
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            if (job != null)
            {
                await _notificationService.PublishUserEventAsync(job.CreatedByUserId, "ReceiveSystemEvent", new
                {
                    Type = "SlotBooked",
                    ApplicationId = applicationId,
                    JobId = job.Id,
                    SlotId = slot.Id
                }, ct);

                // Notify all candidates viewing the schedule to refresh their open slots
                await _notificationService.PublishGroupEventAsync("candidate", "ReceiveSystemEvent", new
                {
                    Type = "SlotBooked",
                    ApplicationId = applicationId,
                    SlotId = request.SlotId
                }, ct);
            }

            return Result.Success(new BookSlotResultDto(booking.Id, slotDto));
        }
    }

    // ============================================================
    // GET /api/candidate/schedule — lịch của ứng viên đang đăng nhập
    // ============================================================

    public record CandidateScheduleDto(List<AvailabilitySlotResponse> UpcomingSlots, List<AvailabilitySlotResponse> PastSlots);

    public record GetCandidateScheduleQuery(Guid AccountId, string? Email) : IRequest<Result<CandidateScheduleDto>>;

    public class GetCandidateScheduleQueryHandler : IRequestHandler<GetCandidateScheduleQuery, Result<CandidateScheduleDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetCandidateScheduleQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<CandidateScheduleDto>> Handle(GetCandidateScheduleQuery request, CancellationToken ct)
        {
            var accId = request.AccountId;
            var emailClaim = request.Email;

            var myAppIds = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>().FindAsync(
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

            return Result.Success(new CandidateScheduleDto(upcoming, past));
        }
    }
}
