using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Việc chấm CV của HỒ SƠ (ADR-070), do hosted service gọi: tìm hồ sơ còn thiếu điểm theo bộ tiêu chí
    /// hiện hành, chấm từng hồ sơ, gắn kết quả, và nhắc Hiring Manager khi tin chưa có bộ tiêu chí.
    ///
    /// "Còn thiếu điểm" SUY RA từ dữ liệu — hồ sơ chưa trỏ tới bản chấm nào, hoặc trỏ tới bản chấm theo
    /// bộ tiêu chí cũ — chứ không có cột cờ nào phải nhớ bật/tắt. Nhờ vậy lưu bộ tiêu chí mới, service
    /// khởi động lại hay AI tạm lỗi đều được lượt quét kế tiếp tự xử lý.
    /// </summary>
    public class CvApplicationScorer
    {
        private static readonly string[] ScorableExtensions = { ".pdf", ".docx", ".doc" };

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICvScoringService _scoring;
        private readonly IFileStorageService _fileStorage;
        private readonly INotificationService _notifications;
        private readonly CvScoringInFlight _inFlight;
        private readonly ILogger<CvApplicationScorer> _logger;

        public CvApplicationScorer(
            IUnitOfWork unitOfWork,
            ICvScoringService scoring,
            IFileStorageService fileStorage,
            INotificationService notifications,
            CvScoringInFlight inFlight,
            ILogger<CvApplicationScorer> logger)
        {
            _unitOfWork = unitOfWork;
            _scoring = scoring;
            _fileStorage = fileStorage;
            _notifications = notifications;
            _inFlight = inFlight;
            _logger = logger;
        }

        private static string AppKey(Guid applicationId, Guid rubricId) => CvScoringInFlight.ApplicationKey(applicationId, rubricId);

        /// <summary>Hồ sơ cần chấm (lần đầu hoặc chấm lại) của một tin, hoặc của mọi tin khi <paramref name="jobPostingId"/> null.</summary>
        public async Task<List<Guid>> FindStaleApplicationIdsAsync(Guid? jobPostingId, int limit, CancellationToken ct)
        {
            var live = await CvRubricStore.LiveIdsByJobAsync(
                _unitOfWork, jobPostingId is { } one ? new[] { one } : null, ct);
            if (live.Count == 0) return new List<Guid>();

            var candidateJobIds = live.Keys.ToList();
            var jobIds = await _unitOfWork.Repository<JobPosting>().QueryAsync(
                q => q.Where(j => candidateJobIds.Contains(j.Id) && j.Status != "archived").Select(j => j.Id), ct);
            if (jobIds.Count == 0) return new List<Guid>();

            var apps = await _unitOfWork.Repository<Domain.Entities.Application>().QueryAsync(
                q => q.Where(a => jobIds.Contains(a.JobPostingId) && a.CvFileUrl != null && a.CvFileUrl != "")
                      .Select(a => new { a.Id, a.JobPostingId, a.CvJdAnalysisId, a.CvFileUrl }), ct);

            var analysisIds = apps.Where(a => a.CvJdAnalysisId != null).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var analysisRubric = analysisIds.Count == 0
                ? new Dictionary<Guid, Guid?>()
                : (await _unitOfWork.Repository<CvJdAnalysis>().QueryAsync(
                        q => q.Where(x => analysisIds.Contains(x.Id)).Select(x => new { x.Id, x.RubricDocumentId }), ct))
                    .ToDictionary(x => x.Id, x => x.RubricDocumentId);

            return apps
                .Where(a => ScorableExtensions.Contains(Path.GetExtension(a.CvFileUrl!).ToLowerInvariant()))
                .Where(a =>
                {
                    var rubricId = live[a.JobPostingId];
                    if (a.CvJdAnalysisId is { } aid && analysisRubric.TryGetValue(aid, out var used) && used == rubricId)
                        return false;
                    return !_inFlight.ShouldBackOff(AppKey(a.Id, rubricId));
                })
                .Select(a => a.Id)
                .Take(limit)
                .ToList();
        }

        /// <summary>Chấm một hồ sơ theo bộ tiêu chí hiện hành rồi gắn kết quả. Không ném lỗi.</summary>
        public async Task ScoreApplicationAsync(Guid applicationId, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (app == null || string.IsNullOrWhiteSpace(app.CvFileUrl)) return;

            var rubric = await CvRubricStore.LiveAsync(_unitOfWork, app.JobPostingId, ct);
            if (rubric == null) return; // chờ HM khai — lượt quét lo phần nhắc

            CvJdAnalysis? current = null;
            if (app.CvJdAnalysisId is { } currentId)
            {
                current = await _unitOfWork.Repository<CvJdAnalysis>().GetByIdAsync(currentId, ct);
                if (current?.RubricDocumentId == rubric.Id) return; // đã chấm theo bộ hiện hành
            }

            var appKey = AppKey(app.Id, rubric.Id);

            // Đường nhanh (ADR-075): HM chỉ đổi công thức → tính lại từ câu trả lời cũ của AI, không đọc file, không
            // gọi AI. File CV của hồ sơ không bao giờ đổi sau khi nộp, nên mã băm của bản chấm hiện tại là mã của file.
            var scored = current is { CvHash.Length: > 0 }
                ? await _scoring.TryDeriveAsync(app.JobPostingId, current.CvHash, ct)
                : null;

            if (scored == null)
            {
                byte[]? bytes;
                try
                {
                    bytes = await _fileStorage.ReadAllBytesAsync(app.CvFileUrl!, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Không đọc được file CV của hồ sơ {AppId}", app.Id);
                    bytes = null;
                }
                if (bytes is not { Length: > 0 })
                {
                    _inFlight.RecordFailure(appKey, "Không đọc được file CV đã lưu.", CvScoringErrors.CvUnreadable);
                    await PublishApplicationChangedAsync(app, ct);
                    return;
                }

                var result = await _scoring.ScoreAsync(app.JobPostingId, bytes, Path.GetFileName(app.CvFileUrl!), ct);
                if (result.IsFailure)
                {
                    if (result.ErrorCode == CvScoringErrors.RubricRequired) return; // bộ tiêu chí vừa bị thay giữa chừng
                    var failure = _inFlight.RecordFailure(appKey, result.Error!, result.ErrorCode);
                    _logger.LogWarning("Chấm CV hồ sơ {AppId} thất bại (lần {Attempt}, thử lại lúc {RetryAt}): {Error}",
                        app.Id, failure.Attempts, failure.RetryAfter, result.Error);
                    // Lỗi không đổi dòng nào trong DB nên trigger realtime không bắn — phải đẩy tay, nếu không màn
                    // nhân sự đứng mãi ở "Đang chấm CV" dù lượt chấm đã hỏng.
                    await PublishApplicationChangedAsync(app, ct);
                    return;
                }
                scored = result.Value!;
            }

            _inFlight.ClearFailure(appKey);
            app.CvJdAnalysisId = scored.Id;
            _unitOfWork.Repository<Domain.Entities.Application>().Update(app);
            await _unitOfWork.SaveChangesAsync(ct);

            // Bảng applications có trigger realtime (ADR-057) nên các màn đang mở tự tải lại; sự kiện dưới giữ hợp
            // đồng cũ với ứng viên ("đã phân tích xong") và phủ cả trường hợp listener DB đang nối lại.
            try
            {
                if (app.CandidateAccountId is { } accountId)
                    await _notifications.PublishUserEventAsync(accountId, "ReceiveUserNotification",
                        new { Type = "AiAnalysisComplete", JobPostingId = app.JobPostingId }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Gửi sự kiện realtime cho ứng viên sau khi chấm CV thất bại (bỏ qua).");
            }
            await PublishApplicationChangedAsync(app, ct);
        }

        /// <summary>
        /// Báo mọi người đọc được hồ sơ này (chủ tin, đội tuyển dụng, HR) rằng trạng thái điểm CV vừa đổi — đúng
        /// phạm vi mà trigger của bảng <c>applications</c> dùng (ADR-057/061). Không ném lỗi.
        /// </summary>
        private async Task PublishApplicationChangedAsync(Domain.Entities.Application app, CancellationToken ct)
        {
            try
            {
                var payload = new { id = app.Id, jobPostingId = app.JobPostingId };
                var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
                var recipients = await _unitOfWork.Repository<JobHiringTeamMember>().QueryAsync(
                    q => q.Where(m => m.JobPostingId == app.JobPostingId && m.DeletedAt == null).Select(m => m.UserId), ct);
                if (job != null) recipients.Add(job.CreatedByUserId);

                foreach (var userId in recipients.Distinct())
                    await _notifications.PublishUserEventAsync(userId, "ReceiveApplicationStatusUpdate", payload, ct);
                await _notifications.PublishGroupEventAsync("hr_admin", "ReceiveApplicationStatusUpdate", payload, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Gửi sự kiện realtime về điểm CV thất bại (bỏ qua).");
            }
        }

        /// <summary>
        /// Tin đang nhận hồ sơ mà chưa có bộ tiêu chí (tin lập trước ADR-070): hồ sơ nằm chờ, nên Hiring
        /// Manager chính phải được báo — MỘT lần cho mỗi tin.
        /// </summary>
        public async Task NotifyMissingRubricAsync(CancellationToken ct)
        {
            var activeJobs = await _unitOfWork.Repository<JobPosting>().QueryAsync(
                q => q.Where(j => j.Status == "active").Select(j => new { j.Id, j.Title }), ct);
            if (activeJobs.Count == 0) return;

            var live = await CvRubricStore.LiveIdsByJobAsync(_unitOfWork, activeJobs.Select(j => j.Id).ToList(), ct);
            var missing = activeJobs.Where(j => !live.ContainsKey(j.Id)).ToList();
            if (missing.Count == 0) return;

            var missingIds = missing.Select(j => j.Id).ToList();
            var withApps = (await _unitOfWork.Repository<Domain.Entities.Application>().QueryAsync(
                    q => q.Where(a => missingIds.Contains(a.JobPostingId)).Select(a => a.JobPostingId).Distinct(), ct))
                .ToHashSet();

            var added = false;
            foreach (var job in missing.Where(j => withApps.Contains(j.Id)))
            {
                var hm = await JobAccess.PrimaryHiringManagerAsync(_unitOfWork, job.Id, ct);
                if (hm == null) continue;

                var dedupKey = $"cv_rubric_missing:{job.Id}";
                var exists = await _unitOfWork.Repository<Notification>().CountAsync(
                    n => n.RecipientUserId == hm.UserId && n.DedupKey == dedupKey, ct) > 0;
                if (exists) continue;

                await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    RecipientUserId = hm.UserId,
                    Type = "pending",
                    Title = "Tin cần bộ tiêu chí chấm CV",
                    Body = $"Tin \"{job.Title}\" đang có hồ sơ chờ chấm nhưng chưa có bộ tiêu chí. Hãy khai bộ tiêu chí để hệ thống chấm CV.",
                    Link = await StaffLinks.JobAsync(_unitOfWork, hm.UserId, job.Id, ct),
                    DedupKey = dedupKey,
                    IsRead = false,
                }, ct);
                added = true;
            }

            if (added) await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
