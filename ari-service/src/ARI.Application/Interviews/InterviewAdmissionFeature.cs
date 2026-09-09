using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Hubs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Interviews
{
    /// <summary>
    /// Phòng chờ buổi phỏng vấn THẬT (ADR-067).
    ///
    /// Luật nghiệp vụ: <b>buổi phỏng vấn chỉ bắt đầu khi Hiring Manager đã vào phòng cùng AI, và ứng
    /// viên vào phòng phải được HM cho vào.</b> Hai điều kiện đó là hai thao tác tách bạch — vào phòng
    /// và cho vào — vì chúng xảy ra ở hai thời điểm khác nhau: HM vào trước, ứng viên có thể tới sớm
    /// hoặc muộn, và HM cần nhìn thấy ai đang đứng ngoài trước khi mở cửa.
    ///
    /// <b>Vì sao chốt chặn nằm ở trạng thái phiên chứ không ở giao diện.</b> Phiên sinh ra ở
    /// <see cref="InterviewSessionStatuses.Waiting"/>, mà <c>GenerateAndSendNextQuestionAsync</c> chỉ
    /// chạy khi phiên <see cref="InterviewSessionStatuses.Active"/>. Ứng viên (hoặc máy Kiosk) có gọi
    /// thẳng SignalR cũng không lấy được câu hỏi nào — không cần rải thêm câu kiểm nào ở tầng trên.
    /// </summary>
    internal static class AdmissionGate
    {
        internal record Decision(
            string? Error, string? Code,
            InterviewSession? Session,
            ARI.Domain.Entities.Application? Application,
            JobPosting? Job,
            bool IsPrimaryHm);

        /// <summary>
        /// Ai được điều khiển phòng chờ của phiên này.
        ///
        /// Hiring Manager của tin là người đương nhiên. Quản trị viên và chủ tin cũng làm được —
        /// nhưng đó là lối thoát hiểm có DẤU VẾT (audit log), không phải quyền ngang hàng: ứng viên
        /// đã đến văn phòng rồi mà HM kẹt họp thì buổi phỏng vấn không được phép chết đứng.
        /// </summary>
        public static async Task<Decision> EvaluateAsync(
            IUnitOfWork unitOfWork, Guid sessionId, Guid? actorId, string? actorRole, CancellationToken ct)
        {
            if (actorId is not { } uid || uid == Guid.Empty)
                return new Decision("Không xác định được người dùng.", CommonErrorCodes.Forbidden, null, null, null, false);

            var session = await unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return new Decision("Không tìm thấy phiên phỏng vấn.", CommonErrorCodes.NotFound, null, null, null, false);

            if (!string.Equals(session.SessionType, "real", StringComparison.OrdinalIgnoreCase))
                return new Decision("Phòng chờ chỉ áp dụng cho buổi phỏng vấn thật.", null, session, null, null, false);

            var (app, job, level) = await JobAccess.EvaluateApplicationAsync(
                unitOfWork, session.ApplicationId, actorId, actorRole, ct);
            if (app == null || job == null)
                return new Decision(JobAccessErrors.ApplicationNotFound, CommonErrorCodes.NotFound, session, null, null, false);

            var hm = await JobAccess.PrimaryHiringManagerAsync(unitOfWork, job.Id, ct);
            var isPrimaryHm = hm != null && hm.UserId == uid;

            if (!isPrimaryHm && level < JobAccessLevel.Owner)
                return new Decision(
                    "Chỉ Hiring Manager phụ trách tin này (hoặc chủ tin / quản trị viên) mới điều khiển được phòng phỏng vấn.",
                    CommonErrorCodes.Forbidden, session, app, job, false);

            return new Decision(null, null, session, app, job, isPrimaryHm);
        }
    }

    // ============================================================
    // GET /api/interview/waiting-rooms
    // ============================================================

    public record WaitingRoomDto(
        Guid SessionId, Guid ApplicationId, Guid JobPostingId,
        string? CandidateName, string? JobTitle,
        int RoundNumber, string? RoundType,
        string Status, DateTimeOffset CreatedAt,
        DateTimeOffset? HmJoinedAt, DateTimeOffset? AdmittedAt);

    /// <summary>
    /// Buổi phỏng vấn thật đang MỞ trong phạm vi của người gọi: đang chờ cho vào, hoặc đang diễn ra.
    ///
    /// Có cả phiên đang diễn ra chứ không chỉ phiên chờ: HM đóng nhầm tab giữa buổi thì vẫn phải tìm
    /// đường quay lại phòng, mà một danh sách chỉ hiện "đang chờ" sẽ nói với họ rằng không còn gì.
    /// </summary>
    public record GetWaitingRoomsQuery(Guid? UserId, string? Role) : IRequest<Result<List<WaitingRoomDto>>>;

    public class GetWaitingRoomsQueryHandler : IRequestHandler<GetWaitingRoomsQuery, Result<List<WaitingRoomDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetWaitingRoomsQueryHandler(IUnitOfWork unitOfWork) { _unitOfWork = unitOfWork; }

        public async Task<Result<List<WaitingRoomDto>>> Handle(GetWaitingRoomsQuery request, CancellationToken ct)
        {
            var open = new[] { InterviewSessionStatuses.Waiting, InterviewSessionStatuses.Active };
            var sessions = (await _unitOfWork.Repository<InterviewSession>().FindAsync(
                    s => s.SessionType == "real" && open.Contains(s.Status), ct))
                .OrderBy(s => s.CreatedAt)
                .ToList();
            if (sessions.Count == 0) return Result.Success(new List<WaitingRoomDto>());

            var appIds = sessions.Select(s => s.ApplicationId).Distinct().ToList();
            var apps = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .FindAsync(a => appIds.Contains(a.Id), ct)).ToList();

            // Phạm vi do SERVER quyết định (quy tắc 19) — null nghĩa là quản trị viên, thấy tất cả.
            var scope = await JobAccess.ScopedJobIdsAsync(_unitOfWork, request.UserId, request.Role, ct);
            if (scope != null)
                apps = apps.Where(a => scope.Contains(a.JobPostingId)).ToList();

            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobs = jobIds.Count == 0
                ? new List<JobPosting>()
                : (await _unitOfWork.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id), ct)).ToList();

            var result = new List<WaitingRoomDto>();
            foreach (var s in sessions)
            {
                var app = apps.FirstOrDefault(a => a.Id == s.ApplicationId);
                if (app == null) continue; // ngoài phạm vi của người gọi
                var job = jobs.FirstOrDefault(j => j.Id == app.JobPostingId);
                result.Add(new WaitingRoomDto(
                    s.Id, app.Id, app.JobPostingId, app.CandidateName, job?.Title,
                    s.RoundNumber, s.RoundType, s.Status, s.CreatedAt, s.HmJoinedAt, s.AdmittedAt));
            }

            return Result.Success(result);
        }
    }

    // ============================================================
    // POST /api/interview/session/{id}/hm-join
    // ============================================================

    /// <summary>
    /// Hiring Manager vào phòng cùng AI. Đây là ĐIỀU KIỆN của việc bắt đầu, không phải một dấu vết:
    /// chưa có mốc này thì không cho ứng viên vào được.
    /// </summary>
    public record JoinInterviewRoomCommand(Guid SessionId, Guid? ActorId, string? ActorRole)
        : IRequest<Result<WaitingRoomDto>>;

    public class JoinInterviewRoomCommandHandler
        : IRequestHandler<JoinInterviewRoomCommand, Result<WaitingRoomDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public JoinInterviewRoomCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<WaitingRoomDto>> Handle(JoinInterviewRoomCommand request, CancellationToken ct)
        {
            var gate = await AdmissionGate.EvaluateAsync(
                _unitOfWork, request.SessionId, request.ActorId, request.ActorRole, ct);
            if (gate.Error != null)
                return gate.Code == null
                    ? Result.Failure<WaitingRoomDto>(gate.Error)
                    : Result.Failure<WaitingRoomDto>(gate.Error, gate.Code);

            var session = gate.Session!;
            if (InterviewSessionStatuses.IsClosed(session.Status))
                return Result.Failure<WaitingRoomDto>("Buổi phỏng vấn này đã kết thúc.");

            // Vào lại phòng sau khi rớt mạng là chuyện bình thường → giữ nguyên mốc đầu tiên, chỉ
            // ghi đè khi người ngồi trong phòng đổi.
            if (session.HmJoinedAt == null || session.HmJoinedByUserId != request.ActorId)
            {
                session.HmJoinedAt ??= DateTimeOffset.UtcNow;
                session.HmJoinedByUserId = request.ActorId;
                session.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<InterviewSession>().Update(session);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // Máy Kiosk đang ở màn chờ — cho nó biết người phỏng vấn đã có mặt, thay vì để ứng viên
            // nhìn một màn hình không đổi và tưởng hệ thống treo.
            await _notifications.PublishInterviewSessionEventAsync(
                session.Id, nameof(ISessionClient.ReceiveSessionStatus), "hm_joined", ct);

            return Result.Success(Map(session, gate));
        }

        internal static WaitingRoomDto Map(InterviewSession s, AdmissionGate.Decision gate) => new(
            s.Id, s.ApplicationId, gate.Job?.Id ?? Guid.Empty,
            gate.Application?.CandidateName, gate.Job?.Title,
            s.RoundNumber, s.RoundType, s.Status, s.CreatedAt, s.HmJoinedAt, s.AdmittedAt);
    }

    // ============================================================
    // POST /api/interview/session/{id}/admit
    // ============================================================

    /// <summary>
    /// Hiring Manager cho ứng viên vào phòng — phiên chuyển sang <c>active</c> và AI bắt đầu hỏi.
    /// </summary>
    public record AdmitCandidateCommand(Guid SessionId, Guid? ActorId, string? ActorRole)
        : IRequest<Result<WaitingRoomDto>>;

    public class AdmitCandidateCommandHandler : IRequestHandler<AdmitCandidateCommand, Result<WaitingRoomDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public AdmitCandidateCommandHandler(IUnitOfWork unitOfWork, INotificationService notifications)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
        }

        public async Task<Result<WaitingRoomDto>> Handle(AdmitCandidateCommand request, CancellationToken ct)
        {
            var gate = await AdmissionGate.EvaluateAsync(
                _unitOfWork, request.SessionId, request.ActorId, request.ActorRole, ct);
            if (gate.Error != null)
                return gate.Code == null
                    ? Result.Failure<WaitingRoomDto>(gate.Error)
                    : Result.Failure<WaitingRoomDto>(gate.Error, gate.Code);

            var session = gate.Session!;
            if (InterviewSessionStatuses.Is(session.Status, InterviewSessionStatuses.Active))
                return Result.Success(JoinInterviewRoomCommandHandler.Map(session, gate)); // đã cho vào rồi — idempotent
            if (!InterviewSessionStatuses.Is(session.Status, InterviewSessionStatuses.Waiting))
                return Result.Failure<WaitingRoomDto>("Buổi phỏng vấn này không ở trạng thái chờ vào phòng.");

            // Điều kiện của ADR-067: HM phải ĐANG Ở TRONG PHÒNG. Kiểm ở đây chứ không suy ra từ việc
            // "người bấm nút chính là HM" — người bấm có thể là quản trị viên mở cửa hộ, và lúc đó
            // câu hỏi vẫn là "trong phòng đã có ai chưa", không phải "ai bấm nút".
            if (session.HmJoinedAt == null)
                return Result.Failure<WaitingRoomDto>(
                    "Chưa có ai vào phòng phỏng vấn cùng AI. Hãy vào phòng trước khi cho ứng viên vào.");

            var now = DateTimeOffset.UtcNow;
            session.Status = InterviewSessionStatuses.Active;
            session.AdmittedAt = now;
            session.AdmittedByUserId = request.ActorId;
            // Trần thời lượng tính từ ĐÂY — thời gian ứng viên ngồi chờ không được trừ vào giờ của họ.
            session.StartedAt = now;
            session.UpdatedAt = now;
            _unitOfWork.Repository<InterviewSession>().Update(session);

            // Quản trị viên mở cửa thay Hiring Manager là ngoại lệ có thật nhưng phải để lại dấu vết —
            // cùng khuôn với việc vượt cổng duyệt shortlist ở ADR-061.
            if (!gate.IsPrimaryHm)
            {
                await AdminSupport.WriteAuditAsync(_unitOfWork, request.ActorId,
                    "interview_admitted_without_hm", "InterviewSession", session.Id,
                    AuditMetadata.Serialize(new
                    {
                        jobTitle = gate.Job?.Title,
                        candidate = gate.Application?.CandidateName,
                        roundNumber = session.RoundNumber,
                    }), ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.PublishInterviewSessionEventAsync(
                session.Id, nameof(ISessionClient.ReceiveSessionStatus), "admitted", ct);

            return Result.Success(JoinInterviewRoomCommandHandler.Map(session, gate));
        }
    }
}
