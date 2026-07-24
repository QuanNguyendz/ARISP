using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ARI.Application.Jobs.Commands.UpdateJobStatus
{
    /// <summary>
    /// Approval workflow tin tuyển dụng: Recruiter draft→pending / active→closed;
    /// HrAdmin/SuperAdmin pending→active|rejected, →archived. Duyệt pending→active ghi nhận
    /// người duyệt + đóng dấu duyệt lên file JD (best-effort).
    /// </summary>
    public record UpdateJobStatusCommand(Guid Id, UpdateJobStatusRequest Request, Guid UserId, string? Role)
        : IRequest<Result<JobPostingResponse>>;

    public class UpdateJobStatusCommandHandler : IRequestHandler<UpdateJobStatusCommand, Result<JobPostingResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IJdStampService _jdStampService;
        private readonly IDocumentParserService _documentParser;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly ILogger<UpdateJobStatusCommandHandler> _logger;

        public UpdateJobStatusCommandHandler(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IJdStampService jdStampService,
            IDocumentParserService documentParser,
            INotificationService notificationService,
            IEmailService emailService,
            ILogger<UpdateJobStatusCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _jdStampService = jdStampService;
            _documentParser = documentParser;
            _notificationService = notificationService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<Result<JobPostingResponse>> Handle(UpdateJobStatusCommand command, CancellationToken ct)
        {
            var request = command.Request;
            var userId = command.UserId;

            if (string.IsNullOrWhiteSpace(request.Status))
                return Result.Failure<JobPostingResponse>("Status is required.");

            var targetStatus = request.Status.Trim().ToLowerInvariant();
            var allowedStatuses = new[] { "draft", "pending", "active", "rejected", "closed", "archived" };

            if (!allowedStatuses.Contains(targetStatus))
                return Result.Failure<JobPostingResponse>($"Trạng thái không hợp lệ. Sử dụng một trong: {string.Join(", ", allowedStatuses)}.");

            if (targetStatus == "draft")
                return Result.Failure<JobPostingResponse>("Không thể chuyển trạng thái về 'draft'. 'draft' chỉ dùng khi tạo hoặc chỉnh sửa nháp ban đầu.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(command.Id, ct);
            if (job == null)
                return Result.Failure<JobPostingResponse>("Job posting not found.", CommonErrorCodes.NotFound);

            var currentStatus = job.Status?.Trim().ToLowerInvariant();

            var isSuperOrHrAdmin = command.Role == AppRoles.SuperAdmin || command.Role == AppRoles.HrAdmin;
            var isOwner = job.CreatedByUserId == userId;

            if (!isSuperOrHrAdmin && !isOwner)
                return Result.Failure<JobPostingResponse>("Bạn không có quyền thay đổi trạng thái tin tuyển dụng này.", CommonErrorCodes.Forbidden);

            if (currentStatus == targetStatus)
                return Result.Failure<JobPostingResponse>($"Tin tuyển dụng hiện tại đã ở trạng thái '{targetStatus}' rồi.");

            if (currentStatus == "archived")
                return Result.Failure<JobPostingResponse>("Không thể thay đổi trạng thái của tin tuyển dụng đã lưu trữ (archived).");

            // --- VALIDATE STATE TRANSITIONS & ROLES WORKFLOW ---

            // CASE A: Từ chối ('rejected') -> Bắt buộc Admin + có lý do
            if (targetStatus == "rejected")
            {
                if (!isSuperOrHrAdmin)
                    return Result.Failure<JobPostingResponse>("Chỉ HrAdmin hoặc SuperAdmin mới có quyền từ chối duyệt bài.", CommonErrorCodes.Forbidden);

                if (currentStatus != "pending")
                    return Result.Failure<JobPostingResponse>("Chỉ có thể từ chối (rejected) những bài viết đang ở trạng thái chờ duyệt (pending).");

                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                    return Result.Failure<JobPostingResponse>("Vui lòng cung cấp lý do từ chối duyệt bài (RejectionReason).");

                job.RejectionReason = request.RejectionReason.Trim();

                // Thông báo kết quả TỪ CHỐI về người tạo tin (thường là Recruiter).
                await AddJobDecisionNotificationAsync(job, userId, reviewerName: null, approved: false, reason: job.RejectionReason, ct);
            }

            // CASE B: Phê duyệt public ('active') -> Chỉ Admin
            if (targetStatus == "active")
            {
                if (!isSuperOrHrAdmin)
                    return Result.Failure<JobPostingResponse>("Chỉ HrAdmin hoặc SuperAdmin mới có quyền kích hoạt/phê duyệt bài viết.", CommonErrorCodes.Forbidden);

                if (currentStatus != "pending" && currentStatus != "closed" && currentStatus != "draft")
                    return Result.Failure<JobPostingResponse>("Chỉ có thể kích hoạt (active) từ trạng thái chờ duyệt (pending), nháp (draft) hoặc đã đóng (closed).");

                if (job.ApplicationDeadline.HasValue && job.ApplicationDeadline.Value <= DateTimeOffset.UtcNow)
                    return Result.Failure<JobPostingResponse>("Hạn nộp hồ sơ của Job này đã ở quá khứ. Hãy cập nhật lại gia hạn Deadline trước khi chuyển sang Active.");

                if (job.PublishedAt == null) job.PublishedAt = DateTimeOffset.UtcNow;
                job.RejectionReason = null;

                // Ghi nhận phê duyệt + đóng dấu duyệt lên file JD — chỉ khi duyệt từ pending hoặc draft (HR Leader tự publish).
                if (currentStatus == "pending" || currentStatus == "draft")
                {
                    var approver = await _unitOfWork.Repository<User>().GetByIdAsync(userId, ct);
                    var approverName = approver != null
                        ? (string.IsNullOrWhiteSpace(approver.FullName) ? approver.Email : approver.FullName)
                        : "HR Leader";

                    job.ApprovedByUserId = userId;
                    job.ApprovedAt = DateTimeOffset.UtcNow;
                    job.ApproverName = approverName;

                    // Thông báo kết quả ĐƯỢC DUYỆT về người tạo tin (thường là Recruiter).
                    await AddJobDecisionNotificationAsync(job, userId, approverName, approved: true, reason: null, ct);

                    // Đóng dấu duyệt lên file JD. PDF: vẽ dấu lên file gốc. DOCX: render nội dung JD
                    // thành PDF mới rồi đóng dấu. Thất bại KHÔNG được chặn việc duyệt tin.
                    var fmt = (job.JdFileFormat ?? string.Empty).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(job.JdFileUrl) && (fmt == "pdf" || fmt == "docx"))
                    {
                        try
                        {
                            byte[]? stamped = null;
                            var original = await _fileStorage.ReadAllBytesAsync(job.JdFileUrl, ct);

                            if (fmt == "pdf")
                            {
                                if (original != null && original.Length > 0)
                                    stamped = await _jdStampService.StampApprovalAsync(original, approverName, job.ApprovedAt.Value, ct);
                            }
                            else // docx
                            {
                                var bodyText = job.JobDescription ?? string.Empty;
                                if (original != null && original.Length > 0)
                                {
                                    try
                                    {
                                        using var ms = new MemoryStream(original);
                                        var parsed = (await _documentParser.ParseDocumentAsync(ms, ".docx"))?.Replace("\0", string.Empty);
                                        if (!string.IsNullOrWhiteSpace(parsed)) bodyText = parsed;
                                    }
                                    catch (Exception exParse)
                                    {
                                        _logger.LogWarning(exParse, "Parse DOCX để đóng dấu thất bại, dùng JobDescription. Job {JobId}", job.Id);
                                    }
                                }
                                stamped = await _jdStampService.StampApprovalFromTextAsync(job.Title, bodyText, approverName, job.ApprovedAt.Value, ct);
                            }

                            if (stamped != null && stamped.Length > 0)
                            {
                                // File đã đóng dấu luôn là PDF, kể cả khi gốc là DOCX.
                                var baseName = Path.GetFileNameWithoutExtension(job.JdFileName ?? "JD") + ".pdf";
                                var signedName = JobsSupport.AppendSuffix(baseName, "-da-duyet");
                                job.SignedJdFileUrl = await _fileStorage.SaveAsync(stamped, signedName, "application/pdf", ct);
                            }
                        }
                        catch (Exception exStamp)
                        {
                            _logger.LogWarning(exStamp, "Đóng dấu duyệt JD thất bại cho job {JobId} — vẫn duyệt tin.", job.Id);
                        }
                    }
                }
            }

            // CASE C: Gửi duyệt bài ('pending') -> Recruiter/Owner
            if (targetStatus == "pending")
            {
                if (!isOwner)
                    return Result.Failure<JobPostingResponse>("Chỉ Recruiter/Owner mới có quyền gửi duyệt bài.", CommonErrorCodes.Forbidden);

                if (currentStatus != "draft" && currentStatus != "rejected")
                    return Result.Failure<JobPostingResponse>("Chỉ có thể gửi duyệt (pending) khi bài viết đang là bản nháp (draft) hoặc bị từ chối (rejected).");

                // Khi sửa xong nộp lại, xóa tạm lý do từ chối cũ để chờ kết quả mới
                job.RejectionReason = null;
            }

            // CASE D: Đóng bài ('closed')
            if (targetStatus == "closed")
            {
                if (!isOwner && !isSuperOrHrAdmin)
                    return Result.Failure<JobPostingResponse>("Chỉ Recruiter/Owner hoặc HrAdmin/SuperAdmin mới có quyền đóng bài.", CommonErrorCodes.Forbidden);

                if (currentStatus != "active")
                    return Result.Failure<JobPostingResponse>("Chỉ có thể đóng (closed) một tin tuyển dụng đang hoạt động (active).");
            }

            // CASE E: Lưu trữ / Xóa mềm ('archived')
            if (targetStatus == "archived")
            {
                var activeApps = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().FindAsync(
                    a => a.JobPostingId == command.Id && a.Status != "not_pass" && a.Status != "withdrawn" && a.Status != "pass",
                    ct);

                if (activeApps.Any())
                    return Result.Failure<JobPostingResponse>("Không thể chuyển tin tuyển dụng sang lưu trữ (archived) khi đang có hồ sơ ứng tuyển đang hoạt động.");

                job.DeletedAt = DateTimeOffset.UtcNow;
            }

            // Đồng bộ cập nhật vào database
            job.Status = targetStatus;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<JobPosting>().Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            // Lấy lại danh sách rounds trả về cho đồng bộ cấu trúc Response
            var rds = await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == command.Id, ct);
            var roundDtos = rds.OrderBy(r => r.RoundNumber).Select(RoundConfigDto.FromEntity).ToList();

            var statusResponse = JobPostingResponse.FromEntity(job, roundDtos);
            // Resolve storageKey -> URL dùng được cho file JD gốc + bản đã đóng dấu (người gọi là staff).
            if (!string.IsNullOrEmpty(statusResponse.JdFileUrl))
                statusResponse.JdFileUrl = await _fileStorage.GetUrlAsync(statusResponse.JdFileUrl, ct);
            if (!string.IsNullOrEmpty(statusResponse.SignedJdFileUrl))
                statusResponse.SignedJdFileUrl = await _fileStorage.GetUrlAsync(statusResponse.SignedJdFileUrl, ct);

            // Gửi thông báo SignalR và Email tương ứng
            if (targetStatus == "pending")
            {
                await _notificationService.PublishGroupEventAsync("hr_admin", "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = "pending", Title = job.Title }, ct);

                var creator = await _unitOfWork.Repository<User>().GetByIdAsync(job.CreatedByUserId, ct);
                var creatorName = creator != null
                    ? (string.IsNullOrWhiteSpace(creator.FullName) ? creator.Email : creator.FullName)
                    : "Nhân viên";

                var hrAdmins = await _unitOfWork.Repository<User>().FindAsync(u => u.Role == "hr_admin" || u.Role == "super_admin", ct);
                var notifRepo = _unitOfWork.Repository<Notification>();
                var dedupKey = $"job_pending:{job.Id}:{DateTimeOffset.UtcNow.Ticks}";

                foreach (var hr in hrAdmins)
                {
                    var hrSettings = !string.IsNullOrEmpty(hr.SettingsJson)
                        ? System.Text.Json.JsonSerializer.Deserialize<StaffSettingsDto>(hr.SettingsJson) ?? new StaffSettingsDto()
                        : new StaffSettingsDto();

                    if (hrSettings.ReceivePush)
                    {
                        await notifRepo.AddAsync(new Notification
                        {
                            RecipientUserId = hr.Id,
                            DedupKey = dedupKey,
                            Type = "pending",
                            Title = "Tin tuyển dụng chờ duyệt",
                            Body = $"Tin tuyển dụng \"{job.Title}\" do {creatorName} gửi cần được phê duyệt.",
                            Link = $"/hr/jobs/{job.Id}",
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        }, ct);
                    }

                    if (hrSettings.ReceiveEmail && !string.IsNullOrWhiteSpace(hr.Email))
                    {
                        var subject = $"[ARISP] - Yêu cầu phê duyệt tin tuyển dụng: {job.Title}";
                        var htmlMessage = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
            <h2 style='color: #1e293b; margin-top: 0;'>Yêu cầu phê duyệt tin tuyển dụng</h2>
            <p style='color: #475569; font-size: 15px;'>Xin chào <strong>{hr.FullName ?? hr.Email}</strong>,</p>
            <p style='color: #475569; font-size: 15px;'>Nhân viên <strong>{creatorName}</strong> ({creator?.Email ?? "N/A"}) vừa gửi yêu cầu phê duyệt tin tuyển dụng mới:</p>
            <div style='background-color: #f8fafc; border-left: 4px solid #4f46e5; padding: 16px; margin: 20px 0; border-radius: 8px;'>
                <p style='margin: 0 0 8px 0; font-size: 16px; font-weight: bold; color: #1e293b;'>{job.Title}</p>
                {(string.IsNullOrEmpty(job.Department) ? "" : $"<p style='margin: 0 0 4px 0; color: #64748b; font-size: 14px;'>Phòng ban: {job.Department}</p>")}
                <p style='margin: 0; color: #64748b; font-size: 14px;'>Người tạo tin: <strong>{creatorName}</strong></p>
            </div>
            <p style='color: #475569; font-size: 15px;'>Vui lòng bấm vào nút bên dưới để xem chi tiết và phê duyệt tin tuyển dụng này:</p>
            <div style='text-align: center; margin: 28px 0;'>
                <a href='http://localhost:3001/hr/jobs/{job.Id}' style='background-color: #4f46e5; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>Xem &amp; Duyệt tin tuyển dụng</a>
            </div>
            <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;' />
            <p style='color: #94a3b8; font-size: 13px; margin: 0;'>Thư điện tử tự động từ Hệ thống tuyển dụng ARISP.</p>
        </div>";
                        try { await _emailService.SendEmailAsync(hr.Email, subject, htmlMessage); } catch { }
                    }
                }
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else if (targetStatus == "active" || targetStatus == "rejected")
            {
                var creator = await _unitOfWork.Repository<User>().GetByIdAsync(job.CreatedByUserId, ct);
                var creatorSettings = creator != null && !string.IsNullOrEmpty(creator.SettingsJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<StaffSettingsDto>(creator.SettingsJson) ?? new StaffSettingsDto()
                    : new StaffSettingsDto();

                if (creatorSettings.ReceivePush)
                {
                    await _notificationService.PublishUserEventAsync(job.CreatedByUserId, "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = targetStatus, Title = job.Title }, ct);
                }
            }

            if (targetStatus == "active" || targetStatus == "closed" || targetStatus == "archived")
            {
                await _notificationService.PublishAllEventAsync("ReceivePublicJobUpdate", new { JobId = job.Id, Status = targetStatus }, ct);
            }

            return Result.Success(statusResponse);
        }

        /// <summary>
        /// Gửi thông báo kết quả duyệt/từ chối tin về cho NGƯỜI TẠO tin (thường là Recruiter).
        /// Bỏ qua nếu người duyệt cũng chính là người tạo. Chỉ stage qua AddAsync — được persist
        /// CÙNG transaction với cập nhật trạng thái tin (1 SaveChanges).
        /// </summary>
        private async Task AddJobDecisionNotificationAsync(
            JobPosting job, Guid actorUserId, string? reviewerName, bool approved, string? reason, CancellationToken ct)
        {
            if (job.CreatedByUserId == actorUserId) return; // người duyệt cũng là người tạo → không tự thông báo

            var creator = await _unitOfWork.Repository<User>().GetByIdAsync(job.CreatedByUserId, ct);
            if (creator == null) return;

            if (string.IsNullOrWhiteSpace(reviewerName))
            {
                var actor = await _unitOfWork.Repository<User>().GetByIdAsync(actorUserId, ct);
                reviewerName = actor != null
                    ? (string.IsNullOrWhiteSpace(actor.FullName) ? actor.Email : actor.FullName)
                    : "HR Admin";
            }

            // Link tới trang chi tiết tin theo workspace của người tạo.
            var isRecruiter = string.Equals(creator.Role, "recruiter", StringComparison.OrdinalIgnoreCase);
            var link = isRecruiter ? $"/recruiter/my-jobs/{job.Id}" : $"/hr/jobs/{job.Id}";
            var now = DateTimeOffset.UtcNow;

            var creatorSettings = !string.IsNullOrEmpty(creator.SettingsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<StaffSettingsDto>(creator.SettingsJson) ?? new StaffSettingsDto()
                : new StaffSettingsDto();

            if (creatorSettings.ReceivePush)
            {
                await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = job.CreatedByUserId,
                    // Ticks ở khóa chống trùng → mỗi lần duyệt/từ chối là một sự kiện riêng, hỗ trợ nhiều vòng nộp lại.
                    DedupKey = $"{(approved ? "job_approved" : "job_rejected")}:{job.Id}:{now.Ticks}",
                    Type = approved ? "approved" : "rejected",
                    Title = approved ? "Tin tuyển dụng đã được duyệt" : "Tin tuyển dụng bị từ chối",
                    Body = approved
                        ? $"\"{job.Title}\" đã được {reviewerName} phê duyệt và đăng công khai."
                        : $"\"{job.Title}\" bị {reviewerName} từ chối. Lý do: {reason}",
                    Link = link,
                    CreatedAt = now,
                    UpdatedAt = now,
                }, ct);
            }

            // Gửi email thông báo kết quả duyệt bài cho Recruiter (creator)
            if (creatorSettings.ReceiveEmail && !string.IsNullOrWhiteSpace(creator.Email))
            {
                var subject = approved
                    ? $"[ARISP] - Tin tuyển dụng \"{job.Title}\" đã được phê duyệt"
                    : $"[ARISP] - Tin tuyển dụng \"{job.Title}\" đã bị từ chối";

                var htmlMessage = approved ? $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
            <h2 style='color: #059669; margin-top: 0;'>Tin tuyển dụng đã được phê duyệt!</h2>
            <p style='color: #475569; font-size: 15px;'>Xin chào <strong>{creator.FullName ?? creator.Email}</strong>,</p>
            <p style='color: #475569; font-size: 15px;'>Tin tuyển dụng <strong>{job.Title}</strong> của bạn đã được <strong>{reviewerName}</strong> phê duyệt và đăng công khai trên Job Board.</p>
            <div style='text-align: center; margin: 28px 0;'>
                <a href='http://localhost:3001{link}' style='background-color: #059669; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>Xem tin tuyển dụng</a>
            </div>
            <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;' />
            <p style='color: #94a3b8; font-size: 13px; margin: 0;'>Thư điện tử tự động từ Đội ngũ HR ARISP.</p>
        </div>" : $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
            <h2 style='color: #dc2626; margin-top: 0;'>Tin tuyển dụng bị từ chối</h2>
            <p style='color: #475569; font-size: 15px;'>Xin chào <strong>{creator.FullName ?? creator.Email}</strong>,</p>
            <p style='color: #475569; font-size: 15px;'>Tin tuyển dụng <strong>{job.Title}</strong> của bạn đã bị <strong>{reviewerName}</strong> từ chối phê duyệt.</p>
            <div style='background-color: #fef2f2; border-left: 4px solid #ef4444; padding: 16px; margin: 20px 0; border-radius: 8px;'>
                <p style='margin: 0; color: #991b1b; font-size: 14px;'><strong>Lý do từ chối:</strong> {reason}</p>
            </div>
            <p style='color: #475569; font-size: 15px;'>Vui lòng kiểm tra và cập nhật lại thông tin bài đăng:</p>
            <div style='text-align: center; margin: 28px 0;'>
                <a href='http://localhost:3001{link}' style='background-color: #dc2626; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>Chỉnh sửa tin tuyển dụng</a>
            </div>
            <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;' />
            <p style='color: #94a3b8; font-size: 13px; margin: 0;'>Thư điện tử tự động từ Đội ngũ HR ARISP.</p>
        </div>";

                try { await _emailService.SendEmailAsync(creator.Email, subject, htmlMessage); } catch { }
            }
        }
    }
}
