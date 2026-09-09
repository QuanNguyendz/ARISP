using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Emails;
using ARI.Application.HiringTeam;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Offers
{
    internal static class OfferSupport
    {
        /// <summary>
        /// Nạp offer + hồ sơ + tin, kèm mức quyền của người gọi trên tin đó. Mọi lệnh dưới đây bắt
        /// đầu bằng bước này nên không lệnh nào có thể quên kiểm quyền.
        /// </summary>
        public static async Task<(Offer? offer, ARI.Domain.Entities.Application? app, JobPosting? job, JobAccessLevel level)>
            LoadAsync(IUnitOfWork uow, Guid offerId, Guid? userId, string? role, CancellationToken ct)
        {
            var offer = await uow.Repository<Offer>().GetByIdAsync(offerId, ct);
            if (offer == null) return (null, null, null, JobAccessLevel.None);

            var (app, job, level) = await JobAccess.EvaluateApplicationAsync(uow, offer.ApplicationId, userId, role, ct);
            return (offer, app, job, level);
        }

        /// <summary>
        /// Thêm thông báo cho nhân sự, BỎ QUA nếu đã có bản cùng (người nhận, dedupKey).
        ///
        /// `notifications` có UNIQUE trên `(recipient_user_id, dedup_key)`, nên thêm lần hai là
        /// DbUpdateException → HTTP 500 và **cả lệnh nghiệp vụ bị rollback**. Không hiếm chút nào:
        /// Hiring Manager trả thư mời về bản nháp rồi người soạn gửi duyệt lại là đi đúng vào đó —
        /// và khi ấy thư **không bao giờ gửi duyệt lại được**. Cùng cách làm đã có ở
        /// `ApplicationService.RejectApplicationAsync`, gom vào đây để chỗ gọi khỏi phải nhớ.
        ///
        /// KHÔNG nhận `INotificationService`: helper này chỉ ghi DB. Nhận vào rồi không dùng khiến
        /// người đọc tưởng nó tự phát realtime, nên chỗ gọi lại phát thêm lần nữa.
        /// </summary>
        public static async Task NotifyStaffAsync(
            IUnitOfWork uow, Guid recipientUserId,
            string type, string title, string body, string link, string dedupKey, CancellationToken ct)
        {
            var existing = await uow.Repository<Notification>()
                .FindAsync(n => n.RecipientUserId == recipientUserId && n.DedupKey == dedupKey, ct);
            if (existing.Any()) return;

            await uow.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = recipientUserId,
                Type = type, Title = title, Body = body, Link = link,
                DedupKey = dedupKey, IsRead = false,
            }, ct);
        }
    }

    // ============================================================
    // GET /api/offers  |  GET /api/offers/{id}
    // ============================================================

    public record GetOffersQuery(Guid? UserId, string? Role, string? Status) : IRequest<Result<List<OfferDto>>>;

    public class GetOffersQueryHandler : IRequestHandler<GetOffersQuery, Result<List<OfferDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        public GetOffersQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<List<OfferDto>>> Handle(GetOffersQuery request, CancellationToken ct)
        {
            var scope = await JobAccess.ScopedJobIdsAsync(_unitOfWork, request.UserId, request.Role, ct);

            // Lọc phạm vi + trạng thái ở SQL, không kéo cả bảng `offers` về rồi lọc trong bộ nhớ:
            // một Hiring Manager phụ trách MỘT tin mở màn thư mời cũng phải nạp toàn bộ thư mời của
            // công ty rồi vứt gần hết. Chi phí tăng tuyến tính theo tổng số thư mời, với mọi người xem.
            var status = request.Status?.Trim();
            var offers = (await _unitOfWork.Repository<Offer>().QueryAsync(q =>
            {
                var query = scope == null ? q : q.Where(o => scope.Contains(o.JobPostingId));
                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(o => o.Status == status);
                return query.OrderByDescending(o => o.CreatedAt);
            }, ct)).ToList();

            if (offers.Count == 0) return Result.Success(new List<OfferDto>());

            var appIds = offers.Select(o => o.ApplicationId).Distinct().ToList();
            var apps = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .FindAsync(a => appIds.Contains(a.Id), ct)).ToDictionary(a => a.Id);

            var jobIds = offers.Select(o => o.JobPostingId).Distinct().ToList();
            var jobs = (await _unitOfWork.Repository<JobPosting>()
                .FindAsync(j => jobIds.Contains(j.Id), ct)).ToDictionary(j => j.Id);

            return Result.Success(offers.Select(o =>
            {
                apps.TryGetValue(o.ApplicationId, out var app);
                jobs.TryGetValue(o.JobPostingId, out var job);
                return OfferDto.FromEntity(o, app, job);
            }).ToList());
        }
    }

    public record GetOfferByIdQuery(Guid Id, Guid? UserId, string? Role) : IRequest<Result<OfferDto>>;

    public class GetOfferByIdQueryHandler : IRequestHandler<GetOfferByIdQuery, Result<OfferDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        public GetOfferByIdQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OfferDto>> Handle(GetOfferByIdQuery request, CancellationToken ct)
        {
            var (offer, app, job, level) = await OfferSupport.LoadAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (offer == null)
                return Result.Failure<OfferDto>("Không tìm thấy thư mời nhận việc.", CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.TeamMember)
                return Result.Failure<OfferDto>("Bạn không có quyền xem thư mời này.", CommonErrorCodes.Forbidden);

            return Result.Success(OfferDto.FromEntity(offer, app, job));
        }
    }

    // ============================================================
    // POST /api/offers  (tạo bản nháp)
    // ============================================================

    public record CreateOfferCommand(UpsertOfferRequest Request, Guid? UserId, string? Role) : IRequest<Result<OfferDto>>;

    public class CreateOfferCommandHandler : IRequestHandler<CreateOfferCommand, Result<OfferDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        public CreateOfferCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OfferDto>> Handle(CreateOfferCommand request, CancellationToken ct)
        {
            if (request.UserId is not { } actorId || actorId == Guid.Empty)
                return Result.Failure<OfferDto>("Không xác định được người dùng.", CommonErrorCodes.Forbidden);

            var (app, job, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.Request.ApplicationId, request.UserId, request.Role, ct);
            if (app == null || job == null)
                return Result.Failure<OfferDto>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result.Failure<OfferDto>(JobAccessErrors.ApplicationManageForbidden, CommonErrorCodes.Forbidden);

            // Chỉ ra offer cho người đã qua HẾT các vòng (ADR-053).
            if (!ApplicationStatuses.Is(app.Status, ApplicationStatuses.Pass))
                return Result.Failure<OfferDto>(
                    "Chỉ tạo thư mời cho ứng viên đã đạt toàn bộ các vòng phỏng vấn.");

            // Một offer sống mỗi hồ sơ — chặn ở DB bằng unique index có lọc; kiểm ở đây để trả
            // thông báo hiểu được thay vì lỗi ràng buộc thô.
            var live = (await _unitOfWork.Repository<Offer>()
                .FindAsync(o => o.ApplicationId == app.Id, ct))
                .FirstOrDefault(o => !OfferStatus.IsClosed(o.Status));
            if (live != null)
                return Result.Failure<OfferDto>(
                    "Hồ sơ này đã có một thư mời đang hiệu lực. Hãy thu hồi thư cũ trước khi tạo thư mới.");

            var r = request.Request;

            // Điền sẵn từ đề xuất của người chốt kết quả (ADR-061, Phase 3c) — công sức HM đã bỏ ra
            // khi chốt không nên phải gõ lại.
            var suggestion = await LatestSuggestionAsync(_unitOfWork, app.Id, ct);

            var offer = new Offer
            {
                ApplicationId = app.Id,
                JobPostingId = app.JobPostingId,
                Status = OfferStatus.Draft,
                Position = r.Position ?? job.Title,
                SalaryAmount = r.SalaryAmount ?? suggestion?.SuggestedSalaryMax ?? suggestion?.SuggestedSalaryMin,
                SalaryCurrency = r.SalaryCurrency ?? suggestion?.SuggestedSalaryCurrency ?? "VND",
                SalaryPeriod = r.SalaryPeriod ?? "month",
                Bonus = r.Bonus,
                Benefits = r.Benefits,
                EmploymentType = r.EmploymentType ?? job.EmploymentType,
                WorkLocation = r.WorkLocation ?? job.Location,
                StartDate = r.StartDate,
                ExpiresAt = r.ExpiresAt,
                Notes = r.Notes,
                CreatedByUserId = actorId,
            };

            await _unitOfWork.Repository<Offer>().AddAsync(offer, ct);
            await AdminSupport.WriteAuditAsync(_unitOfWork, actorId, "offer_created", nameof(Offer), offer.Id,
                AuditMetadata.Serialize(new { candidate = app.CandidateName, jobTitle = job.Title }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(OfferDto.FromEntity(offer, app, job));
        }

        /// <summary>Đề xuất lương/cấp bậc mới nhất của người chốt kết quả phỏng vấn.</summary>
        internal static async Task<HrReview?> LatestSuggestionAsync(
            IUnitOfWork uow, Guid applicationId, CancellationToken ct)
        {
            var evalIds = (await uow.Repository<Evaluation>()
                .FindAsync(e => e.ApplicationId == applicationId && e.SessionType == "real", ct))
                .Select(e => e.Id).ToHashSet();
            if (evalIds.Count == 0) return null;

            return (await uow.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId), ct))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault(r => r.SuggestedSalaryMin.HasValue || r.SuggestedSalaryMax.HasValue);
        }
    }

    // ============================================================
    // PUT /api/offers/{id}  (sửa bản nháp)
    // ============================================================

    public record UpdateOfferCommand(Guid Id, UpsertOfferRequest Request, Guid? UserId, string? Role)
        : IRequest<Result<OfferDto>>;

    public class UpdateOfferCommandHandler : IRequestHandler<UpdateOfferCommand, Result<OfferDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        public UpdateOfferCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result<OfferDto>> Handle(UpdateOfferCommand request, CancellationToken ct)
        {
            var (offer, app, job, level) = await OfferSupport.LoadAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (offer == null)
                return Result.Failure<OfferDto>("Không tìm thấy thư mời nhận việc.", CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result.Failure<OfferDto>("Bạn không có quyền sửa thư mời này.", CommonErrorCodes.Forbidden);
            if (!OfferStatus.Is(offer.Status, OfferStatus.Draft))
                return Result.Failure<OfferDto>("Chỉ sửa được thư mời khi còn là bản nháp.");

            var r = request.Request;
            offer.Position = r.Position ?? offer.Position;
            offer.SalaryAmount = r.SalaryAmount ?? offer.SalaryAmount;
            offer.SalaryCurrency = r.SalaryCurrency ?? offer.SalaryCurrency;
            offer.SalaryPeriod = r.SalaryPeriod ?? offer.SalaryPeriod;
            offer.Bonus = r.Bonus;
            offer.Benefits = r.Benefits;
            // `?? offer.X` giống mọi trường lân cận: PUT thiếu trường thì GIỮ giá trị cũ, không xoá.
            // Trước đây hai dòng này gán thẳng, nên một PUT chỉ đổi lương sẽ âm thầm xoá hình thức
            // làm việc và nơi làm việc mà CreateOfferCommand đã điền sẵn từ tin — thư mời gửi đi
            // thiếu hai dòng đó, không dấu hiệu gì.
            offer.EmploymentType = r.EmploymentType ?? offer.EmploymentType;
            offer.WorkLocation = r.WorkLocation ?? offer.WorkLocation;
            offer.StartDate = r.StartDate ?? offer.StartDate;
            offer.ExpiresAt = r.ExpiresAt ?? offer.ExpiresAt;
            offer.Notes = r.Notes;
            offer.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<Offer>().Update(offer);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(OfferDto.FromEntity(offer, app, job));
        }
    }

    // ============================================================
    // POST /api/offers/{id}/submit  (gửi duyệt)
    // ============================================================

    public record SubmitOfferCommand(Guid Id, Guid? UserId, string? Role) : IRequest<Result<bool>>;

    public class SubmitOfferCommandHandler : IRequestHandler<SubmitOfferCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public SubmitOfferCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<bool>> Handle(SubmitOfferCommand request, CancellationToken ct)
        {
            var (offer, app, job, level) = await OfferSupport.LoadAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (offer == null || app == null || job == null)
                return Result<bool>.Failure("Không tìm thấy thư mời nhận việc.", CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result<bool>.Failure("Bạn không có quyền gửi duyệt thư mời này.", CommonErrorCodes.Forbidden);
            if (!OfferStatus.Is(offer.Status, OfferStatus.Draft))
                return Result<bool>.Failure("Chỉ gửi duyệt được thư mời đang ở bản nháp.");

            // Ba thứ này thiếu thì người duyệt không có gì để duyệt.
            if (offer.SalaryAmount is null or <= 0)
                return Result<bool>.Failure("Vui lòng nhập mức lương trước khi gửi duyệt.");
            if (offer.StartDate == null)
                return Result<bool>.Failure("Vui lòng nhập ngày dự kiến đi làm trước khi gửi duyệt.");
            if (offer.ExpiresAt == null)
                return Result<bool>.Failure("Vui lòng nhập hạn phản hồi trước khi gửi duyệt.");

            offer.Status = OfferStatus.PendingApproval;
            offer.RejectedReason = null;
            offer.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<Offer>().Update(offer);

            // Người duyệt: Hiring Manager của tin; tin chưa gán HM thì rơi về quản trị viên.
            var hm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, job.Id, ct);
            if (hm != null)
            {
                await OfferSupport.NotifyStaffAsync(_unitOfWork, hm.UserId,
                    "pending", "Thư mời nhận việc chờ bạn duyệt",
                    $"Ứng viên {app.CandidateName} — vị trí \"{job.Title}\".",
                    "/hm/offers", $"offer_pending:{offer.Id}", ct);
            }
            else
            {
                // Tin chưa gán HM: trước đây chỉ phát một sự kiện realtime cho nhóm `hr_admin` —
                // **không ai đang mở ứng dụng lúc đó thì không còn dấu vết nào**. Thư nằm im ở
                // `pending_approval` vô thời hạn, mà nó vẫn giữ chỗ "một thư mời sống" của hồ sơ nên
                // cũng không soạn được thư thay thế. Ghi thông báo BỀN cho từng quản trị viên.
                var admins = await _unitOfWork.Repository<User>().QueryAsync(
                    q => q.Where(u => u.IsActive
                                      && (u.Role == RoleNames.HrAdmin || u.Role == RoleNames.SuperAdmin))
                          .Select(u => u.Id), ct);

                foreach (var adminId in admins)
                {
                    await OfferSupport.NotifyStaffAsync(_unitOfWork, adminId,
                        "pending", "Thư mời nhận việc chờ duyệt",
                        $"Ứng viên {app.CandidateName} — vị trí \"{job.Title}\" (tin chưa gán Hiring Manager).",
                        "/hr/offers", $"offer_pending:{offer.Id}", ct);
                }
            }

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.UserId, "offer_submitted", nameof(Offer), offer.Id,
                AuditMetadata.Serialize(new { candidate = app.CandidateName, salary = offer.SalaryAmount }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (hm == null)
            {
                await _notifications.PublishGroupEventAsync("hr_admin", "ReceiveUserNotification",
                    new { Type = "OfferPendingApproval", OfferId = offer.Id }, ct);
            }
            else
            {
                await _notifications.PublishUserEventAsync(hm.UserId, "ReceiveUserNotification",
                    new { Type = "OfferPendingApproval", OfferId = offer.Id }, ct);
            }

            return Result.Success(true);
        }
    }

    // ============================================================
    // POST /api/offers/{id}/decide  (duyệt / trả về nháp)
    // ============================================================

    public record DecideOfferCommand(Guid Id, string Decision, string? Note, Guid? UserId, string? Role)
        : IRequest<Result<bool>>;

    public class DecideOfferCommandHandler : IRequestHandler<DecideOfferCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public DecideOfferCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<bool>> Handle(DecideOfferCommand request, CancellationToken ct)
        {
            var decision = (request.Decision ?? string.Empty).Trim().ToLowerInvariant();
            if (decision != "approved" && decision != "rejected")
                return Result<bool>.Failure("Quyết định phải là 'approved' hoặc 'rejected'.");

            var (offer, app, job, _) = await OfferSupport.LoadAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (offer == null || app == null || job == null)
                return Result<bool>.Failure("Không tìm thấy thư mời nhận việc.", CommonErrorCodes.NotFound);
            if (!OfferStatus.Is(offer.Status, OfferStatus.PendingApproval))
                return Result<bool>.Failure("Thư mời này không ở trạng thái chờ duyệt.");

            // ADR-063 — người CHỐT thư mời là HR Leader, không phải Hiring Manager.
            //
            // Trước đây chỗ này nhận `isTheHm || isAdmin`: HM vừa đề xuất mức lương vừa tự duyệt
            // chính đề xuất của mình. Quy trình nhân sự tách hai vai vì đúng lý do đó — người có
            // nhu cầu tuyển không kiểm soát ngân sách lương.
            //
            // Điều kiện viết lại tường minh chứ không dựa vào policy `OfferApproval` ở controller:
            // policy là lớp ngoài, còn đây là luật nghiệp vụ. Để `isTheHm` nằm lại thì chỉ cần ai
            // đó nới policy là quyền duyệt của HM lặng lẽ sống lại, không dòng nào báo.
            if (!RoleNames.IsAdmin(request.Role))
            {
                var hm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, job.Id, ct);
                var isTheHm = hm != null && hm.UserId == request.UserId;

                return Result<bool>.Failure(
                    isTheHm
                        ? "Hiring Manager đề xuất mức lương nhưng không tự chốt được thư mời — HR Leader là người quyết định cuối cùng."
                        : "Chỉ HR Leader hoặc quản trị viên mới chốt được thư mời.",
                    CommonErrorCodes.Forbidden);
            }

            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            if (decision == "rejected" && note == null)
                return Result<bool>.Failure("Vui lòng nêu rõ cần sửa gì trong thư mời.");

            if (decision == "approved")
            {
                offer.Status = OfferStatus.Approved;
                offer.ApprovedByUserId = request.UserId;
                offer.ApprovedAt = DateTimeOffset.UtcNow;
                offer.ApprovalNote = note;
            }
            else
            {
                // Trả về bản nháp chứ không khép: chủ tin sửa rồi gửi duyệt lại.
                offer.Status = OfferStatus.Draft;
                offer.RejectedReason = note;
            }

            offer.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<Offer>().Update(offer);

            await OfferSupport.NotifyStaffAsync(_unitOfWork, offer.CreatedByUserId,
                decision == "approved" ? "approved" : "rejected",
                decision == "approved" ? "Thư mời đã được duyệt" : "Thư mời cần chỉnh sửa",
                $"{app.CandidateName} — vị trí \"{job.Title}\"." + (note != null ? $" {note}" : string.Empty),
                "/recruiter/offers", $"offer_decided:{offer.Id}:{DateTimeOffset.UtcNow.Ticks}", ct);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.UserId,
                decision == "approved" ? "offer_approved" : "offer_rejected", nameof(Offer), offer.Id,
                AuditMetadata.Serialize(new { candidate = app.CandidateName, note }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(offer.CreatedByUserId, "ReceiveUserNotification",
                new { Type = "OfferDecided", OfferId = offer.Id, Decision = decision }, ct);

            return Result.Success(true);
        }
    }

    // ============================================================
    // POST /api/offers/{id}/send  (gửi cho ứng viên)
    // ============================================================

    public record SendOfferCommand(Guid Id, Guid? UserId, string? Role, EmailOverride? EmailOverride = null)
        : IRequest<Result<bool>>;

    public class SendOfferCommandHandler : IRequestHandler<SendOfferCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;
        private readonly IConfiguration _configuration;

        public SendOfferCommandHandler(
            IUnitOfWork unitOfWork, INotificationService notifications, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
            _configuration = configuration;
        }

        public async Task<Result<bool>> Handle(SendOfferCommand request, CancellationToken ct)
        {
            var (offer, app, job, level) = await OfferSupport.LoadAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (offer == null || app == null || job == null)
                return Result<bool>.Failure("Không tìm thấy thư mời nhận việc.", CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.Owner)
                return Result<bool>.Failure("Bạn không có quyền gửi thư mời này.", CommonErrorCodes.Forbidden);
            if (!OfferStatus.Is(offer.Status, OfferStatus.Approved))
                return Result<bool>.Failure("Chỉ gửi được thư mời đã qua duyệt.");

            offer.Status = OfferStatus.Sent;
            offer.SentAt = DateTimeOffset.UtcNow;
            offer.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<Offer>().Update(offer);

            // Hồ sơ chuyển sang "offer" lúc GỬI, không phải lúc tạo nháp — bản nháp không phải lời hứa.
            app.Status = ApplicationStatuses.Offer;
            app.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.UserId, "offer_sent", nameof(Offer), offer.Id,
                AuditMetadata.Serialize(new { candidate = app.CandidateName, salary = offer.SalaryAmount }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var baseUrl = _configuration["Frontend:CandidateBaseUrl"];
            var mail = OfferEmail.Build(offer, app, job, baseUrl);
            await CandidateEmailSender.SendAsync(
                _unitOfWork, _notifications, EmailTemplateKeys.OfferSent,
                new RenderedEmail(mail.Subject, mail.Html, app.CandidateEmail, app.CandidateName),
                request.EmailOverride,
                applicationId: app.Id, jobPostingId: app.JobPostingId, sentByUserId: request.UserId, ct);

            if (app.CandidateAccountId is { } accountId)
            {
                await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    CandidateAccountId = accountId,
                    Type = "result",
                    Title = "Bạn nhận được thư mời nhận việc",
                    Body = $"Vị trí {offer.Position ?? job.Title}. Hãy xem và phản hồi trong hồ sơ của bạn.",
                    Link = $"/candidate/applications/{app.Id}/offer",
                    DedupKey = $"offer_sent:{offer.Id}",
                    IsRead = false,
                }, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                await _notifications.PublishUserEventAsync(accountId, "ReceiveUserNotification",
                    new { Type = "OfferReceived", ApplicationId = app.Id }, ct);
            }

            return Result.Success(true);
        }
    }

    // ============================================================
    // POST /api/offers/{id}/withdraw
    // ============================================================

    public record WithdrawOfferCommand(Guid Id, string? Reason, Guid? UserId, string? Role) : IRequest<Result<bool>>;

    public class WithdrawOfferCommandHandler : IRequestHandler<WithdrawOfferCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public WithdrawOfferCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<bool>> Handle(WithdrawOfferCommand request, CancellationToken ct)
        {
            if (!RoleNames.IsAdmin(request.Role))
                return Result<bool>.Failure(
                    "Chỉ HR Admin hoặc Super Admin mới thu hồi được thư mời.", CommonErrorCodes.Forbidden);

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (reason == null)
                return Result<bool>.Failure("Vui lòng nhập lý do thu hồi thư mời.");

            var (offer, app, job, _) = await OfferSupport.LoadAsync(
                _unitOfWork, request.Id, request.UserId, request.Role, ct);
            if (offer == null || app == null)
                return Result<bool>.Failure("Không tìm thấy thư mời nhận việc.", CommonErrorCodes.NotFound);
            if (OfferStatus.IsClosed(offer.Status) || OfferStatus.Is(offer.Status, OfferStatus.Accepted))
                return Result<bool>.Failure("Thư mời này đã khép, không thu hồi được.");

            var wasSent = OfferStatus.Is(offer.Status, OfferStatus.Sent);

            offer.Status = OfferStatus.Withdrawn;
            offer.WithdrawnByUserId = request.UserId;
            offer.WithdrawnReason = reason;
            offer.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<Offer>().Update(offer);

            // Thu hồi thư ĐÃ GỬI thì hồ sơ đóng lại; thu hồi bản chưa gửi thì ứng viên chưa biết
            // gì cả, hồ sơ quay về "pass" để còn ra thư mời khác.
            app.Status = wasSent ? ApplicationStatuses.NotPass : ApplicationStatuses.Pass;
            app.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.UserId, "offer_withdrawn", nameof(Offer), offer.Id,
                AuditMetadata.Serialize(new { candidate = app.CandidateName, jobTitle = job?.Title, wasSent, reason }), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (wasSent && app.CandidateAccountId is { } accountId)
            {
                await _notifications.PublishUserEventAsync(accountId, "ReceiveApplicationStatusUpdate",
                    new { Id = app.Id, JobPostingId = app.JobPostingId, Status = app.Status }, ct);
            }

            return Result.Success(true);
        }
    }
}
