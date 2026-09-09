using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Jobs.Commands.DeleteJob
{
    /// <summary>
    /// Xóa mềm tin tuyển dụng. Không cho xóa nếu còn hồ sơ ứng tuyển đang hoạt động
    /// (loại trừ not_pass / withdrawn / pass).
    /// </summary>
    public record DeleteJobCommand(Guid Id, Guid UserId, string? Role) : IRequest<Result>;

    public class DeleteJobCommandHandler : IRequestHandler<DeleteJobCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public DeleteJobCommandHandler(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        public async Task<Result> Handle(DeleteJobCommand command, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(command.Id, ct);
            if (job == null)
                return Result.Failure("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            // 1. Validate ownership & roles — xoá tin cần quyền QUẢN LÝ (chủ tin hoặc quản trị viên).
            var isAuthorized = RoleNames.IsAdmin(command.Role) || job.CreatedByUserId == command.UserId;
            if (!isAuthorized)
                return Result.Failure("Bạn không có quyền xóa tin tuyển dụng này.", CommonErrorCodes.Forbidden);

            // 2. Chỉ hồ sơ CÒN ĐANG XỬ LÝ mới chặn việc xoá. Danh sách "đã đóng" lấy từ
            //    ApplicationStatuses.Terminal — bản liệt kê tại chỗ trước đây bỏ sót cv_rejected,
            //    nên một tin chỉ toàn hồ sơ bị loại ở vòng CV vẫn không xoá được.
            var activeApps = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().FindAsync(
                a => a.JobPostingId == command.Id && !ApplicationStatuses.Terminal.Contains(a.Status),
                ct);

            if (activeApps.Any())
                return Result.Failure("Cannot delete job with active applications. Không thể xóa tin tuyển dụng này vì đang có hồ sơ ứng tuyển đang hoạt động.");

            // 3. Thực hiện XÓA MỀM (Soft Delete) theo đúng Spec thiết kế
            job.DeletedAt = DateTimeOffset.UtcNow;
            job.Status = "archived"; // Đồng bộ chuyển trạng thái thành lưu trữ

            _unitOfWork.Repository<JobPosting>().Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            // Gửi thông báo SignalR cho Recruiter vừa xóa job
            await _notificationService.PublishUserEventAsync(command.UserId, "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = job.Status, Title = job.Title }, ct);

            // Thông báo cập nhật danh sách Job công khai cho Candidates
            await _notificationService.PublishAllEventAsync("ReceivePublicJobUpdate", new { JobId = job.Id, Status = "archived" }, ct);

            return Result.Success();
        }
    }
}
