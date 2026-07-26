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
    // GET /api/candidate/schedule — lịch của ứng viên đang đăng nhập
    // (Ứng viên KHÔNG tự chọn lịch — HR gán trực tiếp từ kho slot, ADR-048.
    //  Ứng viên chỉ được XÁC NHẬN lịch hoặc TỪ CHỐI kèm lý do để nhân sự xếp lại.)
    // ============================================================

    public record CandidateScheduleDto(
        List<CandidateScheduleItemDto> Upcoming,
        List<CandidateScheduleItemDto> Past,
        List<CandidateScheduleItemDto> AwaitingReschedule);

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

            var myApps = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>().FindAsync(
                    a => (accId != Guid.Empty && a.CandidateAccountId == accId)
                         || (emailClaim != null && a.CandidateEmail == emailClaim), ct))
                .ToList();

            var upcoming = new List<CandidateScheduleItemDto>();
            var past = new List<CandidateScheduleItemDto>();
            var awaiting = new List<CandidateScheduleItemDto>();

            if (myApps.Count == 0)
                return Result.Success(new CandidateScheduleDto(upcoming, past, awaiting));

            var appById = myApps.ToDictionary(a => a.Id);
            var myAppIds = appById.Keys.ToHashSet();

            // Booking còn hiệu lực (scheduled) + booking bị từ chối gần đây (chờ xếp lại).
            var since = DateTimeOffset.UtcNow.AddDays(-30);
            var bookings = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => myAppIds.Contains(b.ApplicationId)
                         && (b.Status == "scheduled" || (b.Status == "declined" && b.RespondedAt >= since)), ct))
                .ToList();
            if (bookings.Count == 0)
                return Result.Success(new CandidateScheduleDto(upcoming, past, awaiting));

            var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slotById = (await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id), ct))
                .ToDictionary(s => s.Id);

            // Nhãn vị trí theo job.
            var jobIds = myApps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobTitle = (await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id)).Select(j => new { j.Id, j.Title }), ct))
                .ToDictionary(j => j.Id, j => j.Title);

            // Vòng đang có lịch scheduled — để bỏ qua booking bị từ chối đã được xếp lại.
            var scheduledRounds = bookings
                .Where(b => b.Status == "scheduled")
                .Select(b => (b.ApplicationId, b.RoundNumber))
                .ToHashSet();

            var now = DateTimeOffset.UtcNow;

            CandidateScheduleItemDto? ToItem(InterviewBooking b)
            {
                if (!slotById.TryGetValue(b.AvailabilitySlotId, out var slot)) return null;
                appById.TryGetValue(b.ApplicationId, out var app);
                return new CandidateScheduleItemDto
                {
                    BookingId = b.Id,
                    ApplicationId = b.ApplicationId,
                    JobTitle = app != null && jobTitle.TryGetValue(app.JobPostingId, out var t) ? t : null,
                    RoundNumber = b.RoundNumber,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Timezone = slot.Timezone,
                    ConfirmationStatus = b.ConfirmationStatus,
                    DeclineReason = b.DeclineReason,
                };
            }

            foreach (var b in bookings.OrderBy(b => slotById.TryGetValue(b.AvailabilitySlotId, out var s) ? s.StartTime : DateTimeOffset.MaxValue))
            {
                var item = ToItem(b);
                if (item == null) continue;

                if (b.Status == "scheduled")
                {
                    (item.StartTime >= now ? upcoming : past).Add(item);
                }
                else if (b.Status == "declined" && !scheduledRounds.Contains((b.ApplicationId, b.RoundNumber)))
                {
                    // Đã từ chối và chưa được xếp lại → chờ nhân sự sắp lịch khác.
                    awaiting.Add(item);
                }
            }

            return Result.Success(new CandidateScheduleDto(upcoming, past, awaiting));
        }
    }

    // ============================================================
    // Ownership guard dùng chung cho confirm/decline.
    // ============================================================

    internal static class CandidateBookingSupport
    {
        /// <summary>Nạp booking + application, xác thực booking thuộc về ứng viên đang đăng nhập.</summary>
        public static async Task<(InterviewBooking? booking, ARI.Domain.Entities.Application? app, string? error, string? errorCode)>
            LoadOwnedAsync(IUnitOfWork uow, Guid bookingId, Guid accId, string? email, CancellationToken ct)
        {
            var booking = await uow.Repository<InterviewBooking>().GetByIdAsync(bookingId, ct);
            if (booking == null) return (null, null, "Không tìm thấy lịch phỏng vấn.", CommonErrorCodes.NotFound);

            var app = await uow.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(booking.ApplicationId, ct);
            if (app == null) return (null, null, "Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);

            var owns = (accId != Guid.Empty && app.CandidateAccountId == accId)
                       || (!string.IsNullOrEmpty(email) && string.Equals(app.CandidateEmail, email, StringComparison.OrdinalIgnoreCase));
            if (!owns) return (null, null, "Bạn không có quyền thao tác trên lịch này.", CommonErrorCodes.Forbidden);

            return (booking, app, null, null);
        }

        /// <summary>Đẩy realtime cho nhân sự (chủ tin + nhóm HR admin) khi ứng viên phản hồi lịch.</summary>
        public static async Task NotifyStaffAsync(
            IUnitOfWork uow, INotificationService notif, ARI.Domain.Entities.Application app,
            InterviewBooking booking, string response, CancellationToken ct)
        {
            try
            {
                var job = await uow.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
                var payload = new
                {
                    Type = "ScheduleResponse",
                    applicationId = app.Id,
                    jobPostingId = app.JobPostingId,
                    candidateName = app.CandidateName,
                    roundNumber = booking.RoundNumber,
                    response, // confirmed | declined
                    reason = booking.DeclineReason,
                };
                if (job != null)
                    await notif.PublishUserEventAsync(job.CreatedByUserId, "ReceiveScheduleResponse", payload, ct);
                await notif.PublishGroupEventAsync("hr_admin", "ReceiveScheduleResponse", payload, ct);
            }
            catch { /* best-effort — không chặn phản hồi của ứng viên */ }
        }
    }

    // ============================================================
    // POST /api/candidate/schedule/{bookingId}/confirm — ứng viên xác nhận lịch
    // ============================================================

    public record ConfirmScheduleCommand(Guid BookingId, Guid AccountId, string? Email) : IRequest<Result>;

    public class ConfirmScheduleCommandHandler : IRequestHandler<ConfirmScheduleCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public ConfirmScheduleCommandHandler(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        public async Task<Result> Handle(ConfirmScheduleCommand request, CancellationToken ct)
        {
            var (booking, app, error, errorCode) = await CandidateBookingSupport.LoadOwnedAsync(
                _unitOfWork, request.BookingId, request.AccountId, request.Email, ct);
            if (booking == null || app == null) return Result.Failure(error!, errorCode!);

            if (booking.Status != "scheduled")
                return Result.Failure("Lịch này không còn hiệu lực để xác nhận.");

            // Idempotent: đã xác nhận rồi thì trả thành công.
            if (!string.Equals(booking.ConfirmationStatus, "confirmed", StringComparison.OrdinalIgnoreCase))
            {
                booking.ConfirmationStatus = "confirmed";
                booking.DeclineReason = null;
                booking.RespondedAt = DateTimeOffset.UtcNow;
                booking.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<InterviewBooking>().Update(booking);
                await _unitOfWork.SaveChangesAsync(ct);

                await CandidateBookingSupport.NotifyStaffAsync(_unitOfWork, _notificationService, app, booking, "confirmed", ct);
            }

            return Result.Success();
        }
    }

    // ============================================================
    // POST /api/candidate/schedule/{bookingId}/decline — ứng viên bận, từ chối kèm lý do
    // ============================================================

    public record DeclineScheduleCommand(Guid BookingId, string Reason, Guid AccountId, string? Email) : IRequest<Result>;

    public class DeclineScheduleCommandHandler : IRequestHandler<DeclineScheduleCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public DeclineScheduleCommandHandler(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        public async Task<Result> Handle(DeclineScheduleCommand request, CancellationToken ct)
        {
            var reason = (request.Reason ?? string.Empty).Trim();
            if (reason.Length < 3)
                return Result.Failure("Vui lòng nhập lý do bạn không thể tham dự để nhân sự xếp lịch khác.");
            if (reason.Length > 500)
                reason = reason[..500];

            var (booking, app, error, errorCode) = await CandidateBookingSupport.LoadOwnedAsync(
                _unitOfWork, request.BookingId, request.AccountId, request.Email, ct);
            if (booking == null || app == null) return Result.Failure(error!, errorCode!);

            if (booking.Status != "scheduled")
                return Result.Failure("Lịch này không còn hiệu lực để từ chối.");

            booking.ConfirmationStatus = "declined";
            booking.DeclineReason = reason;
            booking.RespondedAt = DateTimeOffset.UtcNow;
            // Trả chỗ cho khung giờ + gỡ khỏi unique index 'scheduled' để nhân sự có thể gán lịch mới.
            booking.Status = "declined";
            booking.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<InterviewBooking>().Update(booking);
            await _unitOfWork.SaveChangesAsync(ct);

            // Giải phóng 1 chỗ đã chiếm ở khung giờ (best-effort, không âm).
            try
            {
                await _unitOfWork.ExecuteSqlRawAsync(
                    "UPDATE availability_slots SET booked_count = GREATEST(booked_count - 1, 0), updated_at = {0} WHERE id = {1}",
                    new object[] { DateTimeOffset.UtcNow, booking.AvailabilitySlotId }, ct);
            }
            catch { /* best-effort */ }

            await CandidateBookingSupport.NotifyStaffAsync(_unitOfWork, _notificationService, app, booking, "declined", ct);

            return Result.Success();
        }
    }
}
