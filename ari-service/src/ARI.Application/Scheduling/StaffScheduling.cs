using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Emails;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
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

            var slots = (await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(
                    s => s.JobPostingId == request.JobPostingId
                         && (!request.Round.HasValue || s.RoundNumber == request.Round.Value), ct))
                .OrderBy(s => s.StartTime)
                .ToList();

            var result = slots.Select(AvailabilitySlotResponse.FromEntity).ToList();
            if (result.Count == 0) return Result.Success(result);

            // Ai đang giữ chỗ ở từng ca — tra MỘT lượt cho cả trang thay vì mỗi ca một truy vấn.
            // Chỉ booking `scheduled` mới là người thật sự đang giữ chỗ (vị từ duy nhất — ADR-058).
            var slotIds = slots.Select(s => s.Id).ToList();
            var allRows = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => slotIds.Contains(b.AvailabilitySlotId), ct))
                .ToList();

            var bookings = allRows
                .Where(b => string.Equals(b.Status, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var appIds = allRows.Select(b => b.ApplicationId).Distinct().ToList();
            var apps = appIds.Count == 0
                ? new List<ARI.Domain.Entities.Application>()
                : (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .FindAsync(a => appIds.Contains(a.Id), ct)).ToList();

            // Dòng đã đóng không chiếm chỗ nhưng làm ca không xoá được — giao diện phải nói trước
            // thay vì để người dùng bấm xoá rồi nhận lỗi, và nói RÕ là của ai, vì sao.
            foreach (var dto in result)
            {
                dto.ClosedBookings = allRows
                    .Where(b => b.AvailabilitySlotId == dto.Id
                                && !string.Equals(b.Status, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(b => b.UpdatedAt)
                    .Select(b =>
                    {
                        var app = apps.FirstOrDefault(a => a.Id == b.ApplicationId);
                        return new SlotClosedBookingDto
                        {
                            BookingId = b.Id,
                            ApplicationId = b.ApplicationId,
                            CandidateName = app?.CandidateName,
                            CandidateEmail = app?.CandidateEmail,
                            State = ARI.Application.Services.InterviewService.ResolveCandidateState(
                                b.Status, b.ConfirmationStatus, b.DeclinedBy),
                            Reason = b.DeclineReason,
                        };
                    })
                    .ToList();
                dto.ClosedBookingCount = dto.ClosedBookings.Count;
            }

            if (bookings.Count > 0)
            {
                foreach (var dto in result)
                {
                    dto.Bookings = bookings
                        .Where(b => b.AvailabilitySlotId == dto.Id)
                        .Select(b =>
                        {
                            var app = apps.FirstOrDefault(a => a.Id == b.ApplicationId);
                            return new SlotBookingBriefDto
                            {
                                BookingId = b.Id,
                                ApplicationId = b.ApplicationId,
                                CandidateName = app?.CandidateName,
                                CandidateEmail = app?.CandidateEmail,
                                ConfirmationStatus = b.ConfirmationStatus,
                            };
                        })
                        .ToList();
                }
            }

            return Result.Success(result);
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
            // Kiểm hình dạng request ngay cả với ca thi (server sẽ tự tính lại giờ kết thúc bên dưới):
            // giờ kết thúc trước giờ bắt đầu là request hỏng, không phải một cách khai ca thi.
            if (request.EndTime <= request.StartTime)
                return Result.Failure<AvailabilitySlotResponse>("Giờ kết thúc phải sau giờ bắt đầu.");
            if (request.StartTime <= DateTimeOffset.UtcNow)
                return Result.Failure<AvailabilitySlotResponse>("Khung giờ phải nằm trong tương lai.");
            if (request.RoundNumber < 1)
                return Result.Failure<AvailabilitySlotResponse>("RoundNumber phải >= 1.");

            var (ok, job) = await SchedulingSupport.CanManageAsync(_unitOfWork, request.JobPostingId, command.UserId, command.Role, ct);
            if (job == null) return Result.Failure<AvailabilitySlotResponse>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!ok) return Result.Failure<AvailabilitySlotResponse>("Bạn không có quyền tạo lịch cho tin này.", CommonErrorCodes.Forbidden);

            // Sức chứa và giờ kết thúc tuỳ LOẠI vòng, nên phải biết tin và vòng trước đã: vòng trắc
            // nghiệm mặc định không giới hạn và đóng theo thời lượng bài, vòng hội thoại đúng bằng 1
            // (ADR-067) và kết thúc theo giờ người dùng chọn.
            var round = await SchedulingSupport.RoundConfigAsync(
                _unitOfWork, request.JobPostingId, request.RoundNumber, ct);
            var (capacityError, capacity) = SchedulingSupport.ResolveCapacity(round?.RoundType, request.Capacity);
            if (capacityError != null) return Result.Failure<AvailabilitySlotResponse>(capacityError);

            var endTime = SchedulingSupport.EffectiveEndTime(round, request.StartTime, request.EndTime);

            var overlap = await SchedulingSupport.ValidateSlotTimeAsync(
                _unitOfWork, request.JobPostingId, request.RoundNumber, request.StartTime, endTime, null, ct);
            if (overlap != null) return Result.Failure<AvailabilitySlotResponse>(overlap);

            var slot = new AvailabilitySlot
            {
                JobPostingId = request.JobPostingId,
                RoundNumber = request.RoundNumber,
                StartTime = request.StartTime,
                EndTime = endTime,
                Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Ho_Chi_Minh" : request.Timezone,
                Capacity = capacity,
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

            // Đọc theo DÒNG booking chứ không theo cột `booked_count` (bài học ADR-058) — và đọc
            // MỌI dòng, không chỉ dòng đang giữ chỗ.
            //
            // Vì sao phải đếm cả dòng ĐÃ ĐÓNG: `booked_count` chỉ đếm booking `scheduled`, nên một ca
            // từng bị ứng viên báo bận hiện ra là "Đã đặt 0/1" — trông như trống. Xoá nó thì Postgres
            // chặn ở khoá ngoại `FK_interview_bookings_availability_slots_...` và người dùng nhận 500
            // kèm một câu không nói được điều gì. Chính là lỗi vừa gặp.
            //
            // Vì sao KHÔNG xoá kèm dòng booking cũ: dòng đó là bằng chứng "ứng viên này được mời vào
            // ĐÚNG giờ đó rồi báo bận" (`decline_reason`, `declined_by` — ADR-058). Bỏ ca đi thì lời
            // giải thích ấy mất chỗ bám. Ca từng được đem ra mời ai đó là một phần hồ sơ, không phải
            // rác cần dọn — cần dùng lại thì SỬA GIỜ của nó.
            var rows = (await _unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => b.AvailabilitySlotId == slot.Id, ct)).ToList();

            if (rows.Any(b => string.Equals(b.Status, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure("Không thể xoá khung giờ đã có ứng viên đặt lịch.");

            if (rows.Count > 0)
                return Result.Failure(
                    "Ca này từng được gán cho ứng viên (đã báo bận hoặc đã huỷ) nên phải giữ lại để còn dấu vết. "
                    + "Cần dùng lại thì sửa giờ của ca, đừng xoá.");

            _unitOfWork.Repository<AvailabilitySlot>().Delete(slot);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    // ============================================================
    // PATCH /api/schedules/slots/{id}/time — sửa giờ của một ca chưa ai đặt
    // ============================================================

    /// <summary>
    /// Sửa giờ một ca đã tạo (ADR-067).
    ///
    /// <b>Chỉ khi CHƯA ai giữ chỗ.</b> Ca đã hẹn với ứng viên thì đổi giờ tại đây là đổi lịch hẹn
    /// của người khác mà không báo họ — đúng đường cho việc đó là "dời lịch"
    /// (<c>RescheduleBookingsAsync</c>), nơi có gửi thông báo và chốt chỗ nguyên tử. Trước khi có
    /// lệnh này, cách duy nhất để sửa một ca gõ nhầm là xoá đi tạo lại.
    /// </summary>
    public record UpdateSlotTimeCommand(
        Guid Id, DateTimeOffset StartTime, DateTimeOffset EndTime, Guid? UserId, string? Role)
        : IRequest<Result<AvailabilitySlotResponse>>;

    public class UpdateSlotTimeCommandHandler
        : IRequestHandler<UpdateSlotTimeCommand, Result<AvailabilitySlotResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateSlotTimeCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<AvailabilitySlotResponse>> Handle(
            UpdateSlotTimeCommand command, CancellationToken ct)
        {
            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(command.Id, ct);
            if (slot == null)
                return Result.Failure<AvailabilitySlotResponse>("Không tìm thấy khung giờ.", CommonErrorCodes.NotFound);

            var (ok, _) = await SchedulingSupport.CanManageAsync(
                _unitOfWork, slot.JobPostingId, command.UserId, command.Role, ct);
            if (!ok)
                return Result.Failure<AvailabilitySlotResponse>("Bạn không có quyền sửa khung giờ này.", CommonErrorCodes.Forbidden);

            // Đọc theo DÒNG booking chứ không theo cột `booked_count` — cột là khoá tương tranh,
            // dòng mới là sự thật (bài học ADR-058).
            var held = await _unitOfWork.Repository<InterviewBooking>().CountAsync(
                b => b.AvailabilitySlotId == slot.Id && b.Status == BookingStatus.Scheduled, ct);
            if (held > 0)
                return Result.Failure<AvailabilitySlotResponse>(
                    "Ca này đã có ứng viên giữ chỗ nên không sửa giờ được. Hãy dùng chức năng dời lịch để chuyển họ sang ca khác.");

            if (command.EndTime <= command.StartTime)
                return Result.Failure<AvailabilitySlotResponse>("Giờ kết thúc phải sau giờ bắt đầu.");
            if (command.StartTime <= DateTimeOffset.UtcNow)
                return Result.Failure<AvailabilitySlotResponse>("Khung giờ phải nằm trong tương lai.");

            // Ca thi: giờ kết thúc = giờ mở + thời lượng bài, server tự tính (ADR-072).
            var round = await SchedulingSupport.RoundConfigAsync(_unitOfWork, slot.JobPostingId, slot.RoundNumber, ct);
            var endTime = SchedulingSupport.EffectiveEndTime(round, command.StartTime, command.EndTime);

            var overlap = await SchedulingSupport.ValidateSlotTimeAsync(
                _unitOfWork, slot.JobPostingId, slot.RoundNumber, command.StartTime, endTime, slot.Id, ct);
            if (overlap != null) return Result.Failure<AvailabilitySlotResponse>(overlap);

            slot.StartTime = command.StartTime;
            slot.EndTime = endTime;
            slot.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<AvailabilitySlot>().Update(slot);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(AvailabilitySlotResponse.FromEntity(slot));
        }
    }

    // ============================================================
    // PATCH /api/schedules/slots/{id}/capacity — sửa sức chứa
    // ============================================================

    public record UpdateSlotCapacityCommand(Guid Id, int? Capacity, Guid? UserId, string? Role)
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

            // Cùng luật với lúc tạo: vòng hội thoại đúng bằng 1, vòng trắc nghiệm không giới hạn
            // (hoặc một trần do Recruiter đặt).
            var roundType = await SchedulingSupport.RoundTypeAsync(
                _unitOfWork, slot.JobPostingId, slot.RoundNumber, ct);
            var (capacityError, capacity) = SchedulingSupport.ResolveCapacity(roundType, command.Capacity);
            if (capacityError != null) return Result.Failure<AvailabilitySlotResponse>(capacityError);

            // Hạ trần xuống dưới số đã đặt là đuổi người ra khỏi chỗ họ đang giữ mà không ai báo.
            // Không giới hạn thì không có trần nào để mà thấp hơn.
            if (capacity is { } cap && cap < slot.BookedCount)
                return Result.Failure<AvailabilitySlotResponse>($"Sức chứa không được nhỏ hơn số đã đặt ({slot.BookedCount}).");

            slot.Capacity = capacity;
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

    /// <summary>
    /// <paramref name="EmailOverride"/>: nội dung thư mời do nhân sự sửa tay ở trình soạn thảo
    /// (ADR-061, Phase 4). Bỏ trống → dựng từ mẫu như cũ. Truyền KÈM lệnh chứ không lưu bản nháp
    /// riêng, để tính nguyên tử của ADR-059 giữ nguyên: huỷ trình soạn = không chốt chỗ, không gửi.
    /// </summary>
    public record AssignSlotCommand(
        Guid ApplicationId, Guid SlotId, int Round, Guid? UserId, string? Role,
        EmailOverride? EmailOverride = null)
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

            // Chỉ xếp lịch khi ứng viên đã qua vòng duyệt CV và hồ sơ chưa đóng.
            //
            // Dùng ApplicationStatuses thay vì danh sách chuỗi viết tay: bản cũ liệt kê tay 5 giá trị
            // nên `hm_review` (ADR-061) lọt qua — xếp được lịch VƯỢT MẶT cổng duyệt của Hiring Manager,
            // đúng thứ cổng đó sinh ra để chặn. Ba trạng thái kết thúc mới (`hired`, `offer_declined`,
            // `pass`) cũng lọt: ứng viên đã nhận việc vẫn bị xếp vào ca phỏng vấn được.
            var status = (app.Status ?? string.Empty).ToLowerInvariant();
            if (ApplicationStatuses.IsCvPhase(status) || ApplicationStatuses.IsTerminal(status) || status == "rejected")
                return Result.Failure<AssignSlotResultDto>("Chỉ có thể xếp lịch khi ứng viên đã qua vòng duyệt CV và hồ sơ chưa đóng.");

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

            // Qua vòng TRẮC NGHIỆM = Recruiter xếp thẳng lịch vòng kế (vòng này không có bước Hiring
            // Manager chốt kết quả nào để sinh lời mời vòng sau). Nên chính thao tác này phải chặn
            // việc NHẢY QUA bài thi: chưa có bài ở vòng trắc nghiệm ngay trước thì chưa có gì để
            // quyết định cho qua. Bài hệ thống nộp thay khi hết hạn vẫn tính — đó là một kết quả (0
            // điểm), và giữ hay loại là việc của Recruiter.
            if (round > 1
                && InterviewInviteEmail.IsOnlineTest(await SchedulingSupport.RoundTypeAsync(_unitOfWork, app.JobPostingId, round - 1, ct))
                && await _unitOfWork.Repository<OnlineTestSubmission>().CountAsync(
                    s => s.ApplicationId == applicationId && s.RoundNumber == round - 1, ct) == 0)
                return Result.Failure<AssignSlotResultDto>(
                    $"Ứng viên chưa có bài thi trắc nghiệm vòng {round - 1} — chưa thể xếp lịch vòng {round}.");

            // Ba luật chống xếp lịch hỏng (ADR-067): một ca một người · ứng viên không dự hai buổi
            // trùng giờ (kể cả tin khác) · ca phải nằm trong giờ Hiring Manager có mặt được.
            // Kiểm TRƯỚC khi chiếm chỗ, để một lượt gán bị từ chối không để lại chỗ đã trừ.
            var violation = await SchedulingSupport.ValidateAssignmentAsync(
                _unitOfWork, app, slot, round, excludeBookingId: null, ct);
            if (violation != null)
                return Result.Failure<AssignSlotResultDto>(violation);

            // Chốt chỗ NGUYÊN TỬ chống overbooking (DB row-lock 1 câu lệnh, không mutate entity đang được EF theo dõi).
            // `capacity IS NULL` = không giới hạn (vòng trắc nghiệm): vẫn TĂNG booked_count để con số
            // "bao nhiêu người đã đăng ký" còn đúng, chỉ bỏ phép so với trần.
            var incremented = await _unitOfWork.ExecuteSqlRawAsync(
                "UPDATE availability_slots SET booked_count = booked_count + 1, updated_at = {0} "
                + "WHERE id = {1} AND (capacity IS NULL OR booked_count < capacity)",
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
                var assignedRoundType = await SchedulingSupport.RoundTypeAsync(_unitOfWork, app.JobPostingId, round, ct);
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
                        Title = InterviewInviteEmail.IsOnlineTest(assignedRoundType)
                            ? "Lịch làm bài trắc nghiệm đã được xếp"
                            : "Lịch phỏng vấn đã được xếp",
                        Body = $"Nhân sự đã xếp {InterviewInviteEmail.AppointmentNoun(assignedRoundType)} (vòng {round}) cho bạn: {whenText}. Vui lòng XÁC NHẬN nếu bạn tham dự được, hoặc từ chối kèm lý do.",
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

                // Gửi + ghi nhật ký một lượt; dùng bản sửa tay nếu nhân sự đã soạn lại ở trình
                // soạn thảo, nếu không thì dùng đúng mẫu như trước.
                var sent = await CandidateEmailSender.SendAsync(
                    _unitOfWork, _notificationService,
                    EmailTemplateKeys.InterviewInvite,
                    new RenderedEmail(mail.Subject, mail.Html, app.CandidateEmail, app.CandidateName),
                    request.EmailOverride,
                    applicationId: app.Id, jobPostingId: app.JobPostingId,
                    sentByUserId: request.UserId, ct);

                // Giữ Message-Id để thư NHẮC LỊCH sau này trả lời vào đúng luồng thư mời này,
                // thay vì đẻ ra một thư rời mà ứng viên phải tự đi tìm lại giờ hẹn.
                if (!string.IsNullOrWhiteSpace(sent.MessageId))
                {
                    booking.InviteEmailMessageId = sent.MessageId;
                    booking.UpdatedAt = DateTimeOffset.UtcNow;
                    _unitOfWork.Repository<InterviewBooking>().Update(booking);
                    await _unitOfWork.SaveChangesAsync(ct);
                }
            }
            catch { /* best-effort */ }

            return Result.Success(new AssignSlotResultDto(booking.Id, slotDto));
        }
    }
}
