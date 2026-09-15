using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Application.Scheduling;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Interviews
{
    /// <summary>
    /// Mã vào phòng phỏng vấn của MỘT hồ sơ — thứ Recruiter cấp ngay trên danh sách ứng viên của tin.
    ///
    /// Cùng một mã, hai cách dùng tuỳ ĐỊA ĐIỂM của vòng (<see cref="InterviewInviteEmail.IsRemoteRound"/>):
    /// <list type="bullet">
    /// <item><b>Chuyên môn — tại văn phòng.</b> Ứng viên check-in, Recruiter dẫn tới máy đã mở sẵn trang
    /// Kiosk rồi đưa mã. Mã KHÔNG hiện trong Portal (<see cref="InterviewCodeRules.ShownToCandidate"/>).</item>
    /// <item><b>Sơ loại — làm từ nhà.</b> Recruiter cấp mã rồi gửi link vào phòng (đã kèm mã) cho ứng
    /// viên; mã cũng hiện trong Portal kèm nút vào phòng.</item>
    /// </list>
    /// Nhập mã xong, cả hai đi chung một đường: phòng chờ → Hiring Manager cho vào (ADR-067).
    /// </summary>
    /// <param name="RoundNumber">Vòng của lịch đang giữ chỗ; <c>null</c> khi chưa có lịch nào.</param>
    /// <param name="CanIssue">Cấp (hoặc cấp lại) mã được không. <c>false</c> thì <paramref name="BlockedReason"/> nói vì sao.</param>
    /// <param name="KioskUrl">Trang Kiosk trống — mở sẵn trên máy tại văn phòng.</param>
    /// <param name="EntryUrl">Trang Kiosk đã điền sẵn mã — gửi cho ứng viên làm từ nhà. Chỉ có khi đang có mã.</param>
    public record ApplicationInterviewCodeDto(
        int? RoundNumber,
        string? RoundType,
        bool IsRemote,
        bool CanIssue,
        string? BlockedReason,
        string? Code,
        DateTimeOffset? ExpiresAt,
        string? KioskUrl,
        string? EntryUrl);

    internal static class ApplicationInterviewCodeSupport
    {
        public static string? KioskUrl(IConfiguration configuration)
        {
            var root = FrontendUrls.Candidate(configuration);
            return string.IsNullOrEmpty(root) ? null : $"{root}/kiosk";
        }

        /// <summary>
        /// Vòng CẦN mã = vòng của lịch đang giữ chỗ (ADR-058: dòng booking là sự thật). Không đoán từ
        /// phiên đã xong như đường cấp mã cũ (<c>max(completed) + 1</c>): hồ sơ được xếp lại lịch của
        /// chính vòng đó vẫn là vòng đó, và lịch là thứ nói ứng viên sắp dự vòng nào.
        /// </summary>
        public static async Task<InterviewBooking?> LiveBookingAsync(
            IUnitOfWork unitOfWork, Guid applicationId, CancellationToken ct)
            => (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => b.ApplicationId == applicationId && b.Status == BookingStatus.Scheduled, ct))
                .OrderByDescending(b => b.RoundNumber)
                .ThenByDescending(b => b.CreatedAt)
                .FirstOrDefault();

        public static async Task<ApplicationInterviewCodeDto> BuildAsync(
            IUnitOfWork unitOfWork, IConfiguration configuration,
            ARI.Domain.Entities.Application app, CancellationToken ct)
        {
            var kiosk = KioskUrl(configuration);

            ApplicationInterviewCodeDto Blocked(string reason, int? round = null, string? roundType = null) =>
                new(round, roundType, InterviewInviteEmail.IsRemoteRound(roundType), false, reason,
                    null, null, kiosk, null);

            if (ApplicationStatuses.IsTerminal(app.Status))
                return Blocked("Hồ sơ đã đóng — không cấp mã phỏng vấn.");

            var live = await LiveBookingAsync(unitOfWork, app.Id, ct);
            if (live == null)
                return Blocked("Ứng viên chưa có lịch phỏng vấn — xếp lịch trước rồi mới cấp mã.");

            var round = live.RoundNumber;
            var roundType = await SchedulingSupport.RoundTypeAsync(unitOfWork, app.JobPostingId, round, ct);

            if (InterviewInviteEmail.IsOnlineTest(roundType))
                return Blocked(InterviewCodeRules.OnlineTestReason(round), round, roundType);

            var entered = await InterviewCodeRules.EnteredRoomReasonAsync(unitOfWork, app.Id, round, ct);
            if (entered != null)
                return Blocked(entered, round, roundType);

            var now = DateTimeOffset.UtcNow;
            var active = (await unitOfWork.Repository<InterviewCode>().FindAsync(
                    c => c.ApplicationId == app.Id && c.RoundNumber == round && c.UsedAt == null && c.ExpiresAt > now, ct))
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefault();

            var entry = active != null && kiosk != null
                ? $"{kiosk}?code={Uri.EscapeDataString(active.Code)}"
                : null;

            return new ApplicationInterviewCodeDto(
                round, roundType, InterviewInviteEmail.IsRemoteRound(roundType), true, null,
                active?.Code, active?.ExpiresAt, kiosk, entry);
        }

        /// <summary>
        /// Chỉ chủ tin hoặc quản trị viên — cùng ngưỡng với xếp lịch (<see cref="JobAccess.CanManageAsync"/>).
        /// Mã là chìa khoá mở phòng phỏng vấn thật của một người; thành viên đội tuyển dụng (kể cả HM)
        /// xem được hồ sơ nhưng không phát chìa khoá.
        /// </summary>
        public static async Task<(ARI.Domain.Entities.Application? app, Result? denied)> AuthorizeAsync(
            IUnitOfWork unitOfWork, Guid applicationId, Guid? userId, string? role, CancellationToken ct)
        {
            var (app, job, level) = await JobAccess.EvaluateApplicationAsync(unitOfWork, applicationId, userId, role, ct);
            if (app == null || job == null)
                return (null, Result.Failure(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound));
            if (level < JobAccessLevel.Owner)
                return (null, Result.Failure(JobAccessErrors.ApplicationManageForbidden, CommonErrorCodes.Forbidden));
            return (app, null);
        }
    }

    // ============================================================
    // GET /api/applications/{id}/interview-code
    // ============================================================

    public record GetApplicationInterviewCodeQuery(Guid ApplicationId, Guid? UserId, string? Role)
        : IRequest<Result<ApplicationInterviewCodeDto>>;

    public class GetApplicationInterviewCodeQueryHandler
        : IRequestHandler<GetApplicationInterviewCodeQuery, Result<ApplicationInterviewCodeDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public GetApplicationInterviewCodeQueryHandler(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<Result<ApplicationInterviewCodeDto>> Handle(
            GetApplicationInterviewCodeQuery request, CancellationToken ct)
        {
            var (app, denied) = await ApplicationInterviewCodeSupport.AuthorizeAsync(
                _unitOfWork, request.ApplicationId, request.UserId, request.Role, ct);
            if (denied != null) return Result.Failure<ApplicationInterviewCodeDto>(denied.Error, denied.ErrorCode!);

            return Result.Success(await ApplicationInterviewCodeSupport.BuildAsync(_unitOfWork, _configuration, app!, ct));
        }
    }

    // ============================================================
    // POST /api/applications/{id}/interview-code
    // ============================================================

    /// <param name="Regenerate">
    /// <c>false</c>: đang có mã còn hiệu lực thì trả lại CHÍNH mã đó — bấm lại để xem mã không được
    /// làm mất mã ứng viên đang cầm. <c>true</c>: cấp mã mới, mã cũ hết hiệu lực ngay (ứng viên làm mất
    /// mã, hoặc mã đã lộ).
    /// </param>
    public record IssueApplicationInterviewCodeCommand(Guid ApplicationId, bool Regenerate, Guid? UserId, string? Role)
        : IRequest<Result<ApplicationInterviewCodeDto>>;

    public class IssueApplicationInterviewCodeCommandHandler
        : IRequestHandler<IssueApplicationInterviewCodeCommand, Result<ApplicationInterviewCodeDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IInterviewCodeService _interviewCodeService;

        public IssueApplicationInterviewCodeCommandHandler(
            IUnitOfWork unitOfWork, IConfiguration configuration, IInterviewCodeService interviewCodeService)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _interviewCodeService = interviewCodeService;
        }

        public async Task<Result<ApplicationInterviewCodeDto>> Handle(
            IssueApplicationInterviewCodeCommand request, CancellationToken ct)
        {
            var (app, denied) = await ApplicationInterviewCodeSupport.AuthorizeAsync(
                _unitOfWork, request.ApplicationId, request.UserId, request.Role, ct);
            if (denied != null) return Result.Failure<ApplicationInterviewCodeDto>(denied.Error, denied.ErrorCode!);

            var state = await ApplicationInterviewCodeSupport.BuildAsync(_unitOfWork, _configuration, app!, ct);
            if (!state.CanIssue)
                return Result.Failure<ApplicationInterviewCodeDto>(state.BlockedReason ?? "Chưa cấp được mã phỏng vấn.");

            if (state.Code != null && !request.Regenerate)
                return Result.Success(state);

            // Mọi luật cấp mã (lịch, loại vòng, đã vào phòng, một mã sống mỗi vòng, audit, báo ứng viên)
            // nằm ở một chỗ — nút này chỉ chọn ĐÚNG vòng rồi đi qua đó.
            var generated = await _interviewCodeService.GenerateCodeAsync(
                app!.Id, state.RoundNumber, request.UserId ?? Guid.Empty, ct);
            if (generated.IsFailure)
                return Result.Failure<ApplicationInterviewCodeDto>(generated.Error);

            return Result.Success(await ApplicationInterviewCodeSupport.BuildAsync(_unitOfWork, _configuration, app, ct));
        }
    }
}
