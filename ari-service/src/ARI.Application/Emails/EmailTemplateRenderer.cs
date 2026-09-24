using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Application.Scheduling;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Emails
{
    /// <summary>
    /// Dựng nội dung thư cho trình soạn thảo (ADR-061, Phase 4).
    ///
    /// NGUYÊN TẮC: mọi giá trị được thay THẬT ngay tại bước xem trước — không để lại
    /// <c>{{tênỨngViên}}</c> nào trong nội dung người dùng sửa. Nếu còn placeholder thì sau khi
    /// nhân sự sửa tay, hệ thống phải chạy templating trên chuỗi do người dùng nhập, mở ra cả một
    /// lớp lỗi mới (gõ sai tên biến, hoặc cố tình chèn biến để moi dữ liệu). What-you-see-is-what-is-sent.
    /// </summary>
    public interface IEmailTemplateRenderer
    {
        /// <param name="variant">Tham số phụ dạng chuỗi theo mẫu — thư kết quả phỏng vấn dùng để truyền verdict sắp chốt.</param>
        Task<Result<RenderedEmail>> RenderAsync(
            string templateKey, Guid contextId, Guid? secondaryId, string? variant, CancellationToken ct);
    }

    public class EmailTemplateRenderer : IEmailTemplateRenderer
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public EmailTemplateRenderer(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<Result<RenderedEmail>> RenderAsync(
            string templateKey, Guid contextId, Guid? secondaryId, string? variant, CancellationToken ct)
        {
            var key = (templateKey ?? string.Empty).Trim().ToLowerInvariant();

            return key switch
            {
                EmailTemplateKeys.InterviewInvite => await RenderInterviewInviteAsync(contextId, secondaryId, ct),
                EmailTemplateKeys.ApplicationRejected => await RenderApplicationRejectedAsync(contextId, ct),
                EmailTemplateKeys.OfferSent => await RenderOfferAsync(contextId, ct),
                EmailTemplateKeys.InterviewResult => await RenderInterviewResultAsync(contextId, secondaryId, variant, ct),
                EmailTemplateKeys.ScheduleReminder => await RenderScheduleReminderAsync(contextId, secondaryId, ct),
                _ => Result.Failure<RenderedEmail>($"Không biết mẫu thư '{templateKey}'.", CommonErrorCodes.NotFound),
            };
        }

        /// <summary>
        /// Thư mời phỏng vấn: dựng bằng CHÍNH builder mà lệnh gán lịch dùng
        /// (<see cref="InterviewInviteEmail.BuildAsync"/>), nên bản xem trước không thể lệch khỏi
        /// bản thật. Lúc xem trước chưa có booking → truyền <c>bookingId</c> rỗng; hai nút
        /// Xác nhận/Đổi lịch trong bản xem trước vì thế chưa trỏ tới đâu, còn bản gửi thật thì có.
        /// </summary>
        private async Task<Result<RenderedEmail>> RenderInterviewInviteAsync(
            Guid applicationId, Guid? slotId, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(applicationId, ct);
            if (app == null)
                return Result.Failure<RenderedEmail>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

            AvailabilitySlot? slot = null;
            if (slotId is { } sid && sid != Guid.Empty)
                slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(sid, ct);

            if (slot == null)
                return Result.Failure<RenderedEmail>(
                    "Hãy chọn khung giờ trước khi xem trước thư mời — nội dung thư có giờ hẹn.");

            var mail = await InterviewInviteEmail.BuildAsync(
                _unitOfWork, _configuration, app, job, slot.RoundNumber, Guid.Empty, slot.StartTime, ct);

            return Result.Success(new RenderedEmail(mail.Subject, mail.Html, app.CandidateEmail, app.CandidateName));
        }

        /// <summary>
        /// Thư mời nhận việc — dựng từ offer ĐANG HIỆU LỰC của hồ sơ (bản nháp hoặc bản đã duyệt),
        /// bằng chính builder mà lệnh gửi dùng.
        /// </summary>
        private async Task<Result<RenderedEmail>> RenderOfferAsync(Guid applicationId, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(applicationId, ct);
            if (app == null)
                return Result.Failure<RenderedEmail>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);

            var offer = (await _unitOfWork.Repository<Offer>().FindAsync(o => o.ApplicationId == app.Id, ct))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault(o => !ARI.Domain.Constants.OfferStatus.IsClosed(o.Status));
            if (offer == null)
                return Result.Failure<RenderedEmail>("Hồ sơ này chưa có thư mời nhận việc.", CommonErrorCodes.NotFound);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            var mail = ARI.Application.Offers.OfferEmail.Build(
                offer, app, job, FrontendUrls.Candidate(_configuration));

            return Result.Success(new RenderedEmail(mail.Subject, mail.Html, app.CandidateEmail, app.CandidateName));
        }

        /// <summary>
        /// Thư nhắc lịch — biến thể do <see cref="ScheduleReminder.Resolve"/> quyết định, đúng hàm mà
        /// lệnh gửi và giao diện dùng. Lịch đã đóng hoặc đã qua giờ thì KHÔNG dựng bản xem trước: nút
        /// ở giao diện đã tắt, tới được đây nghĩa là dữ liệu vừa đổi dưới chân người dùng.
        /// </summary>
        private async Task<Result<RenderedEmail>> RenderScheduleReminderAsync(
            Guid applicationId, Guid? bookingId, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(applicationId, ct);
            if (app == null)
                return Result.Failure<RenderedEmail>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);

            if (bookingId is not { } bid || bid == Guid.Empty)
                return Result.Failure<RenderedEmail>("Thiếu lịch hẹn để dựng thư nhắc.", CommonErrorCodes.NotFound);

            var booking = await _unitOfWork.Repository<InterviewBooking>().GetByIdAsync(bid, ct);
            if (booking == null || booking.ApplicationId != app.Id)
                return Result.Failure<RenderedEmail>("Không tìm thấy lịch phỏng vấn.", CommonErrorCodes.NotFound);

            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(booking.AvailabilitySlotId, ct);
            if (slot == null)
                return Result.Failure<RenderedEmail>("Không tìm thấy ca phỏng vấn.", CommonErrorCodes.NotFound);

            var state = ScheduleReminder.Resolve(
                booking.Status, booking.ConfirmationStatus, slot.StartTime, DateTimeOffset.UtcNow);
            if (!ScheduleReminder.CanSend(state))
                return Result.Failure<RenderedEmail>(ScheduleReminder.BlockedMessage(state), CommonErrorCodes.Conflict);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

            var mail = state == ScheduleReminder.States.ConfirmNeeded
                ? await InterviewInviteEmail.BuildReminderAsync(
                    _unitOfWork, _configuration, app, job, booking.RoundNumber, booking.Id, slot.StartTime, ct)
                : await InterviewInviteEmail.BuildTimeReminderAsync(
                    _unitOfWork, _configuration, app, job, booking.RoundNumber, slot.StartTime, ct);

            return Result.Success(new RenderedEmail(mail.Subject, mail.Html, app.CandidateEmail, app.CandidateName));
        }

        /// <summary>
        /// Thư kết quả vòng phỏng vấn — cùng builder và cùng hàm suy biến thể với lệnh chốt, nên thứ HM
        /// sửa trong trình soạn đúng là thứ ứng viên nhận (ADR-074).
        /// </summary>
        private async Task<Result<RenderedEmail>> RenderInterviewResultAsync(
            Guid applicationId, Guid? evaluationId, string? verdict, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(applicationId, ct);
            if (app == null)
                return Result.Failure<RenderedEmail>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);

            // Báo cáo phải THUỘC hồ sơ đang xem — quyền đã kiểm trên hồ sơ, không phải trên báo cáo.
            var evaluation = evaluationId is { } eid && eid != Guid.Empty
                ? await _unitOfWork.Repository<Evaluation>().GetByIdAsync(eid, ct)
                : null;
            if (evaluation == null || evaluation.ApplicationId != app.Id)
                return Result.Failure<RenderedEmail>("Không tìm thấy báo cáo đánh giá của hồ sơ này.", CommonErrorCodes.NotFound);

            if (verdict is not ("pass" or "not_pass"))
                return Result.Failure<RenderedEmail>("Chọn kết quả Đạt/Không đạt trước khi xem thư.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            var totalRounds = await InterviewResultEmail.TotalRoundsAsync(_unitOfWork, app.JobPostingId, ct);
            var mail = InterviewResultEmail.Build(
                app, job, InterviewResultEmail.ResolveVariant(verdict, evaluation.RoundNumber, totalRounds),
                evaluation.RoundNumber, FrontendUrls.Candidate(_configuration));

            return Result.Success(new RenderedEmail(mail.Subject, mail.Html, app.CandidateEmail, app.CandidateName));
        }

        private async Task<Result<RenderedEmail>> RenderApplicationRejectedAsync(Guid applicationId, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(applicationId, ct);
            if (app == null)
                return Result.Failure<RenderedEmail>(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);

            // Truyền ĐÚNG base URL mà lệnh gửi thật dùng (ApplicationService.PortalBaseUrl), nếu không
            // bản xem trước thiếu hẳn nút "Xem hồ sơ của bạn" so với thư thật — mà bản người dùng sửa
            // thì THAY nguyên thân thư, nên chỉ cần họ sửa một chữ là ứng viên nhận thư không có lối
            // quay lại Portal. Bản xem trước lệch với thư gửi đi là phá đúng giao ước của ADR-061.
            var portalBaseUrl = FrontendUrls.Candidate(_configuration);
            var mail = ApplicationRejectedEmail.Build(app, job, portalBaseUrl);
            return Result.Success(new RenderedEmail(mail.Subject, mail.Html, app.CandidateEmail, app.CandidateName));
        }
    }
}
