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
}
