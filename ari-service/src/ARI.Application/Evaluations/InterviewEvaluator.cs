using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Offers;
using ARI.Application.Playbooks;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ARI.Application.Evaluations
{
    /// <summary>Kết cục của một lượt chấm — mỗi giá trị khớp một <see cref="EvaluationStatuses"/>.</summary>
    public enum EvaluationOutcome
    {
        /// <summary>Không có gì để làm (phiên không tồn tại hoặc chưa đóng).</summary>
        Skipped,
        Done,
        BlockedNoRubric,
        NoAnswers,
        Failed,
    }

    /// <summary>
    /// Sinh báo cáo đánh giá cho MỘT phiên phỏng vấn đã đóng (ADR-073) — cả buổi thật lẫn buổi thử.
    ///
    /// Trước đây phần này nằm trong <c>InterviewService.EndSessionAsync</c> và chạy ngay trong lệnh đóng
    /// phiên: AI lỗi, model không trả điểm, hay tin chưa có bộ tiêu chí đều kết thúc bằng một dòng log và
    /// không báo cáo nào — phiên đã <c>completed</c> nên không có lượt nào quay lại chấm. Nay mọi kết cục
    /// được GHI vào <c>interview_sessions.evaluation_status</c>, hàng đợi nền gọi vào đây, và lượt quét thử
    /// lại những gì còn dang dở.
    ///
    /// Luật chấm giữ nguyên ADR-060/062: AI chỉ chấm từng tiêu chí, điểm tổng và verdict do backend tính.
    /// </summary>
    public class InterviewEvaluator
    {
        /// <summary>Phiên "đang chấm" quá mốc này coi như tiến trình đã chết giữa chừng — đưa lại vào hàng.</summary>
        public static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(10);

        /// <summary>Trọng số điểm nghi vấn theo loại tín hiệu (ADR-054).</summary>
        internal static readonly IReadOnlyDictionary<string, (decimal Weight, string Severity)> CheatSignalWeights =
            new Dictionary<string, (decimal Weight, string Severity)>
            {
                ["fullscreen_exit"] = (8m, "medium"),   // thoát toàn màn hình
                ["tab_hidden"] = (12m, "high"),         // chuyển tab / thu nhỏ cửa sổ
                ["window_blur"] = (5m, "low"),          // click ra ngoài cửa sổ
                ["shortcut_blocked"] = (3m, "low"),     // bấm phím tắt bị chặn
                ["page_unload"] = (15m, "high"),        // đóng/tải lại trang giữa buổi
            };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIProvider _aiProvider;
        private readonly INotificationService _notificationService;
        private readonly ILogger<InterviewEvaluator>? _logger;

        public InterviewEvaluator(
            IUnitOfWork unitOfWork,
            IAIProvider aiProvider,
            INotificationService notificationService,
            ILogger<InterviewEvaluator>? logger = null)
        {
            _unitOfWork = unitOfWork;
            _aiProvider = aiProvider;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Chấm một phiên. Idempotent: phiên đã có báo cáo chỉ được đánh dấu <c>done</c>, không gọi AI lần hai.
        /// Không ném lỗi vì AI — lỗi được ghi thành <c>failed</c> để lượt quét thử lại.
        /// </summary>
        public async Task<EvaluationOutcome> EvaluateSessionAsync(Guid sessionId, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null || !InterviewSessionStatuses.Is(session.Status, InterviewSessionStatuses.Completed))
                return EvaluationOutcome.Skipped;

            if (await _unitOfWork.Repository<Evaluation>().CountAsync(e => e.SessionId == sessionId, ct) > 0)
            {
                await MarkAsync(session, EvaluationStatuses.Done, null, ct);
                return EvaluationOutcome.Done;
            }

            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            var jobPosting = application == null
                ? null
                : await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            if (application == null || jobPosting == null)
            {
                // Dữ liệu gốc đã mất — thử lại cũng vô ích, nên đốt hết lượt thử ngay.
                session.EvaluationAttempts = EvaluationStatuses.MaxAttempts;
                await MarkAsync(session, EvaluationStatuses.Failed, "Không tìm thấy hồ sơ hoặc tin tuyển dụng của phiên.", ct);
                return EvaluationOutcome.Failed;
            }

            var chatHistory = await BuildChatHistoryAsync(sessionId, ct);
            var hasAnswers = chatHistory.Any(qa => !string.IsNullOrWhiteSpace(qa.AnswerText));
            var isReal = session.SessionType == "real";

            if (!hasAnswers)
            {
                // Buổi thử không có câu trả lời thì không có gì để nhận xét. Buổi THẬT thì phải có một bản ghi
                // để Hiring Manager chốt — nếu không, hồ sơ kẹt ở "đang phỏng vấn" mãi (ADR-053: AI không tự
                // đánh trượt; HM vẫn là người quyết định).
                if (!isReal)
                {
                    await MarkAsync(session, EvaluationStatuses.NoAnswers, null, ct);
                    return EvaluationOutcome.NoAnswers;
                }

                var systemReport = await BuildBaseEvaluationAsync(session, application, ct);
                var reportLang = session.ReportLanguage ?? session.InterviewLanguage ?? "vi";
                var vi = reportLang.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
                systemReport.AiVerdict = "not_pass";
                systemReport.OverallScore = null;
                systemReport.CriterionScores = "{}";
                systemReport.QuestionAnalyses = "[]";
                systemReport.Reasoning = vi
                    ? "Ứng viên không trả lời câu hỏi nào trong buổi phỏng vấn nên AI không có căn cứ để chấm theo tiêu chí. Đây là báo cáo do hệ thống ghi."
                    : "The candidate did not answer any question, so the AI had nothing to score against the criteria. This report was recorded by the system.";
                systemReport.RecommendedNextStep = vi
                    ? "Hiring Manager xem lại bản ghi hình (nếu có) rồi chốt kết quả."
                    : "The Hiring Manager should review the recording (if any) and confirm the result.";

                await SaveAndNotifyAsync(session, application, jobPosting, systemReport, ct);
                return EvaluationOutcome.Done;
            }

            var criteria = await InterviewRubricStore.ResolveAsync(_unitOfWork, jobPosting.Id, session.RoundNumber, ct);
            if (criteria.Count == 0)
            {
                // Việc của Hiring Manager, không phải lỗi của AI — không tốn lượt thử. Khai bộ tiêu chí xong là
                // lượt quét (hoặc chính lệnh lưu bộ tiêu chí) tự đưa phiên này vào hàng.
                _logger?.LogWarning(
                    "Phiên {SessionId} (tin {JobPostingId}, vòng {Round}) chờ bộ tiêu chí chấm phỏng vấn.",
                    sessionId, jobPosting.Id, session.RoundNumber);
                await MarkAsync(session, EvaluationStatuses.BlockedNoRubric, null, ct);
                return EvaluationOutcome.BlockedNoRubric;
            }

            session.EvaluationAttempts += 1;
            await MarkAsync(session, EvaluationStatuses.Processing, null, ct);

            try
            {
                var evalCtx = new SessionContext
                {
                    SessionId = sessionId,
                    // RAG service cần hai trường này để truy hồi playbook đúng tin + vòng lúc chấm.
                    JobPostingId = jobPosting.Id,
                    RoundNumber = session.RoundNumber,
                    JobDescription = jobPosting.JobDescription,
                    CandidateCv = application.CvText ?? "",
                    SessionType = session.SessionType,
                    ChatHistory = chatHistory,
                    ScoringRubric = jobPosting.ScoringRubric ?? "{}",
                    Criteria = criteria,
                    Language = session.InterviewLanguage ?? jobPosting.DetectedLanguage,
                    // Báo cáo viết bằng ngôn ngữ ứng viên đang dùng trên web (ADR-051).
                    ReportLanguage = session.ReportLanguage ?? session.InterviewLanguage ?? "vi"
                };

                var evalReport = await _aiProvider.GenerateEvaluationAsync(evalCtx, ct);

                // ĐIỂM VÀ VERDICT LUÔN DO BACKEND TÍNH (ADR-060/062) — không lấy số model tự đưa ra.
                var aiScores = ScoringRubricSupport.ParseScores(evalReport.CriterionScoresJson);
                var computed = ScoringRubric.ComputeOverall(criteria, aiScores);
                if (!computed.HasValue)
                {
                    // Có bộ tiêu chí mà model không chấm nổi tiêu chí nào: lỗi của model, và KHÔNG được cho 0
                    // điểm (thiếu dữ liệu không phải là điểm kém). Ghi lỗi để thử lại.
                    await MarkAsync(session, EvaluationStatuses.Failed,
                        "AI không trả điểm cho tiêu chí nào trong bộ tiêu chí.", ct);
                    return EvaluationOutcome.Failed;
                }

                var evaluation = await BuildBaseEvaluationAsync(session, application, ct);
                evaluation.AiVerdict = computed.Value >= jobPosting.InterviewPassScore ? "pass" : "not_pass";
                evaluation.OverallScore = computed.Value;
                // Ảnh chụp nhãn + trọng số tại thời điểm chấm: sửa bộ tiêu chí về sau không làm báo cáo cũ mất
                // khả năng giải thích điểm của nó ra từ đâu.
                evaluation.CriterionScores = ScoringRubric.SerializeScoreSnapshot(criteria, aiScores);
                evaluation.Reasoning = evalReport.Reasoning;
                evaluation.RecommendedNextStep = evalReport.RecommendedNextStep;
                evaluation.QuestionAnalyses = evalReport.QuestionAnalysesJson;
                evaluation.LanguageAssessment = await AssessLanguageAsync(jobPosting, evalCtx, ct);

                await SaveAndNotifyAsync(session, application, jobPosting, evaluation, ct);
                return EvaluationOutcome.Done;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Tiến trình đang dừng: trả lượt thử lại, để lần khởi động sau chấm tiếp.
                session.EvaluationAttempts = Math.Max(0, session.EvaluationAttempts - 1);
                await MarkAsync(session, EvaluationStatuses.Pending, null, CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Chấm phiên {SessionId} thất bại (lượt {Attempt}).", sessionId, session.EvaluationAttempts);
                await MarkAsync(session, EvaluationStatuses.Failed, Truncate(ex.Message, 500), CancellationToken.None);
                return EvaluationOutcome.Failed;
            }
        }

        /// <summary>
        /// Những phiên cần được đưa (lại) vào hàng chấm: chưa chấm, kẹt ở "đang chấm", lỗi còn lượt thử (đã
        /// qua thời gian giãn), hoặc đang chờ bộ tiêu chí mà tin nay đã khai.
        /// </summary>
        public async Task<List<Guid>> FindDueSessionIdsAsync(int batch, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;
            var rows = await _unitOfWork.Repository<InterviewSession>().QueryAsync(
                q => q.Where(s => s.Status == InterviewSessionStatuses.Completed
                                  && (s.EvaluationStatus == null
                                      || (s.EvaluationStatus != EvaluationStatuses.Done
                                          && s.EvaluationStatus != EvaluationStatuses.NoAnswers)))
                      .Select(s => new
                      {
                          s.Id, s.ApplicationId, s.RoundNumber, s.EvaluationStatus,
                          s.EvaluationAttempts, s.EvaluationUpdatedAt, s.EndedAt,
                      }),
                ct);

            var due = new List<Guid>();
            var blocked = new List<(Guid Id, Guid ApplicationId, int Round)>();
            foreach (var r in rows)
            {
                var since = r.EvaluationUpdatedAt ?? r.EndedAt ?? now;
                switch (r.EvaluationStatus)
                {
                    case null:
                    case EvaluationStatuses.Pending:
                        due.Add(r.Id);
                        break;
                    case EvaluationStatuses.Processing when now - since > ProcessingTimeout:
                        due.Add(r.Id);
                        break;
                    case EvaluationStatuses.Failed when r.EvaluationAttempts < EvaluationStatuses.MaxAttempts
                                                         && now - since >= RetryDelay(r.EvaluationAttempts):
                        due.Add(r.Id);
                        break;
                    case EvaluationStatuses.BlockedNoRubric:
                        blocked.Add((r.Id, r.ApplicationId, r.RoundNumber));
                        break;
                }
            }

            if (blocked.Count > 0)
            {
                var appIds = blocked.Select(b => b.ApplicationId).Distinct().ToList();
                var jobByApp = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>().QueryAsync(
                        q => q.Where(a => appIds.Contains(a.Id)).Select(a => new { a.Id, a.JobPostingId }), ct))
                    .ToDictionary(a => a.Id, a => a.JobPostingId);
                var jobIds = jobByApp.Values.Distinct().ToList();
                var docs = (await _unitOfWork.Repository<PlaybookDocument>().FindAsync(
                        p => p.DeletedAt == null
                             && (p.Scope == PlaybookScope.ScopeJobPosting || p.Scope == PlaybookScope.ScopeRound)
                             && p.DocumentType == ScoringRubric.TypeInterviewRubric
                             && p.RubricJson != null
                             && p.ScopeRefId != null
                             && jobIds.Contains(p.ScopeRefId!.Value), ct))
                    .GroupBy(p => p.ScopeRefId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var b in blocked)
                {
                    if (jobByApp.TryGetValue(b.ApplicationId, out var jobId)
                        && docs.TryGetValue(jobId, out var jobDocs)
                        && InterviewRubricStore.Pick(jobDocs, b.Round) != null)
                        due.Add(b.Id);
                }
            }

            return due.Take(batch).ToList();
        }

        /// <summary>Đưa lại vào trạng thái chờ chấm mọi phiên của tin đang kẹt vì thiếu bộ tiêu chí (sau khi HM lưu bộ mới).</summary>
        public async Task<List<Guid>> ReleaseBlockedSessionsAsync(Guid jobPostingId, CancellationToken ct = default)
        {
            var appIds = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().QueryAsync(
                q => q.Where(a => a.JobPostingId == jobPostingId).Select(a => a.Id), ct);
            if (appIds.Count == 0) return new List<Guid>();

            var sessions = (await _unitOfWork.Repository<InterviewSession>().FindAsync(
                    s => appIds.Contains(s.ApplicationId)
                         && s.EvaluationStatus == EvaluationStatuses.BlockedNoRubric, ct))
                .ToList();
            if (sessions.Count == 0) return new List<Guid>();

            var now = DateTimeOffset.UtcNow;
            foreach (var s in sessions)
            {
                s.EvaluationStatus = EvaluationStatuses.Pending;
                s.EvaluationUpdatedAt = now;
                s.UpdatedAt = now;
                _unitOfWork.Repository<InterviewSession>().Update(s);
            }
            await _unitOfWork.SaveChangesAsync(ct);
            return sessions.Select(s => s.Id).ToList();
        }

        /// <summary>
        /// Nhắc Hiring Manager chính của các tin đang tuyển mà còn vòng phỏng vấn chưa có bộ tiêu chí. Có buổi đang
        /// chờ chấm thì nhắc mỗi ngày một lần (kèm số buổi); chưa có thì chỉ nhắc một lần để chuẩn bị trước.
        /// </summary>
        public async Task NotifyMissingRubricAsync(CancellationToken ct = default)
        {
            var activeJobs = await _unitOfWork.Repository<JobPosting>().QueryAsync(
                q => q.Where(j => j.Status == "active").Select(j => new { j.Id, j.Title }), ct);
            if (activeJobs.Count == 0) return;

            var added = false;
            var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
            foreach (var job in activeJobs)
            {
                var missing = await InterviewRubricStore.MissingRoundsAsync(_unitOfWork, job.Id, ct);
                if (missing.Count == 0) continue;

                var hm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, job.Id, ct);
                if (hm == null) continue;

                var appIds = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().QueryAsync(
                    q => q.Where(a => a.JobPostingId == job.Id).Select(a => a.Id), ct);
                var waiting = appIds.Count == 0 ? 0 : await _unitOfWork.Repository<InterviewSession>().CountAsync(
                    s => appIds.Contains(s.ApplicationId) && s.EvaluationStatus == EvaluationStatuses.BlockedNoRubric, ct);

                var rounds = string.Join(", ", missing);
                var (dedupKey, body) = waiting > 0
                    ? ($"interview_rubric_missing:{job.Id}:{today}",
                       $"Tin \"{job.Title}\" có {waiting} buổi phỏng vấn đang chờ chấm vì vòng {rounds} chưa có bộ tiêu chí. Khai bộ tiêu chí ở màn tin là báo cáo tự sinh.")
                    : ($"interview_rubric_missing:{job.Id}",
                       $"Tin \"{job.Title}\" chưa có bộ tiêu chí chấm phỏng vấn cho vòng {rounds}. Buổi phỏng vấn của vòng đó sẽ không có báo cáo cho tới khi bạn khai.");

                await OfferSupport.NotifyStaffAsync(_unitOfWork, hm.UserId, "pending",
                    "Tin cần bộ tiêu chí chấm phỏng vấn", body,
                    await StaffLinks.JobAsync(_unitOfWork, hm.UserId, job.Id, ct), dedupKey, ct);
                added = true;
            }

            if (added) await _unitOfWork.SaveChangesAsync(ct);
        }

        /// <summary>Giãn nhịp thử lại: 2, 4, 8… phút theo số lượt đã thử.</summary>
        public static TimeSpan RetryDelay(int attempts) => TimeSpan.FromMinutes(Math.Pow(2, Math.Clamp(attempts, 1, 6)));

        // ------------------------------------------------------------------ helpers

        private async Task<List<QuestionAnswerDto>> BuildChatHistoryAsync(Guid sessionId, CancellationToken ct)
        {
            var questions = await _unitOfWork.Repository<Question>().FindAsync(q => q.SessionId == sessionId, ct);
            var answers = await _unitOfWork.Repository<Answer>().FindAsync(a => a.SessionId == sessionId, ct);
            var answerByQuestion = answers
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.CreatedAt).First());

            return questions
                .OrderBy(q => q.SequenceNumber)
                .Select(q => new QuestionAnswerDto
                {
                    SequenceNumber = q.SequenceNumber,
                    QuestionText = q.QuestionText,
                    AnswerText = answerByQuestion.TryGetValue(q.Id, out var a) ? a.Transcript ?? "" : ""
                })
                .ToList();
        }

        /// <summary>Phần chung của mọi báo cáo: khoá + điểm nghi vấn gian lận (ADR-054).</summary>
        private async Task<Evaluation> BuildBaseEvaluationAsync(
            InterviewSession session, ARI.Domain.Entities.Application application, CancellationToken ct)
        {
            var signals = (await _unitOfWork.Repository<CheatDetectionSignal>()
                .FindAsync(s => s.SessionId == session.Id, ct)).ToList();
            decimal cheatScore = 0;
            foreach (var s in signals)
                cheatScore += CheatSignalWeights.TryGetValue(s.SignalType, out var w) ? w.Weight : 5m;
            cheatScore = Math.Min(100m, cheatScore);

            // Gộp theo loại để HR đọc nhanh: "Thoát toàn màn hình × 3".
            var cheatSignalsJson = JsonSerializer.Serialize(
                signals.GroupBy(s => s.SignalType).Select(g => new
                {
                    type = g.Key,
                    severity = CheatSignalWeights.TryGetValue(g.Key, out var w) ? w.Severity : "low",
                    description = $"{g.Count()} lần",
                    timestamp = g.Max(x => x.RecordedAt)
                }));

            return new Evaluation
            {
                SessionId = session.Id,
                ApplicationId = application.Id,
                RoundNumber = session.RoundNumber,
                SessionType = session.SessionType,
                CheatScore = cheatScore,
                CheatSignals = cheatSignalsJson,
            };
        }

        /// <summary>
        /// Đánh giá ngôn ngữ — phần PHỤ của báo cáo: lỗi thì báo cáo vẫn được ghi, chỉ thiếu mục này. Trước đây
        /// lỗi ở đây làm hỏng cả báo cáo đã chấm xong điểm tiêu chí.
        /// </summary>
        private async Task<string?> AssessLanguageAsync(JobPosting jobPosting, SessionContext evalCtx, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(jobPosting.DetectedLanguage)) return null;
            try
            {
                var la = await _aiProvider.AssessLanguageProficiencyAsync(evalCtx, ct);
                return JsonSerializer.Serialize(new
                {
                    language = evalCtx.Language,
                    fluency = la.Fluency,
                    grammar = la.Grammar,
                    vocabulary = la.Vocabulary,
                    comprehension = la.Comprehension,
                    overall_score = la.OverallScore,
                    cefr_level = la.CefrLevel,
                    language_adherence = la.LanguageAdherence,
                    evidence = la.Evidence
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Đánh giá ngôn ngữ phiên {SessionId} lỗi — báo cáo được ghi không kèm mục này.", evalCtx.SessionId);
                return null;
            }
        }

        private async Task SaveAndNotifyAsync(
            InterviewSession session, ARI.Domain.Entities.Application application, JobPosting jobPosting,
            Evaluation evaluation, CancellationToken ct)
        {
            await _unitOfWork.Repository<Evaluation>().AddAsync(evaluation, ct);
            session.EvaluationStatus = EvaluationStatuses.Done;
            session.EvaluationError = null;
            session.EvaluationUpdatedAt = DateTimeOffset.UtcNow;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<InterviewSession>().Update(session);

            // AI KHÔNG tự đổi trạng thái hồ sơ (ADR-053): hồ sơ giữ nguyên "interview" cho tới khi HM chốt.
            await _unitOfWork.SaveChangesAsync(ct);

            // Buổi thử là không gian riêng của ứng viên — không báo nhân sự (ADR-051).
            if (session.SessionType != "real") return;

            await _notificationService.PublishGroupEventAsync("hr_admin", "ReceiveSystemEvent", new
            {
                Type = "AiEvaluationComplete",
                EvaluationId = evaluation.Id,
                ApplicationId = application.Id
            }, ct);

            // Người CHỐT kết quả là Hiring Manager (ADR-061) — thông báo lưu lại, idempotent theo báo cáo.
            var hm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, application.JobPostingId, ct);
            if (hm != null)
            {
                await OfferSupport.NotifyStaffAsync(_unitOfWork, hm.UserId, "pending",
                    "Có kết quả phỏng vấn chờ bạn chốt",
                    $"Ứng viên {application.CandidateName} — vị trí \"{jobPosting.Title}\", vòng {session.RoundNumber}.",
                    "/hm/evaluations", $"hm_evaluation_ready:{evaluation.Id}", ct);
                await _unitOfWork.SaveChangesAsync(ct);
                await _notificationService.PublishUserEventAsync(hm.UserId, "ReceiveUserNotification",
                    new { Type = "AiEvaluationComplete", EvaluationId = evaluation.Id }, ct);
            }
        }

        private async Task MarkAsync(InterviewSession session, string status, string? error, CancellationToken ct)
        {
            session.EvaluationStatus = status;
            session.EvaluationError = error;
            session.EvaluationUpdatedAt = DateTimeOffset.UtcNow;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<InterviewSession>().Update(session);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        private static string Truncate(string? value, int max)
            => string.IsNullOrEmpty(value) ? string.Empty : (value.Length > max ? value[..max] : value);
    }
}
