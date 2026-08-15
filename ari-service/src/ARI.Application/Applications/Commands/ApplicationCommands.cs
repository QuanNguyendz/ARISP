using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using MediatR;

namespace ARI.Application.Applications.Commands
{
    // ============================================================
    // PATCH /api/applications/{id}/status
    // ============================================================

    public record UpdateApplicationStatusCommand(Guid Id, string Status) : IRequest<Result<ApplicationResponse>>;

    public class UpdateApplicationStatusCommandHandler : IRequestHandler<UpdateApplicationStatusCommand, Result<ApplicationResponse>>
    {
        private readonly IApplicationService _applicationService;

        public UpdateApplicationStatusCommandHandler(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        public Task<Result<ApplicationResponse>> Handle(UpdateApplicationStatusCommand request, CancellationToken ct)
            => _applicationService.UpdateApplicationStatusAsync(request.Id, request.Status, ct);
    }

    // ============================================================
    // POST /api/applications/{id}/accept
    // ============================================================

    public record AcceptApplicationResultDto(Guid BookingId);

    /// <summary>
    /// Duyệt CV = duyệt <b>và</b> xếp lịch vòng 1 trong CÙNG một thao tác.
    /// Trước đây hai việc này tách rời nên "đã duyệt nhưng quên xếp lịch" là một trạng thái hợp lệ:
    /// ứng viên chỉ thấy chuông trong Portal và <b>không nhận được email nào</b> — đúng thứ người dùng
    /// báo lỗi. Nay <c>SlotId</c> là bắt buộc, nên mọi hồ sơ được duyệt đều đi kèm giờ hẹn + thư mời.
    /// </summary>
    public record AcceptApplicationCommand(Guid Id, Guid SlotId, Guid? UserId, string? Role)
        : IRequest<Result<AcceptApplicationResultDto>>;

    public class AcceptApplicationCommandHandler : IRequestHandler<AcceptApplicationCommand, Result<AcceptApplicationResultDto>>
    {
        private readonly IApplicationService _applicationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISender _sender;

        public AcceptApplicationCommandHandler(
            IApplicationService applicationService,
            IUnitOfWork unitOfWork,
            ISender sender)
        {
            _applicationService = applicationService;
            _unitOfWork = unitOfWork;
            _sender = sender;
        }

        public async Task<Result<AcceptApplicationResultDto>> Handle(AcceptApplicationCommand request, CancellationToken ct)
        {
            if (request.SlotId == Guid.Empty)
                return Result.Failure<AcceptApplicationResultDto>("Vui lòng chọn khung giờ phỏng vấn vòng 1 trước khi duyệt hồ sơ.");

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(request.Id, ct);
            if (app == null)
                return Result.Failure<AcceptApplicationResultDto>("Không tìm thấy hồ sơ ứng tuyển này.", CommonErrorCodes.NotFound);

            // Kiểm khung giờ TRƯỚC khi duyệt: duyệt xong mới phát hiện ca sai/đầy thì hồ sơ rơi lại
            // đúng vào trạng thái "đã duyệt mà chưa có lịch" mà thiết kế này muốn xoá bỏ.
            var slot = await _unitOfWork.Repository<ARI.Domain.Entities.AvailabilitySlot>().GetByIdAsync(request.SlotId, ct);
            if (slot == null)
                return Result.Failure<AcceptApplicationResultDto>("Không tìm thấy khung giờ.", CommonErrorCodes.NotFound);
            if (slot.JobPostingId != app.JobPostingId || slot.RoundNumber != 1)
                return Result.Failure<AcceptApplicationResultDto>("Khung giờ không thuộc vòng 1 của tin tuyển dụng này.");
            if (slot.StartTime <= DateTimeOffset.UtcNow)
                return Result.Failure<AcceptApplicationResultDto>("Khung giờ đã ở quá khứ. Hãy chọn khung giờ khác.");
            if (slot.BookedCount >= slot.Capacity)
                return Result.Failure<AcceptApplicationResultDto>("Khung giờ đã đầy. Vui lòng chọn khung giờ khác hoặc tăng sức chứa.");

            var accept = await _applicationService.AcceptApplicationAsync(request.Id, ct);
            if (accept.IsFailure)
                return accept.ErrorCode == null
                    ? Result.Failure<AcceptApplicationResultDto>(accept.Error)
                    : Result.Failure<AcceptApplicationResultDto>(accept.Error, accept.ErrorCode);

            // Gán lịch dùng lại đúng lệnh của ADR-048 (chốt chỗ nguyên tử + thư mời + realtime),
            // không nhân bản logic. Khe hở duy nhất còn lại: chỗ cuối cùng bị người khác lấy mất
            // giữa hai bước — hiếm, và thông báo dưới đây nói rõ phải làm gì tiếp.
            var assign = await _sender.Send(
                new ARI.Application.Scheduling.AssignSlotCommand(request.Id, request.SlotId, 1, request.UserId, request.Role), ct);
            if (assign.IsFailure)
            {
                var message = $"Đã duyệt CV nhưng chưa xếp được lịch: {assign.Error} Hãy gán khung giờ khác cho ứng viên để hệ thống gửi thư mời.";
                return assign.ErrorCode == null
                    ? Result.Failure<AcceptApplicationResultDto>(message)
                    : Result.Failure<AcceptApplicationResultDto>(message, assign.ErrorCode);
            }

            return Result.Success(new AcceptApplicationResultDto(assign.Value.BookingId));
        }
    }

    // ============================================================
    // POST /api/applications/{id}/reject
    // ============================================================

    public record RejectApplicationCommand(Guid Id) : IRequest<Result<bool>>;

    public class RejectApplicationCommandHandler : IRequestHandler<RejectApplicationCommand, Result<bool>>
    {
        private readonly IApplicationService _applicationService;

        public RejectApplicationCommandHandler(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        public Task<Result<bool>> Handle(RejectApplicationCommand request, CancellationToken ct)
            => _applicationService.RejectApplicationAsync(request.Id, ct);
    }
}
