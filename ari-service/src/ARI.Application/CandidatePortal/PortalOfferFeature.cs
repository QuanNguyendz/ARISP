using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.Offers;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CandidatePortal
{
    /// <summary>
    /// Thư mời nhận việc ở Portal ứng viên (ADR-061, Phase 5).
    ///
    /// Hai bất biến của toàn bộ file này:
    ///   1. <b>Chỉ trả offer đã GỬI trở đi</b> (<see cref="OfferStatus.VisibleToCandidate"/>).
    ///      Bản nháp và bản đang duyệt là đàm phán nội bộ — lộ ra là ứng viên thấy mức lương công
    ///      ty còn đang cân nhắc, trước cả khi công ty quyết.
    ///   2. <b>Dùng <see cref="CandidateOfferDto"/></b>, không phải DTO của nhân sự — lớp đó không
    ///      có trường <c>Notes</c> để mà quên xoá.
    /// </summary>
    internal static class PortalOfferSupport
    {
        public static async Task<(Offer? offer, ARI.Domain.Entities.Application? app, JobPosting? job)>
            LoadOwnedAsync(
                IUnitOfWork uow, Guid applicationId, Guid candidateAccountId, string? emailClaim, CancellationToken ct)
        {
            var app = await uow.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (app == null) return (null, null, null);
            if (!await PortalSupport.TryEnsureOwnerAsync(app, candidateAccountId, emailClaim, uow))
                return (null, null, null);

            var offer = (await uow.Repository<Offer>().FindAsync(o => o.ApplicationId == app.Id, ct))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault(o => OfferStatus.IsVisibleToCandidate(o.Status));

            var job = await uow.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            return (offer, app, job);
        }
    }

    // ============================================================
    // GET /api/portal/applications/{id}/offer
    // ============================================================

    public record GetCandidateOfferQuery(Guid ApplicationId, Guid CandidateAccountId, string? EmailClaim)
        : IRequest<Result<CandidateOfferDto>>;

    public class GetCandidateOfferQueryHandler
        : IRequestHandler<GetCandidateOfferQuery, Result<CandidateOfferDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;

        public GetCandidateOfferQueryHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
        }

        public async Task<Result<CandidateOfferDto>> Handle(GetCandidateOfferQuery request, CancellationToken ct)
        {
            var (offer, _, job) = await PortalOfferSupport.LoadOwnedAsync(
                _unitOfWork, request.ApplicationId, request.CandidateAccountId, request.EmailClaim, ct);

            if (offer == null)
                return Result.Failure<CandidateOfferDto>(
                    "Chưa có thư mời nhận việc cho hồ sơ này.", CommonErrorCodes.NotFound);

            var dto = CandidateOfferDto.FromEntity(offer, job);
            if (!string.IsNullOrEmpty(dto.OfferLetterFileUrl))
                dto.OfferLetterFileUrl = await _fileStorage.GetUrlAsync(dto.OfferLetterFileUrl, ct);

            return Result.Success(dto);
        }
    }

    // ============================================================
    // POST /api/portal/offers/{id}/respond
    // ============================================================

    public record RespondToOfferCommand(
        Guid OfferId, string Decision, string? Note, Guid CandidateAccountId, string? EmailClaim)
        : IRequest<Result<bool>>;

    public class RespondToOfferCommandHandler : IRequestHandler<RespondToOfferCommand, Result<bool>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public RespondToOfferCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<bool>> Handle(RespondToOfferCommand request, CancellationToken ct)
        {
            var decision = (request.Decision ?? string.Empty).Trim().ToLowerInvariant();
            if (decision != "accept" && decision != "decline")
                return Result<bool>.Failure("Phản hồi phải là 'accept' hoặc 'decline'.");

            var offer = await _unitOfWork.Repository<Offer>().GetByIdAsync(request.OfferId, ct);
            if (offer == null)
                return Result<bool>.Failure("Không tìm thấy thư mời nhận việc.", CommonErrorCodes.NotFound);

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(offer.ApplicationId, ct);
            if (app == null)
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);

            // IDOR: chỉ chủ hồ sơ trả lời được thư mời của chính mình.
            if (!await PortalSupport.TryEnsureOwnerAsync(app, request.CandidateAccountId, request.EmailClaim, _unitOfWork))
                return Result<bool>.Failure("Bạn không có quyền phản hồi thư mời này.", CommonErrorCodes.Forbidden);

            if (!OfferStatus.Is(offer.Status, OfferStatus.Sent))
                return Result<bool>.Failure(
                    "Thư mời này không còn ở trạng thái chờ phản hồi.", CommonErrorCodes.Conflict);

            if (offer.ExpiresAt is { } exp && exp <= DateTimeOffset.UtcNow)
                return Result<bool>.Failure(
                    "Thư mời đã quá hạn phản hồi. Vui lòng liên hệ bộ phận nhân sự.", CommonErrorCodes.Conflict);

            var accepted = decision == "accept";
            offer.Status = accepted ? OfferStatus.Accepted : OfferStatus.Declined;
            offer.RespondedAt = DateTimeOffset.UtcNow;
            offer.CandidateResponseNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            offer.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<Offer>().Update(offer);

            // Điểm kết thúc của phễu — trước ADR-061 hệ thống không có trạng thái nào cho việc này.
            app.Status = accepted ? ApplicationStatuses.Hired : ApplicationStatuses.OfferDeclined;
            app.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(app);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

            // Báo cho người tạo thư mời + chủ tin.
            foreach (var recipient in new[] { offer.CreatedByUserId, job?.CreatedByUserId }
                         .Where(id => id is { } g && g != Guid.Empty).Select(id => id!.Value).Distinct())
            {
                await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = recipient,
                    Type = "result",
                    Title = accepted ? "Ứng viên đã nhận việc" : "Ứng viên từ chối thư mời",
                    Body = $"{app.CandidateName} — vị trí \"{job?.Title}\"."
                           + (offer.CandidateResponseNote != null ? $" Lý do: {offer.CandidateResponseNote}" : string.Empty),
                    Link = "/hr/offers",
                    DedupKey = $"offer_responded:{offer.Id}:{recipient}",
                    IsRead = false,
                }, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishGroupEventAsync("hr_admin", "ReceiveApplicationStatusUpdate",
                new { ApplicationId = app.Id, Status = app.Status }, ct);

            return Result.Success(true);
        }
    }
}
