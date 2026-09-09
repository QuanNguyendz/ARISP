using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.RecruitmentRequests
{
    // ===================================================================================
    //  Phiếu yêu cầu tuyển dụng — vòng đời (ADR-063)
    //
    //  HM lập phiếu (pending) → HR Leader duyệt (approved + phân công Recruiter)
    //                        └→ HR Leader từ chối kèm lý do (rejected) → HM sửa → gửi lại (pending)
    //
    //  Và từ `approved`, khi nhu cầu đổi (ADR-066) — chỉ khi phiếu CHƯA dựng thành tin:
    //                        ├→ mở lại để sửa (pending, phân công bị gỡ theo chữ ký)
    //                        └→ đóng phiếu vì hết nhu cầu (cancelled)
    //
    //  HAI RÀNG BUỘC KHÔNG ĐƯỢC PHÉP LÁCH:
    //  1. Không ai duyệt được phiếu của CHÍNH MÌNH — kể cả Super Admin. Đặc tả chỉ cấm Hiring
    //     Manager, nhưng HM vốn đã không qua nổi policy duyệt, nên phát biểu hẹp như vậy là một
    //     câu luôn đúng và không bảo vệ gì. Chỗ hở thật là một HR Leader tự lập phiếu cho đội
    //     mình rồi tự bấm duyệt. Chặn theo NGƯỜI, không theo vai trò.
    //  2. Từ chối phải có lý do — phiếu bị trả về mà không nói vì sao thì HM không biết sửa gì,
    //     và vòng lặp sửa–gửi lại thành đoán mò. Cùng ngưỡng ≥10 ký tự với `fallback_reason`
    //     của ADR-061.
    // ===================================================================================

    internal static class RecruitmentRequestSupport
    {
        /// <summary>Độ dài tối thiểu của lý do từ chối — bằng ngưỡng <c>fallback_reason</c> (ADR-061).</summary>
        public const int MinReasonLength = 10;

        public static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// Nạp phiếu cho một thao tác <b>thu hồi phê duyệt</b> (ADR-066) và kiểm đủ bốn điều kiện.
        ///
        /// Dùng chung cho cả <i>mở lại để sửa</i> lẫn <i>đóng phiếu</i>: hai thao tác chỉ khác đích đến, mọi
        /// ràng buộc đều giống nhau. Viết riêng hai bản thì lần sửa sau chỉ một bản được sửa — và bản bị quên
        /// chính là cái cho phép thu hồi một phiếu đã thành tin.
        /// </summary>
        public static async Task<(RecruitmentRequest? Request, string? Reason, string? Error, string? ErrorCode)>
            LoadRevocableAsync(IUnitOfWork uow, Guid id, string? rawReason, Guid? actorId, string? actorRole,
                               CancellationToken ct)
        {
            var req = await GetAsync(uow, id, ct);
            if (req == null)
                return (null, null, "Không tìm thấy phiếu yêu cầu tuyển dụng.", CommonErrorCodes.NotFound);

            // Chủ phiếu hoặc quản trị viên. Recruiter được phân công cố ý KHÔNG có quyền này: họ thực thi
            // nhu cầu chứ không phát sinh hay huỷ bỏ nó.
            if (req.RequestedByUserId != actorId && !RoleNames.IsAdmin(actorRole))
                return (null, null, "Chỉ người lập phiếu hoặc quản trị viên mới thu hồi được phiếu đã duyệt.",
                        CommonErrorCodes.Forbidden);

            if (!RecruitmentRequestStatus.IsRevocable(req.Status))
                return (null, null, "Chỉ thu hồi được phiếu đang ở trạng thái đã duyệt.", null);

            // RÀNG BUỘC QUAN TRỌNG NHẤT: phiếu đã sinh ra tin thì TIN là nguồn sự thật, không phải phiếu nữa.
            // Mở lại phiếu lúc đó tạo ra một tin đang chạy mà phiếu nguồn của nó lại đang "chờ duyệt" — và unique
            // index một-phiếu-một-tin (ADR-063) khiến vòng dựng tin lần hai chết bằng lỗi 409 không ai hiểu.
            var jobs = await GetRecruitmentRequestsQueryHandler.JobByRequestAsync(
                uow, new List<Guid> { req.Id }, ct);
            if (jobs.Count > 0)
                return (null, null,
                    "Phiếu này đã dựng thành tin tuyển dụng nên không sửa hay đóng ở đây được nữa. "
                    + "Hãy thao tác trên chính tin đó (sửa nội dung hoặc đóng tin).", null);

            var reason = Trim(rawReason);
            if (reason == null || reason.Length < MinReasonLength)
                return (null, null,
                    $"Vui lòng nêu rõ lý do (tối thiểu {MinReasonLength} ký tự) — "
                    + "người duyệt và Recruiter đang cầm việc đều đọc dòng này.", null);

            return (req, reason, null, null);
        }

        /// <summary>
        /// Ai cần biết khi một phiếu bị thu hồi: Recruiter đang cầm việc, HR Leader đã duyệt, và chủ phiếu
        /// nếu không phải chính người bấm nút.
        ///
        /// PHẢI gọi TRƯỚC khi sửa phiếu. Lệnh mở lại xoá <c>AssignedRecruiterId</c> và <c>ReviewedByUserId</c>,
        /// nên đọc danh sách SAU khi sửa thì chính hai người cần biết nhất lại là hai người không được báo.
        /// </summary>
        public static List<Guid> RevokeRecipients(RecruitmentRequest req, Guid? actorId)
        {
            var recipients = new List<Guid>();
            if (req.AssignedRecruiterId is { } rec) recipients.Add(rec);
            if (req.ReviewedByUserId is { } reviewer) recipients.Add(reviewer);
            recipients.Add(req.RequestedByUserId);

            // Người tự bấm nút thì không tự báo cho mình.
            return recipients.Distinct().Where(u => u != actorId).ToList();
        }

        /// <summary>
        /// Gửi báo thu hồi cho danh sách đã chốt từ <see cref="RevokeRecipients"/>.
        ///
        /// Recruiter là người thiệt nhất nếu im lặng — họ có thể đang soạn JD cho một nhu cầu vừa bị huỷ.
        /// Trigger realtime (ADR-057) không cứu được chỗ này: payload mang <c>assigned_recruiter_id</c> của
        /// hàng MỚI, mà mở lại vừa xoá đúng cột đó.
        /// </summary>
        public static async Task NotifyRevokedAsync(
            IUnitOfWork uow, INotificationService notifications, RecruitmentRequest req,
            List<Guid> recipients, string title, string body, string eventType, CancellationToken ct)
        {
            foreach (var userId in recipients)
            {
                await uow.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = userId,
                    Type = "warning",
                    Title = title,
                    Body = body,
                    Link = $"/hr/recruitment-requests/{req.Id}",
                    // Gắn theo vòng gửi: thu hồi rồi duyệt lại rồi thu hồi nữa là hai sự kiện khác nhau,
                    // gộp chúng làm lần thứ hai biến mất.
                    DedupKey = $"recruitment_request_revoked:{req.Id}:{req.SubmissionCount}",
                    IsRead = false,
                }, ct);
            }

            await uow.SaveChangesAsync(ct);

            foreach (var userId in recipients)
                await notifications.PublishUserEventAsync(userId, "ReceiveUserNotification",
                    new { Type = eventType, RequestId = req.Id }, ct);
        }

        /// <summary>
        /// Kiểm phần nội dung HM nhập. Dùng chung cho tạo mới và sửa để hai đường không trôi khỏi nhau
        /// — sửa được thành phiếu không hợp lệ thì việc kiểm lúc tạo trở nên vô nghĩa.
        /// </summary>
        public static Result ValidateInput(RecruitmentRequestInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Title))
                return Result.Failure("Vị trí cần tuyển là bắt buộc.");

            if (input.Headcount < 1)
                return Result.Failure("Số lượng cần tuyển phải từ 1 trở lên.");

            if (!RecruitmentPriority.IsValid(input.Priority))
                return Result.Failure("Mức độ ưu tiên phải là Cao, Trung bình hoặc Thấp.");

            if (input.ExpectedStartDate is null)
                return Result.Failure("Ngày dự kiến bắt đầu là bắt buộc.");

            if (string.IsNullOrWhiteSpace(input.Reason))
                return Result.Failure("Lý do tuyển là bắt buộc.");

            if (string.IsNullOrWhiteSpace(input.Description))
                return Result.Failure("Mô tả sơ bộ công việc là bắt buộc.");

            if (string.IsNullOrWhiteSpace(input.Requirements))
                return Result.Failure("Yêu cầu ứng viên là bắt buộc.");

            if (input.SalaryMin.HasValue && input.SalaryMin < 0)
                return Result.Failure("Mức lương không được âm.");

            if (input.SalaryMin.HasValue && input.SalaryMax.HasValue && input.SalaryMin > input.SalaryMax)
                return Result.Failure("Lương tối thiểu không được lớn hơn lương tối đa.");

            // "Thoả thuận" SUY RA từ dữ liệu (cả hai ô lương trống), không có cột riêng — cột `bool`
            // sẽ biểu diễn được trạng thái mâu thuẫn "tích thoả thuận nhưng vẫn có số", mà trạng thái
            // nào biểu diễn được thì sẽ có lúc xảy ra.
            //
            // Đổi lại PHẢI chặn ở đây: không có luật này thì "thoả thuận" và "quên điền" lại lẫn vào
            // nhau — đúng thứ ô tích kia sinh ra để phân biệt.
            if (!input.SalaryNegotiable && input.SalaryMin is null && input.SalaryMax is null)
                return Result.Failure(
                    "Hãy điền dải lương đề xuất, hoặc tích \"Thoả thuận\" nếu chưa chốt được con số.");

            return Result.Success();
        }

        public static void Apply(RecruitmentRequest entity, RecruitmentRequestInput input)
        {
            entity.Title = input.Title.Trim();
            entity.Headcount = input.Headcount;
            entity.Priority = RecruitmentPriority.Normalize(input.Priority);

            // CỐ Ý không gán `DepartmentId` ở đây: đội của phiếu do handler quyết định (Hiring
            // Manager thì lấy từ tài khoản, quản trị viên lập hộ thì chọn). Để `Apply` chép thẳng
            // từ client là mở lại đúng lỗ hổng ADR-065 vừa bịt.

            entity.Reason = Trim(input.Reason);
            entity.Description = Trim(input.Description);
            entity.Requirements = Trim(input.Requirements);
            entity.EmploymentType = Trim(input.EmploymentType);
            entity.WorkMode = Trim(input.WorkMode);
            entity.Location = Trim(input.Location);
            entity.ExperienceLevel = Trim(input.ExperienceLevel);
            entity.ExpectedStartDate = input.ExpectedStartDate;
            // Tích thoả thuận thì XOÁ TRẮNG hai ô, không giữ lại con số cũ: giữ lại là tạo đúng
            // trạng thái mâu thuẫn mà cách suy ra này sinh ra để loại bỏ.
            entity.SalaryMin = input.SalaryNegotiable ? null : input.SalaryMin;
            entity.SalaryMax = input.SalaryNegotiable ? null : input.SalaryMax;
            entity.SalaryCurrency = Trim(input.SalaryCurrency) ?? "VND";
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Chỉ <b>Hiring Manager</b> lập được phiếu.
        ///
        /// Quản trị viên (HR Leader / Super Admin) cố ý <b>không</b> lập hộ, và lý do nằm ngoài chuyện
        /// phân quyền: <c>CreateJobCommand</c> gán <b>người lập phiếu làm Hiring Manager của tin</b> (ADR-063).
        /// HR Leader lập phiếu nghĩa là chính họ thành HM của tin — lúc đó một người vừa giữ cổng chuyên môn
        /// (duyệt shortlist, chốt Pass/Not Pass) vừa giữ cổng ngân sách (chốt thư mời), đúng hai vai mà
        /// ADR-061 và ADR-063 dựng lên để tách nhau. Chặn ở đây rẻ hơn đi vá ở bốn cổng phía sau.
        ///
        /// Đội luôn lấy từ chính tài khoản người lập, <b>bỏ qua</b> giá trị client gửi lên: HM của đội A
        /// không thể lập phiếu ghi đội B, kể cả bằng một request tự dựng (ADR-065).
        /// </summary>
        public static async Task<(Guid? DepartmentId, string? Error)> ResolveDepartmentAsync(
            IUnitOfWork uow, Guid actorId, string? actorRole, CancellationToken ct)
        {
            // Viết tường minh trong handler chứ không chỉ dựa vào policy: nới policy một dòng là quyền cũ
            // sống lại mà không test nào đổ — cùng lý lẽ với `OfferApproval` ở ADR-063.
            if (!RoleNames.Is(actorRole, RoleNames.HiringManager))
                return (null,
                    "Chỉ Hiring Manager lập được phiếu yêu cầu tuyển dụng — người lập phiếu sẽ thành " +
                    "Hiring Manager của tin sinh ra từ phiếu đó.");

            var actor = await uow.Repository<User>().GetByIdAsync(actorId, ct);
            if (actor?.DepartmentId is not { } own)
                return (null,
                    "Tài khoản của bạn chưa được gán đội/bộ phận nên chưa lập được phiếu. " +
                    "Hãy nhờ Super Admin gán đội ở màn Quản lý người dùng.");

            return (own, null);
        }

        /// <summary>Lấy phiếu còn hiệu lực (chưa xoá mềm).</summary>
        public static async Task<RecruitmentRequest?> GetAsync(IUnitOfWork uow, Guid id, CancellationToken ct)
        {
            var found = await uow.Repository<RecruitmentRequest>().FindAsync(r => r.Id == id && r.DeletedAt == null, ct);
            return found.FirstOrDefault();
        }

        /// <summary>Báo cho toàn bộ HR Leader đang hoạt động — hàng chờ duyệt là việc của cả nhóm.</summary>
        public static async Task NotifyHrLeadersAsync(
            IUnitOfWork uow, RecruitmentRequest req, string title, string body, string dedupPrefix, CancellationToken ct)
        {
            var leaders = await uow.Repository<User>().QueryAsync(
                q => q.Where(u => u.IsActive && u.DeletedAt == null
                                  && (u.Role == RoleNames.HrAdmin || u.Role == RoleNames.SuperAdmin))
                      .Select(u => u.Id), ct);

            foreach (var leaderId in leaders)
            {
                await uow.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = leaderId,
                    Type = "info",
                    Title = title,
                    Body = body,
                    Link = $"/hr/recruitment-requests/{req.Id}",
                    // Gộp theo LẦN GỬI: phiếu đi mấy vòng thì có bấy nhiêu thông báo, nhưng một
                    // lần gửi không bao giờ đẻ hai dòng (idempotent — bài học OfferFeature).
                    DedupKey = $"{dedupPrefix}:{req.Id}:{req.SubmissionCount}",
                    IsRead = false,
                }, ct);
            }
        }
    }

    // ---------------------------------------------------------------------------------
    //  Tạo phiếu — Hiring Manager
    // ---------------------------------------------------------------------------------

    public record CreateRecruitmentRequestCommand(RecruitmentRequestInput Input, Guid? ActorId, string? ActorRole)
        : IRequest<Result<Guid>>;

    public class CreateRecruitmentRequestCommandHandler
        : IRequestHandler<CreateRecruitmentRequestCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public CreateRecruitmentRequestCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<Guid>> Handle(CreateRecruitmentRequestCommand request, CancellationToken ct)
        {
            if (request.ActorId is not { } actorId)
                return Result.Failure<Guid>("Không xác định được người lập phiếu.", CommonErrorCodes.Forbidden);

            var validation = RecruitmentRequestSupport.ValidateInput(request.Input);
            if (validation.IsFailure)
                return Result.Failure<Guid>(validation.Error);

            var (departmentId, deptError) = await RecruitmentRequestSupport.ResolveDepartmentAsync(
                _unitOfWork, actorId, request.ActorRole, ct);
            if (deptError != null) return Result.Failure<Guid>(deptError);

            var entity = new RecruitmentRequest
            {
                RequestedByUserId = actorId,
                DepartmentId = departmentId,
                Status = RecruitmentRequestStatus.Pending,
                SubmissionCount = 1,
            };
            RecruitmentRequestSupport.Apply(entity, request.Input);

            await _unitOfWork.Repository<RecruitmentRequest>().AddAsync(entity, ct);

            await AdminSupport.WriteAuditAsync(_unitOfWork, actorId, "recruitment_request_created",
                nameof(RecruitmentRequest), entity.Id,
                AuditMetadata.Serialize(new { entity.Title, entity.Headcount, entity.SalaryMin, entity.SalaryMax }), ct);

            await RecruitmentRequestSupport.NotifyHrLeadersAsync(_unitOfWork, entity,
                "Phiếu yêu cầu tuyển dụng mới",
                $"\"{entity.Title}\" — {entity.Headcount} người. Chờ bạn duyệt và phân công Recruiter.",
                "recruitment_request_pending", ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishGroupEventAsync(RoleNames.HrAdmin, "ReceiveUserNotification",
                new { Type = "RecruitmentRequestSubmitted", RequestId = entity.Id }, ct);

            return Result.Success(entity.Id);
        }
    }

    // ---------------------------------------------------------------------------------
    //  Sửa phiếu — chủ phiếu, khi còn sửa được
    // ---------------------------------------------------------------------------------

    public record UpdateRecruitmentRequestCommand(Guid Id, RecruitmentRequestInput Input, Guid? ActorId, string? ActorRole)
        : IRequest<Result>;

    public class UpdateRecruitmentRequestCommandHandler
        : IRequestHandler<UpdateRecruitmentRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateRecruitmentRequestCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result> Handle(UpdateRecruitmentRequestCommand request, CancellationToken ct)
        {
            var req = await RecruitmentRequestSupport.GetAsync(_unitOfWork, request.Id, ct);
            if (req == null)
                return Result.Failure("Không tìm thấy phiếu yêu cầu tuyển dụng.", CommonErrorCodes.NotFound);

            // Chỉ CHỦ phiếu sửa được. Admin cố ý không sửa hộ: nội dung phiếu là lời của HM, sửa hộ
            // rồi tự duyệt là đi vòng qua đúng cổng vừa dựng lên.
            if (req.RequestedByUserId != request.ActorId)
                return Result.Failure("Chỉ người lập phiếu mới sửa được nội dung.", CommonErrorCodes.Forbidden);

            if (!RecruitmentRequestStatus.IsEditable(req.Status))
                return Result.Failure("Phiếu đã duyệt hoặc đã rút thì không sửa được nữa.");

            var validation = RecruitmentRequestSupport.ValidateInput(request.Input);
            if (validation.IsFailure)
                return validation;

            // Phiếu lập TRƯỚC ADR-065 mất liên kết đội khi cột `department` dạng chuỗi bị bỏ (migration
            // cố ý không đoán để backfill). Không có đường nào khác lấy lại được: ô đội trên biểu mẫu chỉ
            // đọc, còn `Apply` không chạm tới cột này — nên những phiếu đó sẽ hiện "—" vĩnh viễn.
            //
            // Điền vào CHỖ TRỐNG từ tài khoản **chủ phiếu** — cùng nguồn mà `ResolveDepartmentAsync` dùng cho
            // phiếu mới, nên không phải một phép đoán riêng. **Không bao giờ ghi đè**: đổi đội của một phiếu
            // đã có đội vẫn là điều ADR-065 cấm — lập phiếu đúng đội rồi sửa sang đội khác.
            if (req.DepartmentId is null)
            {
                var owner = await _unitOfWork.Repository<User>().GetByIdAsync(req.RequestedByUserId, ct);
                req.DepartmentId = owner?.DepartmentId;
            }

            RecruitmentRequestSupport.Apply(req, request.Input);
            _unitOfWork.Repository<RecruitmentRequest>().Update(req);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
    }

    // ---------------------------------------------------------------------------------
    //  Gửi lại sau khi bị từ chối — chủ phiếu
    // ---------------------------------------------------------------------------------

    public record ResubmitRecruitmentRequestCommand(Guid Id, Guid? ActorId) : IRequest<Result>;

    public class ResubmitRecruitmentRequestCommandHandler
        : IRequestHandler<ResubmitRecruitmentRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public ResubmitRecruitmentRequestCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result> Handle(ResubmitRecruitmentRequestCommand request, CancellationToken ct)
        {
            var req = await RecruitmentRequestSupport.GetAsync(_unitOfWork, request.Id, ct);
            if (req == null)
                return Result.Failure("Không tìm thấy phiếu yêu cầu tuyển dụng.", CommonErrorCodes.NotFound);

            if (req.RequestedByUserId != request.ActorId)
                return Result.Failure("Chỉ người lập phiếu mới gửi lại được.", CommonErrorCodes.Forbidden);

            if (!RecruitmentRequestStatus.Is(req.Status, RecruitmentRequestStatus.Rejected))
                return Result.Failure("Chỉ gửi lại được phiếu đang bị trả về.");

            req.Status = RecruitmentRequestStatus.Pending;
            req.SubmissionCount += 1;
            // Giữ nguyên ReviewReason: lý do bị trả về lần trước là bối cảnh HR Leader cần khi xem
            // lại vòng này — xoá đi thì mỗi vòng duyệt lại bắt đầu từ con số không.
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<RecruitmentRequest>().Update(req);

            await RecruitmentRequestSupport.NotifyHrLeadersAsync(_unitOfWork, req,
                "Phiếu yêu cầu tuyển dụng đã được gửi lại",
                $"\"{req.Title}\" — lần gửi thứ {req.SubmissionCount}.",
                "recruitment_request_pending", ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishGroupEventAsync(RoleNames.HrAdmin, "ReceiveUserNotification",
                new { Type = "RecruitmentRequestSubmitted", RequestId = req.Id }, ct);

            return Result.Success();
        }
    }

    // ---------------------------------------------------------------------------------
    //  Rút phiếu — chủ phiếu
    // ---------------------------------------------------------------------------------

    public record CancelRecruitmentRequestCommand(Guid Id, Guid? ActorId) : IRequest<Result>;

    public class CancelRecruitmentRequestCommandHandler
        : IRequestHandler<CancelRecruitmentRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CancelRecruitmentRequestCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Result> Handle(CancelRecruitmentRequestCommand request, CancellationToken ct)
        {
            var req = await RecruitmentRequestSupport.GetAsync(_unitOfWork, request.Id, ct);
            if (req == null)
                return Result.Failure("Không tìm thấy phiếu yêu cầu tuyển dụng.", CommonErrorCodes.NotFound);

            if (req.RequestedByUserId != request.ActorId)
                return Result.Failure("Chỉ người lập phiếu mới rút được phiếu.", CommonErrorCodes.Forbidden);

            // Phiếu ĐÃ DUYỆT không rút bằng đường này: nó không đòi lý do và không báo cho ai, trong khi
            // lúc đó đã có một Recruiter đang cầm việc và một chữ ký ngân sách cần huỷ hiệu lực (ADR-066).
            if (!RecruitmentRequestStatus.IsEditable(req.Status))
                return Result.Failure(RecruitmentRequestStatus.IsRevocable(req.Status)
                    ? "Phiếu đã duyệt thì dùng Đóng phiếu (kèm lý do) thay vì Rút phiếu."
                    : "Phiếu đã đóng thì không rút lại được.");

            req.Status = RecruitmentRequestStatus.Cancelled;
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<RecruitmentRequest>().Update(req);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "recruitment_request_cancelled",
                nameof(RecruitmentRequest), req.Id, AuditMetadata.Serialize(new { req.Title }), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    // ---------------------------------------------------------------------------------
    //  Duyệt phiếu + phân công Recruiter — HR Leader
    // ---------------------------------------------------------------------------------

    public record ApproveRecruitmentRequestCommand(
        Guid Id, Guid AssignedRecruiterId, string? Note, Guid? ActorId) : IRequest<Result>;

    public class ApproveRecruitmentRequestCommandHandler
        : IRequestHandler<ApproveRecruitmentRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public ApproveRecruitmentRequestCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result> Handle(ApproveRecruitmentRequestCommand request, CancellationToken ct)
        {
            var req = await RecruitmentRequestSupport.GetAsync(_unitOfWork, request.Id, ct);
            if (req == null)
                return Result.Failure("Không tìm thấy phiếu yêu cầu tuyển dụng.", CommonErrorCodes.NotFound);

            // RÀNG BUỘC 1 — không tự duyệt phiếu của mình. Chặn theo NGƯỜI nên một HR Leader tự lập
            // phiếu cho đội mình cũng không lách được, dù policy cho phép vai trò đó duyệt.
            if (req.RequestedByUserId == request.ActorId)
                return Result.Failure(
                    "Không thể tự duyệt phiếu do chính mình lập. Nhờ một HR Leader khác duyệt.",
                    CommonErrorCodes.Forbidden);

            if (!RecruitmentRequestStatus.Is(req.Status, RecruitmentRequestStatus.Pending))
                return Result.Failure("Chỉ duyệt được phiếu đang chờ duyệt.");

            // Phân công là PHẦN CỦA thao tác duyệt, không phải bước rời. Duyệt xong mà không có ai
            // phụ trách thì phiếu nằm im — đúng lỗi ADR-059 đã chữa cho "duyệt CV mà chưa gán ca".
            var recruiters = await _unitOfWork.Repository<User>().FindAsync(
                u => u.Id == request.AssignedRecruiterId && u.DeletedAt == null, ct);
            var recruiter = recruiters.FirstOrDefault();

            if (recruiter == null)
                return Result.Failure("Không tìm thấy Recruiter được chọn.", CommonErrorCodes.NotFound);

            if (!recruiter.IsActive)
                return Result.Failure("Tài khoản Recruiter được chọn đang bị khoá.");

            if (!RoleNames.Is(recruiter.Role, RoleNames.Recruiter))
                return Result.Failure("Người được phân công phải có vai trò Recruiter.");

            req.Status = RecruitmentRequestStatus.Approved;
            req.AssignedRecruiterId = recruiter.Id;
            req.ReviewedByUserId = request.ActorId;
            req.ReviewedAt = DateTimeOffset.UtcNow;
            req.ReviewReason = RecruitmentRequestSupport.Trim(request.Note);
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<RecruitmentRequest>().Update(req);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "recruitment_request_approved",
                nameof(RecruitmentRequest), req.Id,
                AuditMetadata.Serialize(new { req.Title, assignedRecruiterId = recruiter.Id, note = req.ReviewReason }), ct);

            // Hai người cần biết: HM (phiếu được duyệt) và Recruiter (có việc mới).
            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = req.RequestedByUserId,
                Type = "approved",
                Title = "Phiếu yêu cầu tuyển dụng đã được duyệt",
                Body = $"\"{req.Title}\" đã được duyệt. {recruiter.FullName} sẽ dựng bản mô tả công việc.",
                Link = $"/hm/recruitment-requests/{req.Id}",
                DedupKey = $"recruitment_request_approved:{req.Id}:{req.SubmissionCount}",
                IsRead = false,
            }, ct);

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = recruiter.Id,
                Type = "info",
                Title = "Bạn được phân công một yêu cầu tuyển dụng",
                Body = $"\"{req.Title}\" — {req.Headcount} người. Hãy dựng bản mô tả công việc.",
                Link = $"/recruiter/recruitment-requests/{req.Id}",
                DedupKey = $"recruitment_request_assigned:{req.Id}:{req.SubmissionCount}",
                IsRead = false,
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(req.RequestedByUserId, "ReceiveUserNotification",
                new { Type = "RecruitmentRequestApproved", RequestId = req.Id }, ct);
            await _notifications.PublishUserEventAsync(recruiter.Id, "ReceiveUserNotification",
                new { Type = "RecruitmentRequestAssigned", RequestId = req.Id }, ct);

            return Result.Success();
        }
    }

    // ---------------------------------------------------------------------------------
    //  Từ chối phiếu, trả về cho HM sửa — HR Leader
    // ---------------------------------------------------------------------------------

    public record RejectRecruitmentRequestCommand(Guid Id, string? Reason, Guid? ActorId) : IRequest<Result>;

    public class RejectRecruitmentRequestCommandHandler
        : IRequestHandler<RejectRecruitmentRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public RejectRecruitmentRequestCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result> Handle(RejectRecruitmentRequestCommand request, CancellationToken ct)
        {
            var req = await RecruitmentRequestSupport.GetAsync(_unitOfWork, request.Id, ct);
            if (req == null)
                return Result.Failure("Không tìm thấy phiếu yêu cầu tuyển dụng.", CommonErrorCodes.NotFound);

            if (req.RequestedByUserId == request.ActorId)
                return Result.Failure(
                    "Không thể tự xử lý phiếu do chính mình lập.", CommonErrorCodes.Forbidden);

            if (!RecruitmentRequestStatus.Is(req.Status, RecruitmentRequestStatus.Pending))
                return Result.Failure("Chỉ từ chối được phiếu đang chờ duyệt.");

            // RÀNG BUỘC 2 — trả phiếu về mà không nói vì sao thì HM sửa theo phỏng đoán.
            var reason = RecruitmentRequestSupport.Trim(request.Reason);
            if (reason == null || reason.Length < RecruitmentRequestSupport.MinReasonLength)
                return Result.Failure(
                    $"Vui lòng nêu rõ lý do trả lại phiếu (tối thiểu {RecruitmentRequestSupport.MinReasonLength} ký tự) " +
                    "— thường là dải lương cần điều chỉnh.");

            req.Status = RecruitmentRequestStatus.Rejected;
            req.ReviewReason = reason;
            req.ReviewedByUserId = request.ActorId;
            req.ReviewedAt = DateTimeOffset.UtcNow;
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<RecruitmentRequest>().Update(req);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "recruitment_request_rejected",
                nameof(RecruitmentRequest), req.Id, AuditMetadata.Serialize(new { req.Title, reason }), ct);

            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                RecipientUserId = req.RequestedByUserId,
                Type = "rejected",
                Title = "Phiếu yêu cầu tuyển dụng cần chỉnh sửa",
                Body = $"\"{req.Title}\" bị trả lại. Lý do: {reason}",
                Link = $"/hm/recruitment-requests/{req.Id}",
                DedupKey = $"recruitment_request_rejected:{req.Id}:{req.SubmissionCount}",
                IsRead = false,
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishUserEventAsync(req.RequestedByUserId, "ReceiveUserNotification",
                new { Type = "RecruitmentRequestRejected", RequestId = req.Id }, ct);

            return Result.Success();
        }
    }

    // ---------------------------------------------------------------------------------
    //  Thu hồi phê duyệt (ADR-066) — HM chủ phiếu hoặc quản trị viên
    //
    //  Hai lệnh, một bản chất: huỷ hiệu lực chữ ký của HR Leader khi nhu cầu tuyển đã đổi. Chúng
    //  dùng chung toàn bộ phần kiểm ở `LoadRevocableAsync` và chỉ khác đích đến.
    // ---------------------------------------------------------------------------------

    /// <summary>Mở lại phiếu đã duyệt để sửa — quay về <c>pending</c>, chờ HR Leader duyệt lại.</summary>
    public record ReopenRecruitmentRequestCommand(Guid Id, string? Reason, Guid? ActorId, string? ActorRole)
        : IRequest<Result>;

    public class ReopenRecruitmentRequestCommandHandler
        : IRequestHandler<ReopenRecruitmentRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public ReopenRecruitmentRequestCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result> Handle(ReopenRecruitmentRequestCommand request, CancellationToken ct)
        {
            var (req, reason, error, code) = await RecruitmentRequestSupport.LoadRevocableAsync(
                _unitOfWork, request.Id, request.Reason, request.ActorId, request.ActorRole, ct);

            if (req == null) return code == null ? Result.Failure(error!) : Result.Failure(error!, code);

            // Chốt danh sách người nhận TRƯỚC khi gỡ phân công — xem `RevokeRecipients`.
            var formerRecruiter = req.AssignedRecruiterId;
            var recipients = RecruitmentRequestSupport.RevokeRecipients(req, request.ActorId);

            req.Status = RecruitmentRequestStatus.Pending;
            req.RevokedReason = reason;
            req.RevokedByUserId = request.ActorId;

            // Phân công đi theo chữ ký. ADR-063 chốt "duyệt kèm phân công trong CÙNG một thao tác"; để lại
            // Recruiter trên một phiếu đang chờ duyệt là tự tạo ra ngoại lệ cho chính bất biến đó, và tệ hơn là
            // người đó vẫn thấy phiếu trong danh sách việc của mình.
            req.AssignedRecruiterId = null;
            req.ReviewedByUserId = null;
            req.ReviewedAt = null;
            req.ReviewReason = null;

            // Một vòng sửa–gửi lại mới, đúng như khi bị trả lại — để HR Leader thấy phiếu đã đi mấy vòng.
            req.SubmissionCount += 1;
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<RecruitmentRequest>().Update(req);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "recruitment_request_reopened",
                nameof(RecruitmentRequest), req.Id,
                AuditMetadata.Serialize(new { req.Title, reason, formerRecruiterId = formerRecruiter }), ct);

            await RecruitmentRequestSupport.NotifyRevokedAsync(
                _unitOfWork, _notifications, req, recipients,
                "Phiếu yêu cầu tuyển dụng được mở lại để sửa",
                $"\"{req.Title}\" đã thu hồi phê duyệt và quay lại chờ duyệt. Lý do: {reason}",
                "RecruitmentRequestReopened", ct);

            return Result.Success();
        }
    }

    /// <summary>Đóng phiếu đã duyệt vì hết nhu cầu — sang <c>cancelled</c>, không dựng tin được nữa.</summary>
    public record CloseRecruitmentRequestCommand(Guid Id, string? Reason, Guid? ActorId, string? ActorRole)
        : IRequest<Result>;

    public class CloseRecruitmentRequestCommandHandler
        : IRequestHandler<CloseRecruitmentRequestCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public CloseRecruitmentRequestCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result> Handle(CloseRecruitmentRequestCommand request, CancellationToken ct)
        {
            var (req, reason, error, code) = await RecruitmentRequestSupport.LoadRevocableAsync(
                _unitOfWork, request.Id, request.Reason, request.ActorId, request.ActorRole, ct);

            if (req == null) return code == null ? Result.Failure(error!) : Result.Failure(error!, code);

            var recipients = RecruitmentRequestSupport.RevokeRecipients(req, request.ActorId);

            req.Status = RecruitmentRequestStatus.Cancelled;
            req.RevokedReason = reason;
            req.RevokedByUserId = request.ActorId;

            // Giữ nguyên `AssignedRecruiterId` và `ReviewedByUserId`: phiếu đóng là hồ sơ lịch sử, xóa dấu
            // vết "ai từng duyệt, ai từng được giao" là làm hỏng chính thứ cần tra lại sau này.
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<RecruitmentRequest>().Update(req);

            await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId, "recruitment_request_closed",
                nameof(RecruitmentRequest), req.Id, AuditMetadata.Serialize(new { req.Title, reason }), ct);

            await RecruitmentRequestSupport.NotifyRevokedAsync(
                _unitOfWork, _notifications, req, recipients,
                "Phiếu yêu cầu tuyển dụng đã đóng",
                $"\"{req.Title}\" đã đóng, không dựng tin nữa. Lý do: {reason}",
                "RecruitmentRequestClosed", ct);

            return Result.Success();
        }
    }
}
