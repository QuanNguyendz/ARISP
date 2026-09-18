using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Interviews;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.CandidatePortal
{
    // ============================================================
    // GET /api/portal/applications — danh sách hồ sơ của ứng viên hiện tại
    // ============================================================

    public record GetMyApplicationsQuery(Guid CandidateAccountId, string? Email) : IRequest<Result<object>>;

    public class GetMyApplicationsQueryHandler : IRequestHandler<GetMyApplicationsQuery, Result<object>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly Options.InterviewOptions _interviewOptions;

        public GetMyApplicationsQueryHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage, Options.InterviewOptions interviewOptions)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _interviewOptions = interviewOptions;
        }

        public async Task<Result<object>> Handle(GetMyApplicationsQuery request, CancellationToken ct)
        {
            try
            {
                var candidateAccountId = request.CandidateAccountId;
                var emailClaim = request.Email;

                // Liên kết các hồ sơ khớp email nhưng chưa gắn CandidateAccountId — làm thẳng bằng
                // SQL UPDATE (không nạp entity). Sau đó mọi hồ sơ của ứng viên đều có CandidateAccountId.
                if (!string.IsNullOrEmpty(emailClaim))
                {
                    try
                    {
                        await _unitOfWork.ExecuteSqlRawAsync(
                            "UPDATE applications SET candidate_account_id = {0}, updated_at = {1} " +
                            "WHERE candidate_account_id IS NULL AND candidate_email = {2} AND deleted_at IS NULL",
                            new object[] { candidateAccountId, DateTimeOffset.UtcNow, emailClaim });
                    }
                    catch
                    {
                        // Fail-safe: không để lỗi auto-link làm sập toàn bộ request lấy danh sách hồ sơ
                    }
                }

                // Hồ sơ của ứng viên — projection nhẹ (KHÔNG kéo CvText/CoverLetter/DemographicData lớn).
                var appsList = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .QueryAsync(q => q
                        .Where(a => a.CandidateAccountId == candidateAccountId)
                        .Select(a => new
                        {
                            a.Id, a.JobPostingId, a.CandidateEmail, a.CandidateName, a.Status,
                            a.CvJdAnalysisId, a.CvFileUrl, a.PracticeSessionUsed, a.Source, a.CreatedAt, a.UpdatedAt,
                        }));

                // Batch fetch jobs — chỉ cột cần (bỏ JobDescription/ScoringRubric...)
                var jobIds = appsList.Select(a => a.JobPostingId).Distinct().ToList();
                var jobsDict = (await _unitOfWork.Repository<JobPosting>()
                        .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id))
                            .Select(j => new { j.Id, j.Title, j.Location, j.Department, j.InterviewMode })))
                    .GroupBy(j => j.Id)
                    .ToDictionary(g => g.Key, g => g.First());

                // Batch fetch CV–JD analyses (chỉ match score)
                var analysisIds = appsList.Where(a => a.CvJdAnalysisId.HasValue).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
                var analysisDict = (await _unitOfWork.Repository<CvJdAnalysis>()
                        .QueryAsync(q => q.Where(x => analysisIds.Contains(x.Id)).Select(x => new { x.Id, x.MatchScore, x.Status, x.RubricDocumentId })))
                    .GroupBy(x => x.Id)
                    .ToDictionary(g => g.Key, g => g.First());

                // Sessions + evaluations (bỏ cột JSON lớn) + HR reviews để dựng tiến trình vòng phỏng vấn
                var appIds = appsList.Select(a => a.Id).ToList();
                var allSessions = await _unitOfWork.Repository<InterviewSession>()
                    .QueryAsync(q => q.Where(s => appIds.Contains(s.ApplicationId))
                        .Select(s => new { s.Id, s.ApplicationId, s.RoundNumber, s.RoundType, s.SessionType, s.Status }));
                var sessionIds = allSessions.Select(s => s.Id).ToList();
                var allEvals = await _unitOfWork.Repository<Evaluation>()
                    .QueryAsync(q => q.Where(e => sessionIds.Contains(e.SessionId))
                        .Select(e => new { e.Id, e.SessionId, e.ApplicationId, e.AiVerdict, e.OverallScore }));
                var evalBySession = allEvals
                    .GroupBy(e => e.SessionId)
                    .ToDictionary(g => g.Key, g => g.First());
                var evalIds = allEvals.Select(e => e.Id).ToList();
                var allReviews = (await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId))).ToList();
                var reviewByEval = allReviews
                    .GroupBy(r => r.EvaluationId)
                    .ToDictionary(g => g.Key, g => g.First());

                var nowUtc = DateTimeOffset.UtcNow;

                // Mã phỏng vấn On-site còn hiệu lực (chưa dùng, chưa hết hạn) — để hiển thị thẻ "Cần hành động".
                var activeCodes = appIds.Count > 0
                    ? (await _unitOfWork.Repository<InterviewCode>()
                        .FindAsync(c => appIds.Contains(c.ApplicationId) && c.UsedAt == null && c.ExpiresAt > nowUtc)).ToList()
                    : new List<InterviewCode>();

                // Lịch phỏng vấn sắp tới (booking đã đặt + slot tương ứng).
                var bookings = appIds.Count > 0
                    ? (await _unitOfWork.Repository<InterviewBooking>()
                        .FindAsync(b => appIds.Contains(b.ApplicationId) && b.Status == "scheduled")).ToList()
                    : new List<InterviewBooking>();
                var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
                var slots = slotIds.Any()
                    ? (await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id))).ToList()
                    : new List<AvailabilitySlot>();
                var slotById = slots
                    .GroupBy(s => s.Id)
                    .ToDictionary(g => g.Key, g => g.First());

                // Cấu hình vòng của các job liên quan — dùng cho cả tổng số vòng (mốc "Đạt", ADR-053)
                // lẫn LOẠI vòng (vòng trắc nghiệm không có phỏng vấn thử).
                var roundConfigRows = (await _unitOfWork.Repository<InterviewRoundConfig>()
                        .QueryAsync(q => q.Where(r => jobIds.Contains(r.JobPostingId))
                            .Select(r => new { r.JobPostingId, r.RoundNumber, r.RoundType })))
                    .ToList();

                var roundConfigCounts = roundConfigRows
                    .GroupBy(r => r.JobPostingId)
                    .ToDictionary(g => g.Key, g => Math.Max(1, g.Max(x => x.RoundNumber)));

                var roundTypeByJobRound = roundConfigRows
                    .GroupBy(r => (r.JobPostingId, r.RoundNumber))
                    .ToDictionary(g => g.Key, g => g.First().RoundType);

                // Lời mời theo vòng — để xác định "vòng đang hoạt động" (vòng được mời mới nhất).
                var invites = appIds.Count > 0
                    ? (await _unitOfWork.Repository<InterviewInvite>()
                        .FindAsync(i => appIds.Contains(i.ApplicationId))).ToList()
                    : new List<InterviewInvite>();

                bool IsTestRound(Guid jobId, int round) =>
                    roundTypeByJobRound.TryGetValue((jobId, round), out var rt)
                    && ARI.Application.Scheduling.InterviewInviteEmail.IsOnlineTest(rt);

                // Resolve CV storageKey -> URL hiển thị (presigned nếu dùng S3) trước khi project (LINQ sync).
                var cvUrlMap = new Dictionary<Guid, string?>();
                foreach (var a in appsList)
                    cvUrlMap[a.Id] = string.IsNullOrEmpty(a.CvFileUrl) ? a.CvFileUrl : await _fileStorage.GetUrlAsync(a.CvFileUrl);

                var response = appsList
                    .OrderByDescending(a => a.UpdatedAt)
                    .Select(a =>
                    {
                        jobsDict.TryGetValue(a.JobPostingId, out var job);
                        // Chỉ điểm chấm theo bộ tiêu chí (ADR-070) — không hiện điểm AI cũ hay "0" của file không phải CV.
                        int? matchScore = a.CvJdAnalysisId.HasValue
                                          && analysisDict.TryGetValue(a.CvJdAnalysisId.Value, out var an)
                                          && ARI.Application.CvScoring.CvScoreState.IsDisplayable(an.Status, an.RubricDocumentId)
                            ? an.MatchScore
                            : (int?)null;

                        // Vòng đang hoạt động = vòng mới nhất mà hồ sơ được MỜI hoặc đã có LỊCH giữ chỗ.
                        // Lịch phải được tính: qua vòng trắc nghiệm là Recruiter xếp thẳng lịch vòng kế,
                        // không có bước HM chốt nào sinh lời mời vòng sau — thiếu nó thì hồ sơ đã có lịch
                        // vòng 2 vẫn bị coi là đang ở vòng 1.
                        var inviteRounds = invites.Where(i => i.ApplicationId == a.Id).Select(i => i.RoundNumber)
                            .Concat(bookings.Where(b => b.ApplicationId == a.Id).Select(b => b.RoundNumber))
                            .ToList();
                        int activeRound = inviteRounds.Count > 0 ? Math.Max(1, inviteRounds.Max()) : 1;
                        // Phỏng vấn thử theo VÒNG: còn lượt nếu số phiên practice của vòng này chưa chạm
                        // giới hạn (Interview:PracticeAttemptsPerRound, <= 0 = không giới hạn — dev/test)
                        // và chưa làm phỏng vấn thật của vòng này.
                        var maxPractice = _interviewOptions.PracticeAttemptsPerRound;
                        bool practiceUsedForRound = maxPractice > 0
                            && allSessions.Count(s => s.ApplicationId == a.Id && s.SessionType == "practice" && s.RoundNumber == activeRound) >= maxPractice;
                        bool realDoneForRound = allSessions.Any(s => s.ApplicationId == a.Id && s.SessionType == "real" && s.RoundNumber == activeRound);
                        // Vòng trắc nghiệm không hỗ trợ phỏng vấn thử — không hiện lối vào ngay từ đầu.
                        roundTypeByJobRound.TryGetValue((a.JobPostingId, activeRound), out var activeRoundType);
                        bool onlineTestRound = ARI.Application.Scheduling.InterviewInviteEmail.IsOnlineTest(activeRoundType);
                        // Đã LỠ buổi thật của vòng (lịch còn hiệu lực nhưng qua giờ, không có phiên thật)
                        // → coi như trượt vòng đó, phỏng vấn thử không còn nghĩa gì nữa. Vòng trắc nghiệm
                        // không bao giờ "lỡ": nó không có phiên nào, và hết hạn thì hệ thống nộp thay.
                        bool missedActiveRound = !onlineTestRound && bookings.Any(b => b.ApplicationId == a.Id
                            && slotById.ContainsKey(b.AvailabilitySlotId)
                            && slotById[b.AvailabilitySlotId].RoundNumber == activeRound
                            && slotById[b.AvailabilitySlotId].EndTime <= nowUtc
                            && !allSessions.Any(s => s.ApplicationId == a.Id && s.SessionType == "real"
                                && s.RoundNumber == activeRound));

                        // Số vòng THẬT đã được HR xác nhận Đạt — dựng nhãn "Qua vòng N/M" (ADR-053).
                        var totalRounds = roundConfigCounts.TryGetValue(a.JobPostingId, out var tr) ? tr : 1;
                        var passedRounds = allSessions
                            .Where(s => s.ApplicationId == a.Id && s.SessionType != "practice"
                                && evalBySession.TryGetValue(s.Id, out var ev)
                                && reviewByEval.TryGetValue(ev.Id, out var rv) && rv.FinalVerdict == "pass")
                            .Select(s => s.RoundNumber)
                            .Distinct()
                            .Count();

                        bool pendingHrReview = false; // có vòng đã xong + AI đã chấm nhưng HR chưa xác nhận/chia sẻ
                        // Tiến trình vòng CHỈ tính phiên THẬT — buổi thử không được làm ứng viên tưởng
                        // đã qua vòng, cũng không sinh trạng thái "chờ HR xác nhận" (ADR-051).
                        var rounds = allSessions
                            .Where(s => s.ApplicationId == a.Id && s.SessionType != "practice")
                            .OrderBy(s => s.RoundNumber)
                            .Select(s =>
                            {
                                string? verdict = null;
                                decimal? overall = null;
                                bool hasEval = evalBySession.TryGetValue(s.Id, out var ev);
                                bool shared = hasEval && reviewByEval.TryGetValue(ev!.Id, out var rv) && rv.ShareEvaluation;
                                // Chỉ lộ verdict/điểm khi HR đã chia sẻ kết quả vòng đó (giữ nguyên mô hình bảo mật)
                                if (shared)
                                {
                                    verdict = ev!.AiVerdict;
                                    overall = ev.OverallScore;
                                }
                                // Vòng đã hoàn tất + AI đã có đánh giá nhưng HR chưa chia sẻ → đang chờ HR xác nhận
                                if (s.Status == "completed" && hasEval && !shared)
                                    pendingHrReview = true;
                                return new
                                {
                                    s.RoundNumber,
                                    s.RoundType,
                                    s.SessionType,
                                    s.Status,
                                    Verdict = verdict,
                                    OverallScore = overall
                                };
                            })
                            .ToList();

                        // Mã phỏng vấn còn hiệu lực mới nhất (theo vòng) — CHỈ của vòng làm từ nhà.
                        // Vòng tại văn phòng thì Recruiter đưa mã tận tay khi ứng viên đã tới; hiện nó
                        // ở đây là cho vào phòng từ bất cứ đâu (InterviewCodeRules.ShownToCandidate).
                        var code = activeCodes
                            .Where(c => c.ApplicationId == a.Id
                                        && InterviewCodeRules.ShownToCandidate(roundConfigRows
                                            .FirstOrDefault(r => r.JobPostingId == a.JobPostingId
                                                                 && r.RoundNumber == c.RoundNumber)?.RoundType))
                            .OrderByDescending(c => c.RoundNumber)
                            .ThenByDescending(c => c.CreatedAt)
                            .FirstOrDefault();

                        // Phản hồi của HR được chia sẻ cho ứng viên (nếu có).
                        var sharedFeedback = allReviews
                            .Where(r => r.ShareFeedback && !string.IsNullOrWhiteSpace(r.CandidateFeedback)
                                && allEvals.Any(e => e.Id == r.EvaluationId && e.ApplicationId == a.Id))
                            .OrderByDescending(r => r.UpdatedAt)
                            .FirstOrDefault();

                        // Lịch phỏng vấn sắp tới gần nhất.
                        var upcoming = bookings
                            .Where(b => b.ApplicationId == a.Id && slotById.ContainsKey(b.AvailabilitySlotId)
                                && slotById[b.AvailabilitySlotId].StartTime > nowUtc)
                            .OrderBy(b => slotById[b.AvailabilitySlotId].StartTime)
                            .Select(b => slotById[b.AvailabilitySlotId])
                            .FirstOrDefault();

                        // Lịch đã qua giờ mà vòng đó chưa hề có phiên phỏng vấn THẬT → quá hạn.
                        // Ứng viên cần liên hệ nhân sự xếp lại thay vì thấy mãi "đã xếp lịch".
                        //
                        // BỎ vòng trắc nghiệm: vòng đó không bao giờ sinh phiên phỏng vấn, nên phép kiểm
                        // này đánh dấu "lỡ buổi" cho CẢ người đã nộp bài — ứng viên đọc "Bạn đã lỡ buổi
                        // phỏng vấn vòng 1… hồ sơ dừng lại" ngay trên thẻ hồ sơ đang ở vòng 2.
                        var missed = bookings
                            .Where(b => b.ApplicationId == a.Id && slotById.ContainsKey(b.AvailabilitySlotId)
                                && !IsTestRound(a.JobPostingId, slotById[b.AvailabilitySlotId].RoundNumber)
                                && slotById[b.AvailabilitySlotId].EndTime <= nowUtc
                                && !allSessions.Any(s => s.ApplicationId == a.Id && s.SessionType == "real"
                                    && s.RoundNumber == slotById[b.AvailabilitySlotId].RoundNumber))
                            .OrderByDescending(b => slotById[b.AvailabilitySlotId].StartTime)
                            .Select(b => slotById[b.AvailabilitySlotId])
                            .FirstOrDefault();

                        return new
                        {
                            a.Id,
                            a.JobPostingId,
                            JobTitle = job?.Title,
                            Location = job?.Location,
                            Department = job?.Department,
                            InterviewMode = job?.InterviewMode,
                            a.CandidateEmail,
                            a.CandidateName,
                            a.Status,
                            MatchScore = matchScore,
                            CvFileUrl = cvUrlMap[a.Id],
                            a.PracticeSessionUsed,
                            // ADR-038: phỏng vấn thử mở cho ứng viên ĐÃ QUA vòng CV, tính theo TỪNG VÒNG
                            // (1 lượt/vòng) — vòng kế mở lại thử khi được mời lên vòng đó.
                            PracticeAvailable = PortalSupport.PracticeEligible(a.Status) && !practiceUsedForRound
                                && !realDoneForRound && !onlineTestRound && !missedActiveRound,
                            ActiveRound = activeRound,
                            TotalRounds = totalRounds,
                            PassedRounds = passedRounds,
                            PendingHrReview = pendingHrReview,
                            HrFeedback = sharedFeedback?.CandidateFeedback,
                            a.Source,
                            a.CreatedAt,
                            a.UpdatedAt,
                            Rounds = rounds,
                            InterviewCode = code == null ? null : new { code.Code, code.ExpiresAt, code.RoundNumber },
                            // Kèm LOẠI vòng để giao diện gọi đúng tên: vòng trắc nghiệm là "bài trắc nghiệm",
                            // không phải "phỏng vấn".
                            UpcomingInterview = upcoming == null ? null : new
                            {
                                upcoming.StartTime,
                                upcoming.Timezone,
                                RoundNumber = upcoming.RoundNumber,
                                RoundType = roundTypeByJobRound.TryGetValue((a.JobPostingId, upcoming.RoundNumber), out var urt) ? urt : null,
                            },
                            MissedInterview = missed == null ? null : new
                            {
                                missed.StartTime,
                                missed.Timezone,
                                RoundNumber = missed.RoundNumber
                            }
                        };
                    })
                    .ToList();

                return Result.Success<object>(response);
            }
            catch (Exception ex)
            {
                return Result.Failure<object>($"Lỗi tải hồ sơ: {ex.Message}");
            }
        }
    }

    // ============================================================
    // GET /api/portal/applications/{id} — chi tiết + sessions + kết quả share (IDOR)
    // ============================================================

    public record GetMyApplicationDetailQuery(Guid Id, Guid CandidateAccountId, string? Email) : IRequest<Result<object>>;

    public class GetMyApplicationDetailQueryHandler : IRequestHandler<GetMyApplicationDetailQuery, Result<object>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;

        public GetMyApplicationDetailQueryHandler(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
        }

        public async Task<Result<object>> Handle(GetMyApplicationDetailQuery request, CancellationToken ct)
        {
            var (id, candidateAccountId, emailClaim) = (request.Id, request.CandidateAccountId, request.Email);

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(id);
            if (app == null)
                return Result.Failure<object>("Không tìm thấy hồ sơ ứng tuyển.", CommonErrorCodes.NotFound);

            // IDOR Protection + Auto-link
            if (!await PortalSupport.TryEnsureOwnerAsync(app, candidateAccountId, emailClaim, _unitOfWork))
                return Result.Failure<object>("Forbidden", CommonErrorCodes.Forbidden);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId);

            // Round configs của job (nếu có)
            var roundConfigs = (await _unitOfWork.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == app.JobPostingId))
                .OrderBy(r => r.RoundNumber)
                .ToList();

            var sessionsResult = await _unitOfWork.Repository<InterviewSession>().FindAsync(s => s.ApplicationId == id);
            var allSessions = sessionsResult.OrderBy(s => s.RoundNumber).ToList();

            // Tiến trình các vòng chỉ tính phiên THẬT — phiên thử cùng vòng từng che trạng thái phiên thật.
            // Phiên thử trả riêng ở PracticeSessions (lối vào trang xem lại — ADR-051).
            var sessions = allSessions.Where(s => s.SessionType != "practice").ToList();
            var practiceSessions = allSessions
                .Where(s => s.SessionType == "practice")
                .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                .ToList();

            var invites = (await _unitOfWork.Repository<InterviewInvite>()
                .FindAsync(i => i.ApplicationId == id)).ToList();

            // Optimize query: Fetch all evaluations and HR reviews in batch
            var sessionIds = allSessions.Select(s => s.Id).ToList();
            var evaluations = await _unitOfWork.Repository<Evaluation>().FindAsync(e => sessionIds.Contains(e.SessionId));
            var evalDict = evaluations.GroupBy(e => e.SessionId).ToDictionary(g => g.Key, g => g.First());

            var evalIds = evaluations.Select(e => e.Id).ToList();
            var reviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId));
            var reviewDict = reviews.GroupBy(r => r.EvaluationId).ToDictionary(g => g.Key, g => g.First());

            // Lịch phỏng vấn sắp tới (booking đã đặt) cho hồ sơ này
            var nowUtc = DateTimeOffset.UtcNow;
            var bookings = (await _unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => b.ApplicationId == id)).ToList();
            var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slots = slotIds.Any()
                ? (await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id))).ToList()
                : new List<AvailabilitySlot>();
            var slotById = slots.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());

            var upcoming = bookings
                .Where(b => b.Status == "scheduled" && slotById.ContainsKey(b.AvailabilitySlotId) && slotById[b.AvailabilitySlotId].StartTime > nowUtc)
                .OrderBy(b => slotById[b.AvailabilitySlotId].StartTime)
                .Select(b => slotById[b.AvailabilitySlotId])
                .FirstOrDefault();

            // Mã phỏng vấn còn hiệu lực — chỉ của vòng làm từ nhà (xem InterviewCodeRules.ShownToCandidate).
            var activeCode = (await _unitOfWork.Repository<InterviewCode>()
                .FindAsync(c => c.ApplicationId == id && c.UsedAt == null && c.ExpiresAt > nowUtc))
                .Where(c => InterviewCodeRules.ShownToCandidate(
                    roundConfigs.FirstOrDefault(r => r.RoundNumber == c.RoundNumber)?.RoundType))
                .OrderByDescending(c => c.RoundNumber).ThenByDescending(c => c.CreatedAt)
                .FirstOrDefault();

            // Bài trắc nghiệm đã nộp — KẾT QUẢ của vòng trắc nghiệm (vòng đó không có phiên phỏng vấn).
            var testSubmissions = (await _unitOfWork.Repository<OnlineTestSubmission>()
                .FindAsync(x => x.ApplicationId == id)).ToList();

            // Tổng hợp các roundNumber của job
            var roundNumbers = new SortedSet<int>();
            if (roundConfigs.Any())
            {
                foreach (var rc in roundConfigs) roundNumbers.Add(rc.RoundNumber);
            }
            foreach (var s in sessions) roundNumbers.Add(s.RoundNumber);
            foreach (var i in invites) roundNumbers.Add(i.RoundNumber);
            foreach (var b in bookings)
            {
                if (slotById.TryGetValue(b.AvailabilitySlotId, out var sl))
                    roundNumbers.Add(sl.RoundNumber);
            }
            if (!roundNumbers.Any()) roundNumbers.Add(1);

            var sessionDetails = new List<object>();
            foreach (var rNum in roundNumbers)
            {
                var s = sessions.FirstOrDefault(x => x.RoundNumber == rNum);
                var rc = roundConfigs.FirstOrDefault(x => x.RoundNumber == rNum);
                var inv = invites.FirstOrDefault(x => x.RoundNumber == rNum);
                // Ưu tiên lịch ĐANG GIỮ CHỖ: một vòng có thể còn dòng cũ đã từ chối nằm trước dòng
                // hiện hành, và đọc nhầm dòng cũ là hiện sai cả giờ hẹn lẫn trạng thái.
                var bk = bookings
                    .Where(x => slotById.TryGetValue(x.AvailabilitySlotId, out var sl) && sl.RoundNumber == rNum)
                    .OrderByDescending(x => x.Status == "scheduled")
                    .ThenByDescending(x => x.CreatedAt)
                    .FirstOrDefault();
                var slot = bk != null && slotById.TryGetValue(bk.AvailabilitySlotId, out var sl2) ? sl2 : null;

                object? evalData = null;
                string? recordingUrl = null;
                bool transcriptShared = false;
                string? hrFeedback = null;
                string? hrFinalVerdict = null;
                bool pendingHrReview = false;

                string status;
                DateTimeOffset? scheduledAt = slot?.StartTime;

                var isTestRound = ARI.Application.Scheduling.InterviewInviteEmail.IsOnlineTest(rc?.RoundType);
                var testSub = isTestRound ? testSubmissions.FirstOrDefault(x => x.RoundNumber == rNum) : null;

                if (s != null)
                {
                    status = s.Status;
                    if (evalDict.TryGetValue(s.Id, out var evaluation))
                    {
                        reviewDict.TryGetValue(evaluation.Id, out var review);
                        bool sharedEval = review != null && review.ShareEvaluation;

                        // Vòng đã hoàn tất + AI đã chấm nhưng HR chưa chia sẻ → đang chờ HR xác nhận.
                        if (s.Status == "completed" && !sharedEval)
                            pendingHrReview = true;

                        if (review != null)
                        {
                            hrFinalVerdict = sharedEval ? review.FinalVerdict : null;
                            transcriptShared = review.ShareTranscript;
                            if (review.ShareRecording && !string.IsNullOrEmpty(s.RecordingUrl))
                                recordingUrl = await _fileStorage.GetUrlAsync(s.RecordingUrl);
                            if (review.ShareFeedback && !string.IsNullOrWhiteSpace(review.CandidateFeedback))
                                hrFeedback = review.CandidateFeedback;
                        }

                        if (sharedEval)
                        {
                            evalData = new
                            {
                                evaluation.Id,
                                evaluation.RoundNumber,
                                evaluation.AiVerdict,
                                evaluation.OverallScore,
                                evaluation.Reasoning,
                                evaluation.RecommendedNextStep,
                                CriterionScores = PortalSupport.ParseCriterionScores(evaluation.CriterionScores),
                                QuestionAnalyses = PortalSupport.ParseQuestionAnalyses(evaluation.QuestionAnalyses),
                                LanguageAssessment = PortalSupport.ParseLanguageAssessment(evaluation.LanguageAssessment)
                            };
                        }
                    }
                }
                else if (testSub != null)
                {
                    // Vòng trắc nghiệm: BÀI ĐÃ NỘP là kết quả của vòng. Trước đây vòng này rơi xuống
                    // nhánh dưới — tìm phiên phỏng vấn, không thấy (vòng thi không bao giờ có) — nên
                    // người đã nộp bài vẫn bị gắn nhãn "Quá hạn".
                    status = OnlineTestSubmittedBy.IsSystem(testSub.SubmittedBy) ? "expired" : "completed";
                }
                else if (slot != null && bk?.Status == "scheduled")
                {
                    // Hết giờ hẹn mà không có phiên phỏng vấn thật nào của vòng → quá hạn, không
                    // để hiển thị mãi "đã xếp lịch" (ứng viên cần liên hệ nhân sự xếp lại).
                    // Vòng trắc nghiệm thì không "quá hạn" mà HẾT HẠN — khi bài đóng (giờ hẹn + thời
                    // lượng bài, ADR-072).
                    status = isTestRound
                        ? (nowUtc >= ARI.Application.OnlineTest.OnlineTestWindow.ClosesAt(
                                slot.StartTime, ARI.Application.OnlineTest.OnlineTestWindow.DurationOf(rc))
                            ? "expired" : "scheduled")
                        : (slot.EndTime <= nowUtc ? "missed" : "scheduled");
                }
                else if (inv != null)
                {
                    status = "invited";
                }
                else
                {
                    status = "not_started";
                }

                sessionDetails.Add(new
                {
                    Id = s?.Id.ToString() ?? $"virtual_round_{app.Id}_{rNum}",
                    RoundNumber = rNum,
                    RoundType = s?.RoundType ?? rc?.RoundType ?? "screening",
                    SessionType = s?.SessionType ?? "real",
                    Status = status,
                    ScheduledAt = scheduledAt,
                    StartedAt = s?.StartedAt,
                    EndedAt = s?.EndedAt,
                    DurationSeconds = s?.DurationSeconds,
                    RecordingUrl = recordingUrl,
                    TranscriptShared = transcriptShared,
                    PendingHrReview = pendingHrReview,
                    HrFeedback = hrFeedback,
                    HrFinalVerdict = hrFinalVerdict,
                    Evaluation = evalData
                });
            }

            return Result.Success<object>(new
            {
                app.Id,
                app.JobPostingId,
                JobTitle = job?.Title,
                JobDescription = job?.JobDescription,
                Location = job?.Location,
                Department = job?.Department,
                InterviewMode = job?.InterviewMode,
                DetectedLanguage = job?.DetectedLanguage,
                app.CandidateEmail,
                app.CandidateName,
                app.CandidatePhone,
                CvFileUrl = string.IsNullOrEmpty(app.CvFileUrl) ? app.CvFileUrl : await _fileStorage.GetUrlAsync(app.CvFileUrl),
                app.Status,
                app.CreatedAt,
                app.UpdatedAt,
                // Mốc "Đạt" = qua hết mọi vòng cấu hình của job (ADR-053).
                TotalRounds = roundConfigs.Count == 0 ? 1 : Math.Max(1, roundConfigs.Max(r => r.RoundNumber)),
                PassedRounds = sessions
                    .Where(s => evalDict.TryGetValue(s.Id, out var ev)
                        && reviewDict.TryGetValue(ev.Id, out var rv) && rv.FinalVerdict == "pass")
                    .Select(s => s.RoundNumber)
                    .Distinct()
                    .Count(),
                InterviewCode = activeCode == null ? null : new { activeCode.Code, activeCode.ExpiresAt, activeCode.RoundNumber },
                UpcomingInterview = upcoming == null ? null : new { upcoming.StartTime, upcoming.Timezone, RoundNumber = upcoming.RoundNumber },
                Sessions = sessionDetails,
                PracticeSessions = practiceSessions.Select(p => new
                {
                    Id = p.Id.ToString(),
                    p.RoundNumber,
                    p.RoundType,
                    p.Status,
                    p.StartedAt,
                    p.EndedAt,
                    p.DurationSeconds,
                    HasEvaluation = evalDict.ContainsKey(p.Id)
                }).ToList()
            });
        }
    }

    // ============================================================
    // GET /api/portal/evaluations/{sessionId} — chi tiết đánh giá đã share (IDOR)
    // ============================================================

    public record GetMyEvaluationQuery(Guid SessionId, Guid CandidateAccountId, string? Email) : IRequest<Result<object>>;

    public class GetMyEvaluationQueryHandler : IRequestHandler<GetMyEvaluationQuery, Result<object>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetMyEvaluationQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<object>> Handle(GetMyEvaluationQuery request, CancellationToken ct)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(request.SessionId);
            if (session == null)
                return Result.Failure<object>("Không tìm thấy buổi phỏng vấn.", CommonErrorCodes.NotFound);

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId);
            if (app == null)
                return Result.Failure<object>("Không tìm thấy hồ sơ ứng tuyển liên quan.", CommonErrorCodes.NotFound);

            // IDOR Protection + Auto-link
            if (!await PortalSupport.TryEnsureOwnerAsync(app, request.CandidateAccountId, request.Email, _unitOfWork))
                return Result.Failure<object>("Forbidden", CommonErrorCodes.Forbidden);

            var evaluations = await _unitOfWork.Repository<Evaluation>().FindAsync(e => e.SessionId == request.SessionId);
            var evaluation = evaluations.FirstOrDefault();
            if (evaluation == null)
                return Result.Failure<object>("Báo cáo đánh giá chưa được khởi tạo.", CommonErrorCodes.NotFound);

            var reviews = await _unitOfWork.Repository<HrReview>().FindAsync(r => r.EvaluationId == evaluation.Id);
            var review = reviews.FirstOrDefault();
            if (review == null || !review.ShareEvaluation)
                return Result.Failure<object>("Kết quả đánh giá chi tiết chưa được chia sẻ cho vòng phỏng vấn này.");

            return Result.Success<object>(new
            {
                evaluation.Id,
                evaluation.SessionId,
                evaluation.ApplicationId,
                evaluation.RoundNumber,
                evaluation.SessionType,
                evaluation.AiVerdict,
                evaluation.OverallScore,
                evaluation.CriterionScores,
                evaluation.Reasoning,
                evaluation.RecommendedNextStep,
                evaluation.QuestionAnalyses,
                evaluation.LanguageAssessment,
                evaluation.CreatedAt
            });
        }
    }

    // ============================================================
    // POST /api/portal/applications/verify-cv-info — So khớp thông tin liên hệ với CV bằng Code (0 AI token)
    // ============================================================

    public record VerifyCvInfoCommand(
        Guid CandidateId, string CandidateName, string CandidatePhone,
        byte[]? AttachedBytes, string? AttachedFileName) : IRequest<Result<CvContactVerificationResultDto>>;

    public class VerifyCvInfoCommandHandler : IRequestHandler<VerifyCvInfoCommand, Result<CvContactVerificationResultDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IDocumentParserService _documentParserService;

        public VerifyCvInfoCommandHandler(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IDocumentParserService documentParserService)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _documentParserService = documentParserService;
        }

        public async Task<Result<CvContactVerificationResultDto>> Handle(VerifyCvInfoCommand command, CancellationToken ct)
        {
            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(command.CandidateId, ct);
            if (acc == null)
                return Result.Failure<CvContactVerificationResultDto>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.Unauthorized);

            // Nguồn CV: ưu tiên file đính kèm; nếu không có dùng CV hồ sơ.
            byte[] bytes;
            string ext;
            if (command.AttachedBytes is { Length: > 0 })
            {
                bytes = command.AttachedBytes;
                ext = System.IO.Path.GetExtension(command.AttachedFileName ?? string.Empty).ToLowerInvariant();
            }
            else
            {
                if (string.IsNullOrEmpty(acc.ProfileCvUrl))
                    return Result.Failure<CvContactVerificationResultDto>("Bạn cần tải CV lên hồ sơ hoặc đính kèm CV.", "no_cv");
                bytes = await _fileStorage.ReadAllBytesAsync(acc.ProfileCvUrl, ct);
                if (bytes == null || bytes.Length == 0)
                    return Result.Failure<CvContactVerificationResultDto>("Không đọc được file CV trong hồ sơ. Vui lòng tải lại CV.", "cv_unreadable");
                ext = System.IO.Path.GetExtension(acc.ProfileCvFileName ?? acc.ProfileCvUrl).ToLowerInvariant();
            }

            string cvText = string.Empty;
            try
            {
                using var s = new System.IO.MemoryStream(bytes);
                cvText = (await _documentParserService.ParseDocumentAsync(s, ext))?.Replace("\0", string.Empty) ?? string.Empty;
            }
            catch { /* best-effort */ }

            // SO SÁNH BẰNG CODE BÌNH THƯỜNG TRÊN FILE CV (0 AI TOKEN, FAST & FREE)
            return Result.Success(PerformCodeVerification(cvText, command.CandidateName, command.CandidatePhone));
        }

        private static CvContactVerificationResultDto PerformCodeVerification(string cvText, string formName, string formPhone)
        {
            var mismatches = new List<string>();

            if (string.IsNullOrWhiteSpace(cvText))
            {
                // File CV dạng ảnh (scan) hoặc không đọc được văn bản -> Không thể so sánh bằng text, coi như hợp lệ
                return new CvContactVerificationResultDto
                {
                    IsMatch = true,
                    MismatchDetails = null
                };
            }

            var normCvText = RemoveDiacritics(cvText).ToLowerInvariant();
            var normFormName = RemoveDiacritics(formName).ToLowerInvariant().Trim();
            var formPhoneDigits = System.Text.RegularExpressions.Regex.Replace(formPhone ?? string.Empty, @"\D", "");

            // 1. Kiểm tra Họ và tên xuất hiện trong nội dung file CV
            if (!string.IsNullOrWhiteSpace(normFormName))
            {
                var nameParts = normFormName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bool nameMatches = nameParts.Length > 0 && nameParts.All(p => p.Length <= 1 || normCvText.Contains(p));
                if (!nameMatches)
                {
                    mismatches.Add($"• Họ và tên: \"{formName.Trim()}\" không tìm thấy hoặc khác với nội dung trong file CV.");
                }
            }

            // 2. Kiểm tra Số điện thoại xuất hiện trong nội dung file CV
            if (!string.IsNullOrWhiteSpace(formPhoneDigits) && formPhoneDigits.Length >= 8)
            {
                var phoneCore = formPhoneDigits.Length > 9 ? formPhoneDigits.Substring(formPhoneDigits.Length - 9) : formPhoneDigits;
                var digitsCvText = System.Text.RegularExpressions.Regex.Replace(cvText, @"\D", "");

                bool phoneMatches = digitsCvText.Contains(phoneCore);
                if (!phoneMatches)
                {
                    mismatches.Add($"• Số điện thoại: \"{formPhone.Trim()}\" không tìm thấy trong nội dung file CV.");
                }
            }

            if (mismatches.Count > 0)
            {
                return new CvContactVerificationResultDto
                {
                    IsMatch = false,
                    MismatchDetails = string.Join("\n", mismatches)
                };
            }

            return new CvContactVerificationResultDto
            {
                IsMatch = true,
                MismatchDetails = null
            };
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder(capacity: normalizedString.Length);

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder
                .ToString()
                .Normalize(System.Text.NormalizationForm.FormC)
                .Replace('đ', 'd').Replace('Đ', 'D');
        }
    }

    // ============================================================
    // POST /api/portal/applications/{jobPostingId}/apply — nộp hồ sơ qua Job Board
    // ============================================================

    public record PortalApplyOutcome(bool AlreadyApplied, Guid? ExistingApplicationId, ApplicationResponse? Application);

    public record ApplyToJobCommand(
        Guid JobPostingId, Guid CandidateId,
        string CandidateName, string CandidatePhone, string? CoverLetter, string NoticePeriod,
        byte[]? AttachedBytes, string? AttachedFileName) : IRequest<Result<PortalApplyOutcome>>;

    public class ApplyToJobCommandHandler : IRequestHandler<ApplyToJobCommand, Result<PortalApplyOutcome>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly IDocumentParserService _documentParserService;
        private readonly IApplicationService _applicationService;

        public ApplyToJobCommandHandler(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            IDocumentParserService documentParserService,
            IApplicationService applicationService)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _documentParserService = documentParserService;
            _applicationService = applicationService;
        }

        public async Task<Result<PortalApplyOutcome>> Handle(ApplyToJobCommand command, CancellationToken ct)
        {
            var (jobPostingId, candidateId) = (command.JobPostingId, command.CandidateId);

            var acc = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(candidateId, ct);
            if (acc == null)
                return Result.Failure<PortalApplyOutcome>("Không tìm thấy tài khoản ứng viên.", CommonErrorCodes.Unauthorized);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null)
                return Result.Failure<PortalApplyOutcome>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            // Chặn ứng tuyển trùng (đã có hồ sơ chưa rút cho tin này).
            var existing = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .FindAsync(a => a.JobPostingId == jobPostingId && a.CandidateAccountId == candidateId, ct))
                .Where(a => a.Status != "withdrawn")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();
            if (existing != null)
                return Result.Success(new PortalApplyOutcome(true, existing.Id, null));

            // Nguồn CV: ưu tiên file ứng viên đính kèm cho tin này; nếu không có dùng CV hồ sơ.
            byte[] bytes;
            string fileName;
            string ext;
            if (command.AttachedBytes is { Length: > 0 })
            {
                bytes = command.AttachedBytes;
                ext = System.IO.Path.GetExtension(command.AttachedFileName ?? string.Empty).ToLowerInvariant();
                fileName = System.IO.Path.GetFileName(command.AttachedFileName ?? "cv" + ext);
            }
            else
            {
                // Không đính kèm → bắt buộc phải có CV trong hồ sơ.
                if (string.IsNullOrEmpty(acc.ProfileCvUrl))
                    return Result.Failure<PortalApplyOutcome>("Bạn cần tải CV lên hồ sơ hoặc đính kèm CV cho tin này.", "no_cv");
                bytes = await _fileStorage.ReadAllBytesAsync(acc.ProfileCvUrl, ct);
                if (bytes == null || bytes.Length == 0)
                    return Result.Failure<PortalApplyOutcome>("Không đọc được file CV trong hồ sơ. Vui lòng tải lại CV.", "cv_unreadable");
                ext = System.IO.Path.GetExtension(acc.ProfileCvFileName ?? acc.ProfileCvUrl).ToLowerInvariant();
                fileName = string.IsNullOrWhiteSpace(acc.ProfileCvFileName) ? $"cv{ext}" : acc.ProfileCvFileName;
            }

            var mime = ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };

            string cvText = string.Empty;
            try
            {
                using var s = new System.IO.MemoryStream(bytes);
                cvText = (await _documentParserService.ParseDocumentAsync(s, ext))?.Replace("\0", string.Empty) ?? string.Empty;
            }
            catch { /* best-effort: vẫn ứng tuyển dù không parse được text */ }

            // Lưu một BẢN SAO CV riêng cho hồ sơ ứng tuyển (immutable — không phụ thuộc việc
            // ứng viên đổi/xoá CV hồ sơ sau này).
            string cvFileUrl;
            try
            {
                cvFileUrl = await _fileStorage.SaveAsync(bytes, fileName, mime, StorageFolder.Cv);
            }
            catch (Exception ex)
            {
                return Result.Failure<PortalApplyOutcome>($"Không thể lưu CV cho hồ sơ ứng tuyển: {ex.Message}", CommonErrorCodes.ServerError);
            }

            var serviceRequest = new SubmitApplicationRequest
            {
                JobPostingId = jobPostingId,
                CandidateAccountId = candidateId,
                CandidateEmail = acc.Email,
                CandidateName = command.CandidateName.Trim(),
                CandidatePhone = command.CandidatePhone.Trim(),
                CvFileUrl = cvFileUrl,
                CvText = cvText,
                CoverLetter = command.CoverLetter?.Trim(),
                NoticePeriod = command.NoticePeriod.Trim()
            };

            var result = await _applicationService.SubmitApplicationAsync(serviceRequest, "job_board", ct);
            if (result.IsFailure)
            {
                await _fileStorage.DeleteAsync(cvFileUrl); // dọn file nếu tạo hồ sơ thất bại
                return Result.Failure<PortalApplyOutcome>(result.Error);
            }

            return Result.Success(new PortalApplyOutcome(false, null, result.Value));
        }
    }
}
