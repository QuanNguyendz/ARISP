using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Evaluations
{
    /// <summary>
    /// Buổi phỏng vấn thật đang ở đâu trên đường tới báo cáo — thứ nhân sự cần biết để bấm được vào đúng
    /// chỗ. Suy ra từ dữ liệu (ca đã gán · phiên · đánh giá · lượt chốt), không lưu cột nào.
    /// </summary>
    public static class InterviewResultStates
    {
        /// <summary>Đã gán ca, chưa tới giờ.</summary>
        public const string Scheduled = "scheduled";
        /// <summary>Quá giờ ca mà chưa có phiên nào. Chưa phải "vắng mặt": bộ quét no-show còn chờ ân hạn.</summary>
        public const string Overdue = "overdue";
        /// <summary>Ứng viên đã nhập mã, đang ở phòng chờ.</summary>
        public const string Waiting = "waiting";
        public const string InProgress = "in_progress";
        /// <summary>Buổi đã kết thúc, AI đang viết báo cáo (hoặc đang chờ tới lượt / đang thử lại sau một lần lỗi).</summary>
        public const string Evaluating = "evaluating";
        /// <summary>
        /// Buổi đã kết thúc nhưng vòng chưa có bộ tiêu chí chấm phỏng vấn — việc của Hiring Manager (ADR-073).
        /// Khai xong là báo cáo tự sinh.
        /// </summary>
        public const string NeedsRubric = "needs_rubric";
        /// <summary>AI hỏng hết số lượt thử tự động — cần người bấm "Chấm lại".</summary>
        public const string EvaluationFailed = "evaluation_failed";
        /// <summary>Có báo cáo, chờ Hiring Manager chốt.</summary>
        public const string PendingReview = "pending_review";
        public const string Reviewed = "reviewed";
        /// <summary>Phiên bị huỷ ngang hoặc lỗi — không có báo cáo nào để xem.</summary>
        public const string Aborted = "aborted";
    }

    /// <summary>
    /// Một buổi phỏng vấn thật (một vòng hội thoại) của một hồ sơ: giờ ca, diễn biến phiên, báo cáo AI,
    /// và có video / transcript để xem hay không.
    /// </summary>
    public class InterviewResultRowDto
    {
        public Guid ApplicationId { get; set; }
        public Guid JobPostingId { get; set; }
        public string? CandidateName { get; set; }
        public string? JobTitle { get; set; }
        public int RoundNumber { get; set; }
        public string? RoundType { get; set; }

        /// <summary>Ca đã gán cho vòng này (ADR-048/067) — một ca một ứng viên.</summary>
        public DateTimeOffset? SlotStartTime { get; set; }
        public DateTimeOffset? SlotEndTime { get; set; }

        /// <summary>Xem <see cref="InterviewResultStates"/>.</summary>
        public string State { get; set; } = InterviewResultStates.Scheduled;

        public Guid? SessionId { get; set; }
        public string? SessionStatus { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int? DurationSeconds { get; set; }

        public Guid? EvaluationId { get; set; }
        public string? AiVerdict { get; set; }
        public decimal? OverallScore { get; set; }
        /// <summary>Kết quả đã chốt (null = chưa chốt).</summary>
        public string? FinalVerdict { get; set; }
        public string? ReviewerRole { get; set; }
        public bool IsHrFallback { get; set; }

        public bool HasRecording { get; set; }
        public DateTimeOffset? RecordingExpiresAt { get; set; }
        public DateTimeOffset? RecordingDeletedAt { get; set; }

        /// <summary>Số lượt hỏi–đáp đã có câu trả lời — 0 nghĩa là không có transcript để đọc.</summary>
        public int TranscriptTurns { get; set; }

        /// <summary>Trạng thái sinh báo cáo (xem <see cref="EvaluationStatuses"/>) — null với buổi chưa đóng.</summary>
        public string? EvaluationStatus { get; set; }
        /// <summary>Lý do lượt chấm gần nhất thất bại — hiện kèm nút "Chấm lại".</summary>
        public string? EvaluationError { get; set; }
    }

    /// <summary>
    /// Dựng <see cref="InterviewResultRowDto"/> theo LÔ — mọi bảng liên quan nạp một lần rồi ghép trong bộ
    /// nhớ, để màn danh sách không bắn N truy vấn cho N hồ sơ.
    /// </summary>
    internal static class InterviewResultRows
    {
        public static async Task<List<InterviewResultRowDto>> BuildAsync(
            IUnitOfWork uow,
            IReadOnlyCollection<ARI.Domain.Entities.Application> apps,
            IReadOnlyCollection<InterviewSession> sessions,
            bool includeRoundsWithoutSession,
            CancellationToken ct)
        {
            if (apps.Count == 0) return new List<InterviewResultRowDto>();

            var appIds = apps.Select(a => a.Id).ToList();
            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();

            var jobs = (await uow.Repository<JobPosting>().FindAsync(j => jobIds.Contains(j.Id), ct))
                .ToDictionary(j => j.Id);
            var roundTypes = (await uow.Repository<InterviewRoundConfig>().FindAsync(r => jobIds.Contains(r.JobPostingId), ct))
                .GroupBy(r => (r.JobPostingId, r.RoundNumber))
                .ToDictionary(g => g.Key, g => g.First().RoundType);

            // Ca đang giữ chỗ của từng vòng — booking báo bận/bị huỷ không phải giờ hẹn của ai nữa.
            var bookings = (await uow.Repository<InterviewBooking>().FindAsync(
                    b => appIds.Contains(b.ApplicationId)
                         && b.Status != BookingStatus.Cancelled
                         && b.Status != BookingStatus.Declined, ct))
                .GroupBy(b => (b.ApplicationId, b.RoundNumber))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CreatedAt).First());
            var slotIds = bookings.Values.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slots = slotIds.Count == 0
                ? new Dictionary<Guid, AvailabilitySlot>()
                : (await uow.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id), ct))
                    .ToDictionary(s => s.Id);

            var sessionIds = sessions.Select(s => s.Id).ToList();
            var evaluations = sessionIds.Count == 0
                ? new Dictionary<Guid, Evaluation>()
                : (await uow.Repository<Evaluation>().FindAsync(e => sessionIds.Contains(e.SessionId), ct))
                    .GroupBy(e => e.SessionId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());
            var evalIds = evaluations.Values.Select(e => e.Id).ToList();
            var reviews = evalIds.Count == 0
                ? new Dictionary<Guid, HrReview>()
                : (await uow.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId), ct))
                    .GroupBy(r => r.EvaluationId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First());
            // "Có transcript" = có lời trả lời THẬT — bản ghi chỉ toàn khoảng trắng (ứng viên bấm gửi khi mic
            // chưa bắt được gì) không phải thứ đáng mở ra đọc.
            var answered = sessionIds.Count == 0
                ? new Dictionary<Guid, int>()
                : (await uow.Repository<Answer>().FindAsync(
                        a => sessionIds.Contains(a.SessionId) && a.Transcript != null, ct))
                    .Where(a => !string.IsNullOrWhiteSpace(a.Transcript))
                    .GroupBy(a => a.SessionId)
                    .ToDictionary(g => g.Key, g => g.Select(a => a.QuestionId).Distinct().Count());

            // Mỗi vòng chỉ một dòng: phiên CÓ báo cáo được ưu tiên (đó là thứ người dùng tìm), không có
            // thì lấy phiên mới nhất — một phiên lỗi rồi làm lại không được che mất phiên đã có kết quả.
            var sessionByRound = sessions
                .GroupBy(s => (s.ApplicationId, s.RoundNumber))
                .ToDictionary(g => g.Key, g => g
                    .OrderByDescending(s => evaluations.ContainsKey(s.Id))
                    .ThenByDescending(s => s.CreatedAt)
                    .First());

            var keys = new HashSet<(Guid, int)>(sessionByRound.Keys);
            if (includeRoundsWithoutSession)
            {
                foreach (var key in bookings.Keys)
                {
                    var app = apps.First(a => a.Id == key.ApplicationId);
                    // Vòng trắc nghiệm không có phiên hội thoại — bảng điểm bài thi có màn riêng.
                    if (roundTypes.TryGetValue((app.JobPostingId, key.RoundNumber), out var type)
                        && !InterviewRoundTypes.NeedsHiringManager(type))
                        continue;
                    keys.Add(key);
                }
            }

            var now = DateTimeOffset.UtcNow;
            var rows = new List<InterviewResultRowDto>();
            foreach (var (appId, round) in keys)
            {
                var app = apps.First(a => a.Id == appId);
                jobs.TryGetValue(app.JobPostingId, out var job);
                sessionByRound.TryGetValue((appId, round), out var session);
                bookings.TryGetValue((appId, round), out var booking);
                AvailabilitySlot? slot = null;
                if (booking != null) slots.TryGetValue(booking.AvailabilitySlotId, out slot);

                var row = new InterviewResultRowDto
                {
                    ApplicationId = app.Id,
                    JobPostingId = app.JobPostingId,
                    CandidateName = app.CandidateName,
                    JobTitle = job?.Title,
                    RoundNumber = round,
                    RoundType = session?.RoundType is { Length: > 0 } rt
                        ? rt
                        : roundTypes.TryGetValue((app.JobPostingId, round), out var cfgType) ? cfgType : null,
                    SlotStartTime = slot?.StartTime,
                    SlotEndTime = slot?.EndTime,
                };

                if (session == null)
                {
                    row.State = slot != null && slot.EndTime < now
                        ? InterviewResultStates.Overdue
                        : InterviewResultStates.Scheduled;
                    rows.Add(row);
                    continue;
                }

                row.SessionId = session.Id;
                row.SessionStatus = session.Status;
                row.StartedAt = session.StartedAt;
                row.EndedAt = session.EndedAt;
                row.DurationSeconds = session.DurationSeconds;
                row.HasRecording = !string.IsNullOrEmpty(session.RecordingUrl);
                row.RecordingExpiresAt = session.RecordingExpiresAt;
                row.RecordingDeletedAt = session.RecordingDeletedAt;
                row.TranscriptTurns = answered.TryGetValue(session.Id, out var n) ? n : 0;
                row.EvaluationStatus = session.EvaluationStatus;
                row.EvaluationError = session.EvaluationError;

                if (evaluations.TryGetValue(session.Id, out var eval))
                {
                    row.EvaluationId = eval.Id;
                    row.AiVerdict = eval.AiVerdict;
                    row.OverallScore = eval.OverallScore;
                    if (reviews.TryGetValue(eval.Id, out var review))
                    {
                        row.FinalVerdict = review.FinalVerdict;
                        row.ReviewerRole = review.ReviewerRole;
                        row.IsHrFallback = review.IsHrFallback;
                        row.State = InterviewResultStates.Reviewed;
                    }
                    else
                    {
                        row.State = InterviewResultStates.PendingReview;
                    }
                }
                else
                {
                    row.State = session.Status switch
                    {
                        InterviewSessionStatuses.Waiting => InterviewResultStates.Waiting,
                        InterviewSessionStatuses.Active => InterviewResultStates.InProgress,
                        // Trước ADR-073 "đã xong mà chưa có báo cáo" luôn là "AI đang chấm" — kể cả khi báo cáo
                        // không bao giờ tới vì tin thiếu bộ tiêu chí. Nay nói đúng lý do và ai phải làm gì.
                        InterviewSessionStatuses.Completed => session.EvaluationStatus switch
                        {
                            EvaluationStatuses.BlockedNoRubric => InterviewResultStates.NeedsRubric,
                            EvaluationStatuses.Failed when session.EvaluationAttempts >= EvaluationStatuses.MaxAttempts
                                => InterviewResultStates.EvaluationFailed,
                            _ => InterviewResultStates.Evaluating,
                        },
                        InterviewSessionStatuses.Aborted or InterviewSessionStatuses.Error => InterviewResultStates.Aborted,
                        _ => InterviewResultStates.Scheduled,
                    };
                }

                rows.Add(row);
            }

            return rows;
        }
    }

    // ============================================================
    // GET /api/evaluations/application/{applicationId}/interviews
    // ============================================================

    /// <summary>
    /// Các buổi phỏng vấn thật của MỘT hồ sơ, theo vòng: ca đã gán, diễn biến, báo cáo AI, video,
    /// transcript. Dùng ở màn tin (khối ứng viên) và màn hồ sơ của cả ba vai.
    ///
    /// Vì sao không dùng lại danh sách đánh giá: một buổi vừa kết thúc chưa có báo cáo (AI còn đang
    /// viết), và một buổi hỏng giữa chừng sẽ không bao giờ có — danh sách đánh giá khi đó im lặng, người
    /// dùng tưởng buổi phỏng vấn "không có kết quả". Danh sách theo vòng nói ra đang ở bước nào.
    /// </summary>
    public record GetApplicationInterviewResultsQuery(Guid ApplicationId, Guid? UserId, string? Role)
        : IRequest<Result<List<InterviewResultRowDto>>>;

    public class GetApplicationInterviewResultsQueryHandler
        : IRequestHandler<GetApplicationInterviewResultsQuery, Result<List<InterviewResultRowDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetApplicationInterviewResultsQueryHandler(IUnitOfWork unitOfWork) { _unitOfWork = unitOfWork; }

        public async Task<Result<List<InterviewResultRowDto>>> Handle(
            GetApplicationInterviewResultsQuery request, CancellationToken ct)
        {
            var (application, job, level) = await JobAccess.EvaluateApplicationAsync(
                _unitOfWork, request.ApplicationId, request.UserId, request.Role, ct);
            if (application == null || job == null)
                return Result.Failure<List<InterviewResultRowDto>>("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);
            if (level < JobAccessLevel.TeamMember)
                return Result.Failure<List<InterviewResultRowDto>>(JobAccessErrors.EvaluationForbidden, CommonErrorCodes.Forbidden);

            // Chỉ buổi THẬT — buổi thử là không gian riêng của ứng viên (ADR-051).
            var sessions = (await _unitOfWork.Repository<InterviewSession>().FindAsync(
                s => s.ApplicationId == application.Id && s.SessionType == "real", ct)).ToList();

            var rows = await InterviewResultRows.BuildAsync(
                _unitOfWork, new[] { application }, sessions, includeRoundsWithoutSession: true, ct);

            return Result.Success(rows.OrderBy(r => r.RoundNumber).ToList());
        }
    }

    // ============================================================
    // GET /api/evaluations/recent-interviews
    // ============================================================

    /// <summary>
    /// Buổi phỏng vấn thật vừa KẾT THÚC trong phạm vi của người gọi, mới nhất trước.
    ///
    /// Sinh ra cho màn "Phòng phỏng vấn" của Hiring Manager: HM ngồi trong phòng suốt buổi, buổi kết thúc
    /// là thẻ phòng biến mất — và không còn đường nào dẫn tới báo cáo AI của chính buổi vừa ngồi.
    /// </summary>
    public record GetRecentInterviewResultsQuery(Guid? UserId, string? Role, int Hours = 24)
        : IRequest<Result<List<InterviewResultRowDto>>>;

    public class GetRecentInterviewResultsQueryHandler
        : IRequestHandler<GetRecentInterviewResultsQuery, Result<List<InterviewResultRowDto>>>
    {
        private const int MaxHours = 24 * 7;
        private readonly IUnitOfWork _unitOfWork;

        public GetRecentInterviewResultsQueryHandler(IUnitOfWork unitOfWork) { _unitOfWork = unitOfWork; }

        public async Task<Result<List<InterviewResultRowDto>>> Handle(
            GetRecentInterviewResultsQuery request, CancellationToken ct)
        {
            var hours = Math.Clamp(request.Hours, 1, MaxHours);
            var since = DateTimeOffset.UtcNow.AddHours(-hours);

            var sessions = (await _unitOfWork.Repository<InterviewSession>().FindAsync(
                    s => s.SessionType == "real" && s.EndedAt != null && s.EndedAt >= since, ct))
                .ToList();
            if (sessions.Count == 0) return Result.Success(new List<InterviewResultRowDto>());

            var appIds = sessions.Select(s => s.ApplicationId).Distinct().ToList();
            var apps = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .FindAsync(a => appIds.Contains(a.Id), ct)).ToList();

            // Phạm vi do SERVER quyết định (quy tắc 19) — null nghĩa là quản trị viên, thấy tất cả.
            var scope = await JobAccess.ScopedJobIdsAsync(_unitOfWork, request.UserId, request.Role, ct);
            if (scope != null)
                apps = apps.Where(a => scope.Contains(a.JobPostingId)).ToList();
            var inScope = apps.Select(a => a.Id).ToHashSet();
            sessions = sessions.Where(s => inScope.Contains(s.ApplicationId)).ToList();

            var rows = await InterviewResultRows.BuildAsync(
                _unitOfWork, apps, sessions, includeRoundsWithoutSession: false, ct);

            return Result.Success(rows.OrderByDescending(r => r.EndedAt).ToList());
        }
    }
}
