using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Application.Scheduling;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.HiringTeam
{
    /// <summary>
    /// Cổng duyệt shortlist của Hiring Manager (ADR-061, 3a — sửa ở ADR-067).
    ///
    /// Trình tự CỐ Ý là: Recruiter duyệt hồ sơ → HM quyết định <b>kèm khung giờ họ có mặt được</b>
    /// → hồ sơ về hàng chờ xếp lịch của <b>Recruiter</b> → Recruiter chọn ca nằm trong khung đó.
    ///
    /// HM gửi giờ rảnh chứ KHÔNG tự chọn ca: chọn ca là thao tác vận hành có thể thất bại vì lý do
    /// lịch ("ca đã có người"), mà một cái duyệt chuyên môn hỏng vì hết ghế là vô nghĩa. Việc chốt
    /// giờ cụ thể với ứng viên (SMS/Zalo/gọi điện) cũng nằm ngoài hệ thống — hệ thống chỉ ràng buộc
    /// kết quả phải khớp giờ HM rảnh.
    ///
    /// <b>Ứng viên không được báo gì cho tới khi có giờ hẹn.</b> Cả bước Recruiter duyệt lẫn bước HM
    /// duyệt đều im lặng với ứng viên; thư "qua vòng" đi kèm lịch, ở <c>AssignSlotCommand</c>.
    /// </summary>
    internal static class ShortlistGateSupport
    {
        /// <summary>
        /// Người gọi có phải Hiring Manager CHÍNH của tin không. Quản trị viên KHÔNG tự động thoả:
        /// họ có đường riêng là "vượt cổng" (kèm lý do + audit), để phân biệt rõ trong dấu vết
        /// giữa "HM đã duyệt" và "quản trị viên duyệt thay".
        /// </summary>
        public static async Task<bool> IsPrimaryHmAsync(
            IUnitOfWork uow, Guid jobPostingId, Guid? userId, CancellationToken ct)
        {
            if (userId is not { } uid || uid == Guid.Empty) return false;
            var hm = await JobAccess.PrimaryHiringManagerAsync(uow, jobPostingId, ct);
            return hm != null && hm.UserId == uid;
        }
    }

    // ============================================================
    // POST /api/applications/{id}/request-hm-approval  (Recruiter/chủ tin)
    // ============================================================

    public record RequestHmApprovalCommand(Guid ApplicationId, Guid? ActorId, string? ActorRole)
        : IRequest<Result<bool>>;

    public class RequestHmApprovalCommandHandler : IRequestHandler<RequestHmApprovalCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;
        private readonly IApplicationService _applicationService;

        public RequestHmApprovalCommandHandler(
            IUnitOfWork unitOfWork, INotificationService notifications, IApplicationService applicationService)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
            _applicationService = applicationService;
        }

        public async Task<Result<bool>> Handle(RequestHmApprovalCommand request, CancellationToken ct)
        {
            var (app, job, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.ApplicationId, request.ActorId, request.ActorRole, ct);
            if (app == null || job == null)
                return Result<bool>.Failure(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result<bool>.Failure(JobAccessErrors.ApplicationManageForbidden, CommonErrorCodes.Forbidden);

            if (!ApplicationStatuses.Is(app.Status, ApplicationStatuses.CvSubmitted)
                && !ApplicationStatuses.Is(app.Status, ApplicationStatuses.Invited))
                return Result<bool>.Failure("Chỉ gửi duyệt được hồ sơ vừa nộp CV và chưa qua sàng lọc.");

            var hm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, job.Id, ct);

            // Tin chưa gán Hiring Manager thì KHÔNG có cổng nào để chờ (ADR-061: cổng suy ra từ việc
            // có người được gán). Trước đây chỗ này trả lỗi, nghĩa là nút "Duyệt hồ sơ" của Recruiter
            // chết cứng trên những tin cũ — nay hồ sơ đi thẳng sang hàng chờ xếp lịch, đúng như hành
            // vi trước khi có vai trò Hiring Manager.
            if (hm == null)
            {
                var advance = await _applicationService.AcceptApplicationAsync(app.Id, ct);
                if (advance.IsFailure) return advance;
                await _unitOfWork.SaveChangesAsync(ct);
                return Result.Success(true);
            }

            app.Status = ApplicationStatuses.HmReview;
            app.HmDecision = HmDecision.Pending;
            app.HmDecisionByUserId = null;
            app.HmDecidedAt = null;
            app.HmDecisionNote = null;
            app.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = hm.UserId,
                Type = "pending",
                Title = "Hồ sơ chờ bạn duyệt",
                Body = $"Ứng viên {app.CandidateName} ứng tuyển vị trí \"{job.Title}\" đang chờ bạn duyệt.",
                Link = $"/hm/shortlists",
                DedupKey = $"hm_shortlist:{app.Id}",
                IsRead = false,
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(hm.UserId, "ReceiveUserNotification",
                new { Type = "ShortlistPendingApproval", ApplicationId = app.Id, JobTitle = job.Title }, ct);

            return Result.Success(true);
        }
    }

    // ============================================================
    // POST /api/applications/{id}/hm-decision  (Hiring Manager)
    // ============================================================

    /// <param name="Availabilities">
    /// Khung giờ HM có mặt được cho vòng 1 (ADR-067), gửi KÈM lệnh duyệt. Bỏ trống chỉ hợp lệ khi
    /// vòng 1 của tin đã có khung giờ còn hiệu lực từ lần duyệt trước — duyệt người thứ hai trong
    /// cùng đợt không phải khai lại lịch.
    /// </param>
    public record HmDecideApplicationCommand(
        Guid ApplicationId, string Decision, string? Note, Guid? ActorId, string? ActorRole,
        IReadOnlyList<HmAvailabilityWindowInput>? Availabilities = null)
        : IRequest<Result<bool>>;

    public class HmDecideApplicationCommandHandler : IRequestHandler<HmDecideApplicationCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;
        private readonly IApplicationService _applicationService;

        public HmDecideApplicationCommandHandler(
            IUnitOfWork unitOfWork, INotificationService notifications, IApplicationService applicationService)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
            _applicationService = applicationService;
        }

        public async Task<Result<bool>> Handle(HmDecideApplicationCommand request, CancellationToken ct)
        {
            var decision = (request.Decision ?? string.Empty).Trim().ToLowerInvariant();
            if (decision != HmDecision.Approved && decision != HmDecision.Rejected)
                return Result<bool>.Failure("Quyết định phải là 'approved' hoặc 'rejected'.");

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(request.ApplicationId, ct);
            if (app == null)
                return Result<bool>.Failure(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);

            if (!await ShortlistGateSupport.IsPrimaryHmAsync(_unitOfWork, app.JobPostingId, request.ActorId, ct))
                return Result<bool>.Failure(
                    "Chỉ Hiring Manager phụ trách tin này mới duyệt được hồ sơ.", CommonErrorCodes.Forbidden);

            if (!ApplicationStatuses.Is(app.Status, ApplicationStatuses.HmReview)
                || !HmDecision.Is(app.HmDecision, HmDecision.Pending))
                return Result<bool>.Failure("Hồ sơ này không ở trạng thái chờ bạn duyệt.");

            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            if (decision == HmDecision.Rejected && note == null)
                return Result<bool>.Failure("Vui lòng nhập lý do khi từ chối hồ sơ.");

            // ---- Lịch rảnh đi CÙNG lệnh duyệt (ADR-067) -------------------------------------
            // Duyệt mà không có giờ nào để xếp thì hồ sơ rơi vào hàng chờ xếp lịch rồi đứng im ở đó:
            // Recruiter mở ra thấy một danh sách không thao tác được và không biết phải hỏi ai.
            // Nên khung giờ là điều kiện của việc DUYỆT, không phải một việc để làm sau.
            if (decision == HmDecision.Approved)
            {
                var (windows, windowError) = HmAvailabilitySupport.Sanitize(request.Availabilities);
                if (windowError != null) return Result<bool>.Failure(windowError);

                if (windows.Count > 0)
                    await HmAvailabilityWriteGate.AppendAsync(
                        _unitOfWork, app.JobPostingId, 1, request.ActorId!.Value, windows, ct);

                var active = await HmAvailabilitySupport.ActiveWindowsAsync(_unitOfWork, app.JobPostingId, 1, ct);
                if (active.Count + windows.Count == 0)
                    return Result<bool>.Failure(
                        "Vui lòng gửi kèm khung giờ bạn có thể tham gia phỏng vấn vòng 1 — "
                        + "Recruiter sẽ xếp lịch cho ứng viên trùng khung giờ đó.");
            }

            app.HmDecision = decision;
            app.HmDecisionByUserId = request.ActorId;
            app.HmDecidedAt = DateTimeOffset.UtcNow;
            app.HmDecisionNote = note;
            app.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId,
                decision == HmDecision.Approved ? "hm_shortlist_approved" : "hm_shortlist_rejected",
                "Application", app.Id,
                AuditMetadata.Serialize(new { jobTitle = job?.Title, candidate = app.CandidateName, note }), ct);

            // Chủ tin là người hành động tiếp theo (xếp lịch, hoặc đóng hồ sơ) → phải biết ngay.
            if (job != null)
            {
                await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = job.CreatedByUserId,
                    Type = decision == HmDecision.Approved ? "approved" : "rejected",
                    Title = decision == HmDecision.Approved
                        ? "Hiring Manager đã duyệt — hồ sơ chờ bạn xếp lịch"
                        : "Hiring Manager đã từ chối hồ sơ",
                    Body = $"{app.CandidateName} — vị trí \"{job.Title}\"."
                           + (decision == HmDecision.Approved
                               ? " Hãy xếp ca phỏng vấn nằm trong khung giờ Hiring Manager đã gửi."
                               : string.Empty)
                           + (note != null ? $" Lý do: {note}" : string.Empty),
                    Link = $"/recruiter/candidates/{app.Id}",
                    DedupKey = $"hm_shortlist_decided:{app.Id}",
                    IsRead = false,
                }, ct);
            }

            // Từ chối = đóng hồ sơ luôn: dùng lại đường loại hồ sơ sẵn có (gửi thư cảm ơn, huỷ
            // mọi lịch còn hiệu lực) thay vì tự viết một nhánh đóng hồ sơ thứ hai.
            //
            // GỌI TRƯỚC khi lưu, KHÔNG phải sau. `ApplicationService` và handler này cùng vòng đời
            // Scoped nên dùng chung một `IUnitOfWork`: `RejectApplicationAsync` tự `SaveChanges`, và
            // lượt lưu đó commit luôn quyết định + thông báo đang chờ ở trên — thành một giao dịch.
            //
            // Thứ tự cũ (lưu trước, đóng hồ sơ sau) để lại trạng thái nửa vời nếu bước sau hỏng:
            // hồ sơ mắc ở `hm_review` với `hmDecision = rejected`, mà handler này TỪ CHỐI chạy lại
            // (đòi `pending`), nút loại CV thì tắt ở `hm_review` — không lối ra, và ứng viên không
            // hề được báo.
            if (decision == HmDecision.Rejected)
            {
                var reject = await _applicationService.RejectApplicationAsync(app.Id, ct);
                if (reject.IsFailure) return reject;
            }
            else
            {
                // Duyệt xong là hồ sơ ĐI TIẾP ngay, không dừng lại ở `hm_review` chờ Recruiter bấm
                // thêm một nút "duyệt" nữa (ADR-067). Trạng thái sau đây là `screening` — đúng bước
                // "chờ xếp lịch" trên thanh quy trình, nên Recruiter mở màn ra là thấy việc của mình.
                //
                // Dùng lại `AcceptApplicationAsync` thay vì tự gán trạng thái: hàm đó còn mở vòng 1
                // để xếp lịch (`InterviewInvite`), mà chép lại phần đó là chép một luật đã kiểm chứng.
                var advance = await _applicationService.AcceptApplicationAsync(app.Id, ct);
                if (advance.IsFailure) return advance;
            }

            await _unitOfWork.SaveChangesAsync(ct);

            if (job != null)
            {
                await _notifications.PublishUserEventAsync(job.CreatedByUserId, "ReceiveUserNotification",
                    new { Type = "ShortlistDecided", ApplicationId = app.Id, Decision = decision }, ct);
            }

            return Result.Success(true);
        }
    }

    // ============================================================
    // POST /api/applications/{id}/hm-bypass  (HR Admin / Super Admin)
    // ============================================================

    /// <summary>
    /// Quản trị viên vượt cổng duyệt của Hiring Manager (HM nghỉ, hoặc gấp).
    /// Lý do BẮT BUỘC + audit log + <b>báo cho chính HM bị vượt</b>: một cái vượt cổng im lặng
    /// mới là thất bại quản trị, còn vượt cổng có dấu vết là nghiệp vụ bình thường.
    /// </summary>
    public record BypassHmApprovalCommand(Guid ApplicationId, string? Reason, Guid? ActorId, string? ActorRole)
        : IRequest<Result<bool>>;

    public class BypassHmApprovalCommandHandler : IRequestHandler<BypassHmApprovalCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;
        private readonly IApplicationService _applicationService;

        public BypassHmApprovalCommandHandler(
            IUnitOfWork unitOfWork, INotificationService notifications, IApplicationService applicationService)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
            _applicationService = applicationService;
        }

        public async Task<Result<bool>> Handle(BypassHmApprovalCommand request, CancellationToken ct)
        {
            if (!RoleNames.IsAdmin(request.ActorRole))
                return Result<bool>.Failure(
                    "Chỉ HR Admin hoặc Super Admin mới vượt được cổng duyệt.", CommonErrorCodes.Forbidden);

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (reason == null || reason.Length < 10)
                return Result<bool>.Failure("Vui lòng nhập lý do vượt cổng duyệt (tối thiểu 10 ký tự).");

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(request.ApplicationId, ct);
            if (app == null)
                return Result<bool>.Failure(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);

            if (!ApplicationStatuses.Is(app.Status, ApplicationStatuses.HmReview)
                || !HmDecision.Is(app.HmDecision, HmDecision.Pending))
                return Result<bool>.Failure("Hồ sơ này không ở trạng thái chờ Hiring Manager duyệt.");

            app.HmDecision = HmDecision.Bypassed;
            app.HmDecisionByUserId = request.ActorId;
            app.HmDecidedAt = DateTimeOffset.UtcNow;
            app.HmDecisionNote = reason;
            app.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            var hm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, app.JobPostingId, ct);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "hm_shortlist_bypassed",
                "Application", app.Id,
                AuditMetadata.Serialize(new
                {
                    jobTitle = job?.Title, candidate = app.CandidateName,
                    hiringManagerUserId = hm?.UserId, reason,
                }), ct);

            if (hm != null)
            {
                await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = hm.UserId,
                    Type = "system",
                    Title = "Cổng duyệt của bạn đã bị vượt",
                    Body = $"Hồ sơ {app.CandidateName} — vị trí \"{job?.Title}\" đã được duyệt thay. Lý do: {reason}",
                    Link = "/hm/shortlists",
                    DedupKey = $"hm_shortlist_bypassed:{app.Id}",
                    IsRead = false,
                }, ct);
            }

            // Vượt cổng cũng phải đẩy hồ sơ đi tiếp như khi HM duyệt (ADR-067) — nếu không, "vượt
            // cổng" chỉ mở một cái cửa rồi để hồ sơ đứng nguyên tại chỗ, đúng thứ bế tắc mà thao tác
            // này sinh ra để gỡ. Lịch rảnh của HM thì KHÔNG bịa thay họ: Recruiter sẽ bị chặn ở bước
            // xếp ca kèm câu nói rõ còn thiếu gì.
            var advance = await _applicationService.AcceptApplicationAsync(app.Id, ct);
            if (advance.IsFailure) return advance;

            await _unitOfWork.SaveChangesAsync(ct);

            if (hm != null)
            {
                await _notifications.PublishUserEventAsync(hm.UserId, "ReceiveUserNotification",
                    new { Type = "ShortlistBypassed", ApplicationId = app.Id }, ct);
            }

            return Result.Success(true);
        }
    }
}
