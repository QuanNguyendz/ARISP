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
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Scheduling
{
    // ============================================================
    // GET /api/schedules/slots — danh sách slot của một job (staff)
    // ============================================================

    public record GetAvailabilitySlotsQuery(Guid JobPostingId, int? Round, Guid? UserId, string? Role)
        : IRequest<Result<List<AvailabilitySlotResponse>>>;

    public class GetAvailabilitySlotsQueryHandler : IRequestHandler<GetAvailabilitySlotsQuery, Result<List<AvailabilitySlotResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAvailabilitySlotsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<AvailabilitySlotResponse>>> Handle(GetAvailabilitySlotsQuery request, CancellationToken ct)
        {
            if (request.JobPostingId == Guid.Empty)
                return Result.Failure<List<AvailabilitySlotResponse>>("jobPostingId là bắt buộc.");

            var (ok, job) = await SchedulingSupport.CanManageAsync(_unitOfWork, request.JobPostingId, request.UserId, request.Role, ct);
            if (job == null) return Result.Failure<List<AvailabilitySlotResponse>>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<List<AvailabilitySlotResponse>>("Bạn không có quyền xem lịch của tin này.", CommonErrorCodes.Forbidden);

            var slots = await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(
                s => s.JobPostingId == request.JobPostingId && (!request.Round.HasValue || s.RoundNumber == request.Round.Value), ct);

            return Result.Success(slots.OrderBy(s => s.StartTime).Select(AvailabilitySlotResponse.FromEntity).ToList());
        }
    }

    // ============================================================
    // POST /api/schedules/slots — tạo khung giờ
    // ============================================================

    public record CreateSlotCommand(CreateSlotRequest Request, Guid? UserId, string? Role)
        : IRequest<Result<AvailabilitySlotResponse>>;

    public class CreateSlotCommandHandler : IRequestHandler<CreateSlotCommand, Result<AvailabilitySlotResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateSlotCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<AvailabilitySlotResponse>> Handle(CreateSlotCommand command, CancellationToken ct)
        {
            var request = command.Request;

            if (request.JobPostingId == Guid.Empty)
                return Result.Failure<AvailabilitySlotResponse>("jobPostingId là bắt buộc.");
            if (request.EndTime <= request.StartTime)
                return Result.Failure<AvailabilitySlotResponse>("Giờ kết thúc phải sau giờ bắt đầu.");
            if (request.StartTime <= DateTimeOffset.UtcNow)
                return Result.Failure<AvailabilitySlotResponse>("Khung giờ phải nằm trong tương lai.");
            if (request.Capacity < 1)
                return Result.Failure<AvailabilitySlotResponse>("Sức chứa (capacity) tối thiểu là 1.");
            if (request.RoundNumber < 1)
                return Result.Failure<AvailabilitySlotResponse>("RoundNumber phải >= 1.");

            var (ok, job) = await SchedulingSupport.CanManageAsync(_unitOfWork, request.JobPostingId, command.UserId, command.Role, ct);
            if (job == null) return Result.Failure<AvailabilitySlotResponse>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<AvailabilitySlotResponse>("Bạn không có quyền tạo lịch cho tin này.", CommonErrorCodes.Forbidden);

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

            return Result.Success(AvailabilitySlotResponse.FromEntity(slot));
        }
    }

    // ============================================================
    // DELETE /api/schedules/slots/{id} — xoá khung giờ (chưa ai đặt)
    // ============================================================

    public record DeleteSlotCommand(Guid Id, Guid? UserId, string? Role) : IRequest<Result>;

    public class DeleteSlotCommandHandler : IRequestHandler<DeleteSlotCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeleteSlotCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(DeleteSlotCommand command, CancellationToken ct)
        {
            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(command.Id, ct);
            if (slot == null) return Result.Failure("Không tìm thấy khung giờ.", CommonErrorCodes.NotFound);

            var (ok, _) = await SchedulingSupport.CanManageAsync(_unitOfWork, slot.JobPostingId, command.UserId, command.Role, ct);
            if (!ok) return Result.Failure("Bạn không có quyền xoá khung giờ này.", CommonErrorCodes.Forbidden);

            if (slot.BookedCount > 0)
                return Result.Failure("Không thể xoá khung giờ đã có ứng viên đặt lịch.");

            _unitOfWork.Repository<AvailabilitySlot>().Delete(slot);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    // ============================================================
    // PATCH /api/schedules/slots/{id}/capacity — sửa sức chứa
    // ============================================================

    public record UpdateSlotCapacityCommand(Guid Id, int Capacity, Guid? UserId, string? Role)
        : IRequest<Result<AvailabilitySlotResponse>>;

    public class UpdateSlotCapacityCommandHandler : IRequestHandler<UpdateSlotCapacityCommand, Result<AvailabilitySlotResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateSlotCapacityCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<AvailabilitySlotResponse>> Handle(UpdateSlotCapacityCommand command, CancellationToken ct)
        {
            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(command.Id, ct);
            if (slot == null) return Result.Failure<AvailabilitySlotResponse>("Không tìm thấy khung giờ.", CommonErrorCodes.NotFound);

            var (ok, _) = await SchedulingSupport.CanManageAsync(_unitOfWork, slot.JobPostingId, command.UserId, command.Role, ct);
            if (!ok) return Result.Failure<AvailabilitySlotResponse>("Bạn không có quyền sửa khung giờ này.", CommonErrorCodes.Forbidden);

            if (command.Capacity < 1)
                return Result.Failure<AvailabilitySlotResponse>("Sức chứa tối thiểu là 1.");
            if (command.Capacity < slot.BookedCount)
                return Result.Failure<AvailabilitySlotResponse>($"Sức chứa không được nhỏ hơn số đã đặt ({slot.BookedCount}).");

            slot.Capacity = command.Capacity;
            slot.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<AvailabilitySlot>().Update(slot);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(AvailabilitySlotResponse.FromEntity(slot));
        }
    }

    // ============================================================
    // POST /api/schedules/assign — HR gán cứng 1 khung giờ cho 1 ứng viên (ADR-048)
    // Thay cho luồng ứng viên tự chọn: staff chọn slot trong kho rồi ấn định cho hồ sơ.
    // ============================================================

    public record AssignSlotResultDto(Guid BookingId, AvailabilitySlotResponse Slot);

    public record AssignSlotCommand(Guid ApplicationId, Guid SlotId, int Round, Guid? UserId, string? Role)
        : IRequest<Result<AssignSlotResultDto>>;

    public class AssignSlotCommandHandler : IRequestHandler<AssignSlotCommand, Result<AssignSlotResultDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _configuration;

        public AssignSlotCommandHandler(
            IUnitOfWork unitOfWork, INotificationService notificationService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _configuration = configuration;
        }

        public async Task<Result<AssignSlotResultDto>> Handle(AssignSlotCommand request, CancellationToken ct)
        {
            var round = request.Round > 0 ? request.Round : 1;
            var applicationId = request.ApplicationId;

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (app == null) return Result.Failure<AssignSlotResultDto>("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);

            // Chỉ xếp lịch khi ứng viên đã qua vòng duyệt CV (>= screening). Không gán khi mới nộp/bị từ chối.
            var status = (app.Status ?? string.Empty).ToLowerInvariant();
            if (status is "cv_submitted" or "invited" or "cv_rejected" or "not_pass" or "rejected")
                return Result.Failure<AssignSlotResultDto>("Chỉ có thể xếp lịch khi ứng viên đã qua vòng duyệt CV. Hãy gửi lời mời phỏng vấn trước.");

            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(request.SlotId, ct);
            if (slot == null) return Result.Failure<AssignSlotResultDto>("Không tìm thấy khung giờ.", CommonErrorCodes.NotFound);

            // Quyền quản lý slot = quyền quản lý job của slot (chủ tin hoặc admin).
            var (ok, _) = await SchedulingSupport.CanManageAsync(_unitOfWork, slot.JobPostingId, request.UserId, request.Role, ct);
            if (!ok) return Result.Failure<AssignSlotResultDto>("Bạn không có quyền xếp lịch cho tin này.", CommonErrorCodes.Forbidden);

            if (slot.JobPostingId != app.JobPostingId || slot.RoundNumber != round)
                return Result.Failure<AssignSlotResultDto>("Khung giờ không thuộc tin tuyển dụng/vòng phỏng vấn của ứng viên này.");
            if (slot.StartTime <= DateTimeOffset.UtcNow)
                return Result.Failure<AssignSlotResultDto>("Khung giờ đã ở quá khứ.");

            // Đã có lịch cho vòng này rồi? (một booking "scheduled" / vòng)
            var existing = await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.ApplicationId == applicationId && b.RoundNumber == round && b.Status == "scheduled", ct);
            if (existing.Any())
                return Result.Failure<AssignSlotResultDto>("Ứng viên đã có lịch cho vòng này. Hãy huỷ lịch cũ trước khi gán lại.");

            // Chốt chỗ NGUYÊN TỬ chống overbooking (DB row-lock 1 câu lệnh, không mutate entity đang được EF theo dõi).
            var incremented = await _unitOfWork.ExecuteSqlRawAsync(
                "UPDATE availability_slots SET booked_count = booked_count + 1, updated_at = {0} WHERE id = {1} AND booked_count < capacity",
                new object[] { DateTimeOffset.UtcNow, slot.Id }, ct);
            if (incremented == 0)
                return Result.Failure<AssignSlotResultDto>("Khung giờ đã đầy. Vui lòng chọn khung giờ khác hoặc tăng sức chứa.");

            // Nếu đây là lần xếp lại sau khi ứng viên từ chối, liên kết về booking đã từ chối gần nhất.
            var priorDeclined = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => b.ApplicationId == applicationId && b.RoundNumber == round && b.Status == "declined", ct))
                .OrderByDescending(b => b.RespondedAt ?? b.UpdatedAt).FirstOrDefault();

            var booking = new InterviewBooking
            {
                ApplicationId = applicationId,
                AvailabilitySlotId = slot.Id,
                RoundNumber = round,
                Status = "scheduled",
                ConfirmationStatus = "pending",
                RescheduledFromId = priorDeclined?.Id,
            };
            await _unitOfWork.Repository<InterviewBooking>().AddAsync(booking, ct);

            // Đã xếp lịch buổi thật → chuyển "screening" (đang sàng lọc) sang "interview". Vòng 2+ vốn đã ở "interview".
            if (string.Equals(app.Status, "screening", StringComparison.OrdinalIgnoreCase))
            {
                app.Status = "interview";
                _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);
            }

            // Đánh dấu lời mời của vòng đã có lịch (nếu có).
            var invites = await _unitOfWork.Repository<InterviewInvite>().FindAsync(
                i => i.ApplicationId == applicationId && i.RoundNumber == round && i.ScheduledAt == null, ct);
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
                // Bù trừ chỗ đã chiếm nếu lưu thất bại (vd trùng vòng do double-click — chặn bởi unique index).
                await _unitOfWork.ExecuteSqlRawAsync(
                    "UPDATE availability_slots SET booked_count = GREATEST(booked_count - 1, 0), updated_at = {0} WHERE id = {1}",
                    new object[] { DateTimeOffset.UtcNow, slot.Id }, ct);
                return Result.Failure<AssignSlotResultDto>("Không thể hoàn tất xếp lịch (có thể ứng viên đã có lịch vòng này). Vui lòng tải lại và thử lại.");
            }

            var slotDto = AvailabilitySlotResponse.FromEntity(slot);
            slotDto.BookedCount += 1;

            // Giờ hiển thị theo múi giờ VN (+7) cho notification/email.
            var local = slot.StartTime.ToOffset(TimeSpan.FromHours(7));
            var whenText = $"{local:HH:mm} ngày {local:dd/MM/yyyy} (giờ VN)";

            // Thông báo ứng viên: DB notification (bell) + realtime.
            if (app.CandidateAccountId.HasValue)
            {
                var notifRepo = _unitOfWork.Repository<ARI.Domain.Entities.Notification>();
                // Dedup theo booking.Id: mỗi lần xếp/xếp-lại là booking mới → luôn thông báo lại.
                var dedupKey = $"schedule_assigned:{booking.Id}";
                var already = await notifRepo.FindAsync(
                    n => n.CandidateAccountId == app.CandidateAccountId.Value && n.DedupKey == dedupKey, ct);
                if (!already.Any())
                {
                    await notifRepo.AddAsync(new ARI.Domain.Entities.Notification
                    {
                        CandidateAccountId = app.CandidateAccountId.Value,
                        DedupKey = dedupKey,
                        Type = "schedule",
                        Title = "Lịch phỏng vấn đã được xếp",
                        Body = $"Nhân sự đã xếp lịch phỏng vấn (vòng {round}) cho bạn: {whenText}. Vui lòng XÁC NHẬN nếu bạn tham dự được, hoặc báo bận kèm lý do để được xếp lịch khác.",
                        Link = $"/portal/schedule/{applicationId}",
                        IsRead = false
                    }, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                await _notificationService.PublishUserEventAsync(app.CandidateAccountId.Value, "ReceiveUserNotification",
                    new { Type = "InterviewScheduled", ApplicationId = applicationId, RoundNumber = round }, ct);
            }

            // Thư mời phỏng vấn kèm lịch (best-effort — không chặn kết quả nếu gửi lỗi).
            // Nội dung dựng ở InterviewInviteEmail: gộp (1) kết quả vòng trước / qua vòng CV,
            // (2) giờ hẹn + địa điểm, (3) quy trình theo vòng, (4) 2 nút Xác nhận / Đổi lịch (ADR-048).
            try
            {
                var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
                var mail = await InterviewInviteEmail.BuildAsync(
                    _unitOfWork, _configuration, app, job, round, booking.Id, slot.StartTime, ct);
                await _notificationService.SendEmailAsync(app.CandidateEmail, mail.Subject, mail.Html, ct);
            }
            catch { /* best-effort */ }

            return Result.Success(new AssignSlotResultDto(booking.Id, slotDto));
        }
    }
}
