using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Jobs.Commands.ReassignJob
{
    /// <summary>
    /// HR Lead chuyển giao một tin tuyển dụng sang Recruiter khác — thao tác cân tải kinh điển
    /// của ATS (Greenhouse/Lever gọi là đổi recruiter/owner của requisition): dùng khi một người
    /// quá tải, nghỉ phép, nghỉ việc, hoặc tin cần người có chuyên môn khác.
    ///
    /// Cố ý ghi thẳng vào <see cref="JobPosting.CreatedByUserId"/> thay vì thêm cột chủ sở hữu
    /// riêng: TOÀN BỘ cổng kiểm quyền của hệ thống đã dùng đúng trường này (sửa/xoá/đổi trạng
    /// thái tin, xem ứng viên của tin, cấu hình lịch, ngân hàng trắc nghiệm, bộ lọc "tin của
    /// tôi", định tuyến thông báo). Thêm cột thứ hai sẽ phải sửa đồng bộ 14 cổng đó, sót một chỗ
    /// là người mới không vào được hoặc người cũ vẫn còn quyền. Lịch sử "ai tạo ban đầu" được
    /// giữ bằng bản ghi AuditLog dưới đây — vốn cũng là cách ATS lưu vết chuyển giao.
    /// </summary>
    public record ReassignJobCommand(Guid JobId, Guid ToRecruiterId, Guid ActorId, string? Reason)
        : IRequest<Result<string>>;

    public class ReassignJobCommandHandler : IRequestHandler<ReassignJobCommand, Result<string>>
    {
        /// <summary>
        /// Không escape ký tự ngoài ASCII: nhật ký kiểm toán do người đọc ở màn Super Admin,
        /// mặc định của System.Text.Json sẽ biến tên và lý do tiếng Việt thành `â...`.
        /// An toàn vì chuỗi này chỉ nằm trong DB, không nhúng vào HTML.
        /// </summary>
        private static readonly System.Text.Json.JsonSerializerOptions AuditMetadataJson = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public ReassignJobCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<string>> Handle(ReassignJobCommand request, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobId, ct);
            if (job == null || job.DeletedAt != null)
                return Result.Failure<string>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            var target = await _unitOfWork.Repository<User>().GetByIdAsync(request.ToRecruiterId, ct);
            if (target == null || target.DeletedAt != null)
                return Result.Failure<string>("Không tìm thấy người được chuyển giao.", CommonErrorCodes.NotFound);

            // Chỉ chuyển cho Recruiter: chuyển cho HR Lead hay Super Admin là làm sai mô hình
            // phân công (họ vốn đã thấy mọi tin), còn chuyển cho tài khoản đang khoá thì tin sẽ
            // rơi vào trạng thái không ai xử lý được.
            if (!string.Equals(target.Role, AppRoles.Recruiter, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<string>("Chỉ chuyển giao được cho Chuyên viên tuyển dụng.");
            if (!target.IsActive)
                return Result.Failure<string>("Tài khoản nhận đang bị khoá, không thể nhận tin mới.");

            var fromUserId = job.CreatedByUserId;
            if (fromUserId == request.ToRecruiterId)
                return Result.Failure<string>("Tin này đã do chính người đó phụ trách.");

            var fromUser = await _unitOfWork.Repository<User>().GetByIdAsync(fromUserId, ct);

            job.CreatedByUserId = request.ToRecruiterId;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<JobPosting>().Update(job);

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = request.ActorId,
                Action = "job_reassigned",
                EntityType = "JobPosting",
                EntityId = job.Id,
                // Giữ nguyên người phụ trách cũ ở đây — sau khi ghi đè CreatedByUserId thì đây
                // là nơi duy nhất còn dấu vết ai từng phụ trách tin này.
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    fromUserId,
                    fromUserName = fromUser?.FullName,
                    toUserId = request.ToRecruiterId,
                    toUserName = target.FullName,
                    jobTitle = job.Title,
                    reason
                }, AuditMetadataJson),
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);

            // Hồ sơ đang chạy theo tin này chuyển sang người mới cùng lúc — báo cho họ biết đang
            // nhận thêm bao nhiêu việc chứ không chỉ nhận một cái tên tin.
            var openApplications = (await _unitOfWork.Repository<Domain.Entities.Application>()
                .FindAsync(a => a.DeletedAt == null && a.JobPostingId == job.Id, ct))
                .Count(a => a.Status != "pass" && a.Status != "not_pass" && a.Status != "rejected");

            var toBody = openApplications > 0
                ? $"Bạn được giao phụ trách tin \"{job.Title}\" với {openApplications} hồ sơ đang xử lý."
                : $"Bạn được giao phụ trách tin \"{job.Title}\".";
            if (reason != null) toBody += $" Lý do: {reason}";

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = request.ToRecruiterId,
                Type = "job_reassigned",
                Title = "Bạn nhận phụ trách một tin tuyển dụng",
                Body = toBody,
                Link = $"/recruiter/my-jobs/{job.Id}",
                DedupKey = $"job_reassigned:{job.Id}:{request.ToRecruiterId}",
                IsRead = false
            }, ct);

            if (fromUser != null)
            {
                await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = fromUserId,
                    Type = "job_reassigned",
                    Title = "Một tin tuyển dụng đã chuyển sang người khác",
                    Body = $"Tin \"{job.Title}\" nay do {target.FullName} phụ trách."
                           + (reason != null ? $" Lý do: {reason}" : string.Empty),
                    Link = "/recruiter/my-jobs",
                    DedupKey = $"job_unassigned:{job.Id}:{fromUserId}",
                    IsRead = false
                }, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            // Realtime để chuông của cả hai bên sáng ngay, không phải đợi tải lại trang.
            await _notifications.PublishUserEventAsync(request.ToRecruiterId, "JobReassigned",
                new { jobId = job.Id, jobTitle = job.Title, direction = "received" }, ct);
            if (fromUser != null)
            {
                await _notifications.PublishUserEventAsync(fromUserId, "JobReassigned",
                    new { jobId = job.Id, jobTitle = job.Title, direction = "handed_over" }, ct);
            }

            return Result.Success($"Đã chuyển tin \"{job.Title}\" sang {target.FullName}.");
        }
    }
}
