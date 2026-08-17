using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Application.Scheduling;
using ARI.Domain.Entities;
using ARI.Domain.Constants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ARI.Application.Services
{
    public class InterviewService : IInterviewService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIProvider _aiProvider;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IAvatarService _avatarService;
        private readonly INotificationService _notificationService;
        private readonly IDeepgramTokenService _deepgramTokenService;
        private readonly IRagIngestionService _ragIngestion;
        private readonly ITTSService _ttsService;
        private readonly IFileStorageService _fileStorage;
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
        private readonly InterviewOptions _interviewOptions;
        private readonly IMemoryCache _cache;
        private readonly ILogger<InterviewService>? _logger;

        // Cache key cho danh sách toàn bộ phiên phỏng vấn (HR view).
        private const string AllSessionsCacheKey = "interview-sessions:all";

        public InterviewService(
            IUnitOfWork unitOfWork,
            IAIProvider aiProvider,
            IEmbeddingProvider embeddingProvider,
            IAvatarService avatarService,
            INotificationService notificationService,
            IDeepgramTokenService deepgramTokenService,
            IRagIngestionService ragIngestion,
            ITTSService ttsService,
            IFileStorageService fileStorage,
            Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
            IMemoryCache cache,
            InterviewOptions? interviewOptions = null,
            ILogger<InterviewService>? logger = null)
        {
            _logger = logger;
            _fileStorage = fileStorage;
            _unitOfWork = unitOfWork;
            _aiProvider = aiProvider;
            _embeddingProvider = embeddingProvider;
            _avatarService = avatarService;
            _notificationService = notificationService;
            _deepgramTokenService = deepgramTokenService;
            _ragIngestion = ragIngestion;
            _ttsService = ttsService;
            _scopeFactory = scopeFactory;
            _cache = cache;
            _interviewOptions = interviewOptions ?? new InterviewOptions();
        }

        /// <summary>
        /// TTS cho 1 câu hỏi (base64 PCM 24k) để FE đẩy vào LiveAvatar repeatAudio (ADR-044).
        /// Xác thực ứng viên sở hữu phiên. Rỗng nếu chưa cấu hình ElevenLabs → FE fallback browser TTS.
        /// </summary>
        public async Task<Result<string>> GetSpeechAudioAsync(
            Guid sessionId, string text, Guid? candidateAccountId, string? candidateEmail, bool kioskAuthorized = false, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Result.Success(string.Empty);

            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<string>("Không tìm thấy phiên phỏng vấn.");
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            if (application == null)
                return Result.Failure<string>("Không tìm thấy hồ sơ ứng tuyển.");
            var owns = kioskAuthorized
                       || (candidateAccountId.HasValue && application.CandidateAccountId == candidateAccountId.Value)
                       || (!string.IsNullOrEmpty(candidateEmail)
                           && string.Equals(application.CandidateEmail, candidateEmail, StringComparison.OrdinalIgnoreCase));
            if (!owns)
                return Result.Failure<string>("Bạn không có quyền truy cập phiên phỏng vấn này.");

            try
            {
                var audio = await _ttsService.TextToSpeechBase64PcmAsync(text, string.Empty, ct);
                return Result.Success(audio);
            }
            catch
            {
                return Result.Success(string.Empty); // FE fallback browser TTS
            }
        }

        /// <summary>
        /// Đảm bảo CV (theo application) + JD (theo job) đã được ingest vào document_chunks để
        /// RAG (hybrid retrieve hoặc in-process) có ngữ cảnh khi sinh câu hỏi. Chỉ ingest khi THIẾU
        /// (idempotent, tránh re-embed mỗi lần vào phòng). An toàn nếu RAG service tạm lỗi (nuốt lỗi).
        /// </summary>
        private async Task EnsureSourcesIngestedAsync(ARI.Domain.Entities.Application application, JobPosting? jobPosting, CancellationToken ct)
        {
            try
            {
                var cvExists = (await _unitOfWork.Repository<DocumentChunk>()
                    .QueryAsync(q => q.Where(c => c.SourceType == "cv" && c.SourceId == application.Id).Select(c => c.Id).Take(1), ct)).Any();
                if (!cvExists && !string.IsNullOrWhiteSpace(application.CvText))
                    await _ragIngestion.IngestAsync("cv", application.Id, application.CvText!, ct: ct);

                if (jobPosting != null && !string.IsNullOrWhiteSpace(jobPosting.JobDescription))
                {
                    var jdExists = (await _unitOfWork.Repository<DocumentChunk>()
                        .QueryAsync(q => q.Where(c => c.SourceType == "jd" && c.SourceId == jobPosting.Id).Select(c => c.Id).Take(1), ct)).Any();
                    if (!jdExists)
                        await _ragIngestion.IngestAsync("jd", jobPosting.Id, jobPosting.JobDescription, ct: ct);
                }
            }
            catch
            {
                // RAG service/embedding tạm lỗi → vẫn vào phỏng vấn (openai path dùng full-text JD/CV trực tiếp).
            }
        }

        /// <summary>
        /// Cấu hình media cho FE vào phòng phỏng vấn: mint token Deepgram (STT) + HeyGen (avatar),
        /// kèm ngôn ngữ + voice. Xác thực ứng viên sở hữu phiên. Token nào chưa cấu hình key → null
        /// (FE tự fallback).
        /// </summary>
        public async Task<Result<PracticeMediaConfigResponse>> GetMediaConfigAsync(
            Guid sessionId, Guid? candidateAccountId, string? candidateEmail, bool kioskAuthorized = false, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<PracticeMediaConfigResponse>("Không tìm thấy phiên phỏng vấn.");

            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            if (application == null)
                return Result.Failure<PracticeMediaConfigResponse>("Không tìm thấy hồ sơ ứng tuyển.");

            // Kiosk: token đã gắn đúng session_id (kiểm ở controller) — máy Kiosk không đăng nhập ứng viên.
            var owns = kioskAuthorized
                       || (candidateAccountId.HasValue && application.CandidateAccountId == candidateAccountId.Value)
                       || (!string.IsNullOrEmpty(candidateEmail)
                           && string.Equals(application.CandidateEmail, candidateEmail, StringComparison.OrdinalIgnoreCase));
            if (!owns)
                return Result.Failure<PracticeMediaConfigResponse>("Bạn không có quyền truy cập phiên phỏng vấn này.");

            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);

            // Token media là BEST-EFFORT: provider lỗi (hết key/sunset/transient) → null để FE
            // fallback mềm (browser TTS / nhập tay), KHÔNG được làm sập cả phòng phỏng vấn.
            DeepgramToken? deepgram = null;
            try { deepgram = await _deepgramTokenService.CreateTemporaryTokenAsync(ct); }
            catch { /* STT tuỳ chọn */ }

            // ADR-050: Practice audio-only — KHÔNG mint avatar (giữ đủ STT/RAG/LLM/ElevenLabs).
            // Tránh cạnh tranh concurrency LiveAvatar với buổi thật + đốt credit không dự đoán.
            // Real luôn có avatar. Cờ PracticeUseAvatar cho phép bật lại khi cần.
            AvatarStreamingToken? avatar = null;
            var useAvatar = session.SessionType != "practice" || _interviewOptions.PracticeUseAvatar;
            if (useAvatar)
            {
                try { avatar = await _avatarService.CreateStreamingTokenAsync(null, jobPosting?.PersonaVoiceId, ct); }
                catch { /* avatar tuỳ chọn — FE fallback WebAudio (ElevenLabs) + bot tĩnh */ }
            }

            // Trần thời lượng để FE vẽ đếm ngược khớp giờ server: practice 20' (ADR-050),
            // real 45' (ADR-052) — cùng cơ chế hết giờ AI nói câu kết rồi đóng phiên.
            var maxMinutes = session.SessionType == "practice"
                ? _interviewOptions.PracticeMaxDurationMinutes
                : _interviewOptions.RealMaxDurationMinutes;
            var maxDurationSeconds = maxMinutes > 0 ? maxMinutes * 60 : 0;

            return Result.Success(new PracticeMediaConfigResponse
            {
                SessionId = session.Id,
                Language = session.InterviewLanguage,
                SessionType = session.SessionType,
                MaxDurationSeconds = maxDurationSeconds,
                StartedAtUtc = session.StartedAt,
                Deepgram = deepgram == null ? null : new DeepgramConfigDto
                {
                    Token = deepgram.AccessToken,
                    ExpiresInSeconds = deepgram.ExpiresInSeconds,
                    Model = deepgram.Model
                },
                HeyGen = avatar == null ? null : new HeyGenConfigDto
                {
                    Token = avatar.Token,
                    ServerUrl = avatar.ServerUrl,
                    AvatarId = avatar.AvatarId,
                    VoiceId = avatar.VoiceId
                }
            });
        }

        /// <summary>
        /// Danh sách phiên phỏng vấn cho HR: join Application + JobPosting + Evaluation mới nhất.
        /// </summary>
        public async Task<List<HrInterviewSessionItem>> GetSessionsForHrAsync(Guid? applicationId = null, CancellationToken ct = default)
        {
            // Cache toàn bộ danh sách (không lọc theo applicationId) để tránh query DB lặp lại.
            // Cache bị invalidate khi có phiên mới bắt đầu hoặc kết thúc.
            bool isAllSessions = !applicationId.HasValue || applicationId.Value == Guid.Empty;
            if (isAllSessions && _cache.TryGetValue(AllSessionsCacheKey, out List<HrInterviewSessionItem>? cached) && cached != null)
                return cached;

            // Phỏng vấn thử là không gian riêng của ứng viên — không lộ cho nhân sự nội bộ (ADR-051).
            var sessions = applicationId.HasValue && applicationId.Value != Guid.Empty
                ? await _unitOfWork.Repository<InterviewSession>().QueryAsync(q => q
                    .Where(s => s.ApplicationId == applicationId.Value && s.SessionType != "practice")
                    .Select(s => new
                    {
                        s.Id, s.ApplicationId, s.RoundNumber, s.RoundType, s.SessionType,
                        s.Status, s.InterviewLanguage, s.DurationSeconds, s.RecordingUrl,
                        s.StartedAt, s.EndedAt, s.CreatedAt
                    }), ct)
                : await _unitOfWork.Repository<InterviewSession>().QueryAsync(q => q
                    .Where(s => s.SessionType != "practice")
                    .Select(s => new
                    {
                        s.Id, s.ApplicationId, s.RoundNumber, s.RoundType, s.SessionType,
                        s.Status, s.InterviewLanguage, s.DurationSeconds, s.RecordingUrl,
                        s.StartedAt, s.EndedAt, s.CreatedAt
                    }), ct);
            if (sessions.Count == 0) return new List<HrInterviewSessionItem>();

            var appIds = sessions.Select(s => s.ApplicationId).Distinct().ToList();

            var appsTask = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                return await uow.Repository<ARI.Domain.Entities.Application>()
                    .QueryAsync(q => q.Where(a => appIds.Contains(a.Id)).Select(a => new { a.Id, a.CandidateName, a.CandidateEmail, a.JobPostingId }), ct);
            });

            var evalsTask = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                return await uow.Repository<Evaluation>()
                    .QueryAsync(q => q
                        .Where(e => appIds.Contains(e.ApplicationId))
                        .Select(e => new { e.Id, e.ApplicationId, e.RoundNumber, e.AiVerdict, e.CreatedAt }), ct);
            });

            await Task.WhenAll(appsTask, evalsTask);

            var apps = await appsTask;
            var evaluations = await evalsTask;

            var appById = apps.ToDictionary(a => a.Id);

            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobTitleById = jobIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id)).Select(j => new { j.Id, j.Title }), ct))
                    .ToDictionary(j => j.Id, j => j.Title);


            var evalByAppRound = evaluations
                .GroupBy(e => (e.ApplicationId, e.RoundNumber))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());

            var result = sessions
                .OrderByDescending(s => s.CreatedAt)
                .Select(s =>
                {
                    appById.TryGetValue(s.ApplicationId, out var app);
                    evalByAppRound.TryGetValue((s.ApplicationId, s.RoundNumber), out var eval);
                    return new HrInterviewSessionItem
                    {
                        Id = s.Id,
                        ApplicationId = s.ApplicationId,
                        CandidateName = app?.CandidateName ?? "—",
                        JobTitle = app != null && jobTitleById.TryGetValue(app.JobPostingId, out var t) ? t : null,
                        RoundNumber = s.RoundNumber,
                        RoundType = s.RoundType,
                        SessionType = s.SessionType,
                        Status = s.Status,
                        InterviewLanguage = s.InterviewLanguage,
                        DurationSeconds = s.DurationSeconds,
                        HasRecording = !string.IsNullOrEmpty(s.RecordingUrl),
                        StartedAt = s.StartedAt,
                        EndedAt = s.EndedAt,
                        CreatedAt = s.CreatedAt,
                        EvaluationId = eval?.Id,
                        Verdict = eval?.AiVerdict,
                    };
                })
                .ToList();

            if (isAllSessions)
            {
                _cache.Set(AllSessionsCacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                });
            }

            return result;
        }

        // ─────────── Interview Management: Job → Slot → Candidate ───────────

        /// <summary>
        /// Lấy danh sách vị trí tuyển dụng nhân sự này được quản lý, kèm thống kê ca phỏng vấn & ứng viên.
        ///
        /// Lọc Ở ĐÂY chứ không ở giao diện: trước đây endpoint trả TOÀN BỘ tin của hệ thống rồi màn
        /// Recruiter tự lọc bằng danh sách hồ sơ của mình — mà nhánh dự phòng của nó lại là "không
        /// lọc được thì hiện hết", nên recruiter chưa có hồ sơ nào thấy được mọi tin. Lọc ở server
        /// cũng bỏ luôn được lời gọi API thứ hai lúc tải trang.
        /// </summary>
        public async Task<List<InterviewJobSummaryDto>> GetInterviewJobsAsync(Guid? userId, string? role, CancellationToken ct = default)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var isAdmin = role == AppRoles.SuperAdmin || role == AppRoles.HrAdmin;

            // 1. Lấy JobPostings trong phạm vi quản lý
            var jobs = await _unitOfWork.Repository<JobPosting>()
                .QueryAsync(q => q.Where(j => isAdmin || (userId != null && j.CreatedByUserId == userId))
                    .OrderByDescending(j => j.CreatedAt)
                    .Select(j => new { j.Id, j.Title, j.Status, j.ApplicationDeadline }), ct);
            if (jobs.Count == 0) return new List<InterviewJobSummaryDto>();

            var jobIds = jobs.Select(j => j.Id).ToList();

            // 2. Lấy tất cả ca (AvailabilitySlots) thuộc các job này
            var slots = await _unitOfWork.Repository<AvailabilitySlot>()
                .QueryAsync(q => q.Where(s => jobIds.Contains(s.JobPostingId))
                    .Select(s => new { s.Id, s.JobPostingId, s.RoundNumber, s.StartTime, s.BookedCount, s.Capacity }), ct);

            var slotIds = slots.Select(s => s.Id).ToList();

            // 3. Bookings
            var bookings = slotIds.Count > 0
                ? await _unitOfWork.Repository<InterviewBooking>()
                    .QueryAsync(q => q.Where(b => slotIds.Contains(b.AvailabilitySlotId))
                        .Select(b => new { b.ApplicationId, b.AvailabilitySlotId, b.Status, b.ConfirmationStatus }), ct)
                : new List<dynamic>() as dynamic;

            // 4. Sessions
            var appIds = (bookings is System.Collections.IEnumerable bksEnum)
                ? bksEnum.Cast<dynamic>().Select(b => (Guid)b.ApplicationId).Distinct().ToList()
                : new List<Guid>();

            var sessions = appIds.Count > 0
                ? await _unitOfWork.Repository<InterviewSession>()
                    .QueryAsync(q => q.Where(s => appIds.Contains(s.ApplicationId))
                        .Select(s => new { s.ApplicationId, s.Status, s.RoundNumber }), ct)
                : new List<dynamic>() as dynamic;

            var slotByJob = slots.GroupBy(s => s.JobPostingId).ToDictionary(g => g.Key, g => g.ToList());
            var bookingList = (bookings is System.Collections.IEnumerable bEnum) ? bEnum.Cast<dynamic>().ToList() : new List<dynamic>();
            var sessionList = (sessions is System.Collections.IEnumerable sEnum) ? sEnum.Cast<dynamic>().ToList() : new List<dynamic>();

            var result = new List<InterviewJobSummaryDto>();
            foreach (var job in jobs)
            {
                var jobSlots = slotByJob.TryGetValue(job.Id, out var sl) ? sl : new();
                var jobSlotIds = jobSlots.Select(s => s.Id).ToHashSet();
                var jobBookings = bookingList.Where(b => jobSlotIds.Contains((Guid)b.AvailabilitySlotId)).ToList();
                var jobAppIds = jobBookings.Select(b => (Guid)b.ApplicationId).Distinct().ToHashSet();

                var sessionsForJob = sessionList.Where(s => jobAppIds.Contains((Guid)s.ApplicationId)).ToList();
                var nextSlot = jobSlots.Where(s => s.StartTime > nowUtc).OrderBy(s => s.StartTime).FirstOrDefault();

                // Tính status thực tế: nếu quá hạn nộp -> "closed"
                var effectiveStatus = (job.Status == "active" && job.ApplicationDeadline.HasValue && job.ApplicationDeadline.Value <= nowUtc)
                    ? "closed"
                    : job.Status;

                result.Add(new InterviewJobSummaryDto
                {
                    JobId = job.Id,
                    JobTitle = job.Title,
                    JobStatus = effectiveStatus,
                    TotalSlots = jobSlots.Count,
                    // Cùng vị từ chiếm chỗ với GetSlotsForJobAsync và migration đối soát. Bản cũ đếm
                    // MỌI dòng booking nên thẻ tổng quan ở đầu trang nói một đằng, phân số từng ca
                    // bên dưới nói một nẻo; và "đã xác nhận" còn đếm cả lịch sau đó đã bị huỷ.
                    TotalBooked = jobBookings.Count(b => (string)b.Status == BookingStatus.Scheduled),
                    TotalConfirmed = jobBookings.Count(b =>
                        (string)b.Status == BookingStatus.Scheduled &&
                        (string)b.ConfirmationStatus == BookingConfirmationStatus.Confirmed),
                    MaxRound = jobSlots.Count > 0 ? jobSlots.Max(s => s.RoundNumber) : 0,
                    TotalSessions = sessionsForJob.Count,
                    CompletedSessions = sessionsForJob.Count(s => (string)s.Status == "completed"),
                    NextSlotTime = nextSlot?.StartTime
                });
            }

            return result
                .OrderByDescending(j => j.NextSlotTime.HasValue)
                .ThenBy(j => j.NextSlotTime)
                .ThenByDescending(j => j.TotalSlots > 0)
                .ThenBy(j => j.JobTitle)
                .ToList();
        }

        /// <summary>Gửi email + notification nhắc lịch phỏng vấn cho ứng viên. Chỉ chủ tin hoặc admin.</summary>
        public async Task<Result<bool>> SendBookingReminderAsync(
            Guid bookingId, Guid? userId, string? role, CancellationToken ct = default)
        {
            var booking = await _unitOfWork.Repository<InterviewBooking>().GetByIdAsync(bookingId, ct);
            if (booking == null) return Result.Failure<bool>("Không tìm thấy lịch phỏng vấn.", CommonErrorCodes.NotFound);

            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(booking.ApplicationId, ct);
            if (app == null) return Result.Failure<bool>("Không tìm thấy hồ sơ ứng viên.", CommonErrorCodes.NotFound);

            var (canManage, _) = await SchedulingSupport.CanManageAsync(_unitOfWork, app.JobPostingId, userId, role, ct);
            if (!canManage)
                return Result.Failure<bool>("Bạn không có quyền nhắc lịch cho ứng viên của tin này.", CommonErrorCodes.Forbidden);

            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(booking.AvailabilitySlotId, ct);
            if (slot == null) return Result.Failure<bool>("Không tìm thấy ca phỏng vấn.", CommonErrorCodes.NotFound);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
            var jobTitle = job?.Title ?? "vị trí ứng tuyển";

            var local = slot.StartTime.ToOffset(TimeSpan.FromHours(7));
            var whenText = $"{local:HH:mm} ngày {local:dd/MM/yyyy} (giờ VN)";

            // 1. Send DB Notification + SignalR
            if (app.CandidateAccountId.HasValue)
            {
                var notifRepo = _unitOfWork.Repository<ARI.Domain.Entities.Notification>();
                await notifRepo.AddAsync(new ARI.Domain.Entities.Notification
                {
                    CandidateAccountId = app.CandidateAccountId.Value,
                    Type = "schedule_reminder",
                    Title = "Nhắc nhở lịch phỏng vấn",
                    Body = $"Nhắc nhở: Bạn có lịch phỏng vấn (vòng {booking.RoundNumber}) cho vị trí {jobTitle} vào lúc {whenText}. Vui lòng đăng nhập Candidate Portal để kiểm tra.",
                    Link = $"/portal/schedule/{app.Id}",
                    IsRead = false
                }, ct);
                await _notificationService.PublishUserEventAsync(app.CandidateAccountId.Value, "ReceiveUserNotification",
                    new { Type = "InterviewReminder", ApplicationId = app.Id, RoundNumber = booking.RoundNumber }, ct);
            }

            // 2. Send Email
            var subject = $"[ARISP] - Nhắc nhở lịch phỏng vấn vị trí {jobTitle}";
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
                    <h3 style='color: #333;'>Chào {app.CandidateName},</h3>
                    <p>Đây là thư nhắc nhở về lịch phỏng vấn <strong>vòng {booking.RoundNumber}</strong> cho vị trí <strong>{jobTitle}</strong>:</p>
                    <p style='text-align: center; font-size: 18px; font-weight: bold; color: #007bff; margin: 24px 0;'>{whenText}</p>
                    <p>Trạng thái hiện tại: <strong>{(booking.ConfirmationStatus == "confirmed" ? "Đã xác nhận" : "Chờ xác nhận")}</strong>.</p>
                    <p>Vui lòng chuẩn bị sẵn sàng và truy cập hệ thống đúng giờ.</p>
                    <br/>
                    <p>Trân trọng,</p>
                    <p><strong>Đội ngũ tuyển dụng ARISP</strong></p>
                </div>";

            try { await _notificationService.SendEmailAsync(app.CandidateEmail, subject, html, ct); } catch { }

            booking.Reminder24hSent = true;
            booking.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<InterviewBooking>().Update(booking);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(true);
        }

        /// <summary>Dời MỘT ứng viên sang ca khác — wrapper mỏng của bản nhiều người để chỉ có một đường code.</summary>
        public async Task<Result<bool>> RescheduleBookingAsync(
            Guid bookingId, Guid targetSlotId, Guid? userId, string? role, CancellationToken ct = default)
        {
            var result = await RescheduleBookingsAsync(new[] { bookingId }, targetSlotId, userId, role, ct);
            if (result.IsFailure) return Result.Failure<bool>(result.Error!, result.ErrorCode!);

            var failure = result.Value.Failed.FirstOrDefault();
            if (failure != null) return Result.Failure<bool>(failure.Message);

            return Result.Success(true);
        }

        /// <summary>
        /// Dời một hoặc nhiều ứng viên sang CÙNG một ca phỏng vấn, theo kiểu ĐƯỢC ĂN CẢ NGÃ VỀ KHÔNG:
        /// hoặc mọi người hợp lệ cùng được dời, hoặc không ai được dời.
        ///
        /// Trước đây giao diện gửi N request đơn lẻ tuần tự, nên dời 3 người vào ca còn 1 chỗ thì
        /// người đầu lọt còn hai người sau thất bại lần lượt, không có gì hoàn tác — nhân sự nhìn
        /// vào không biết ai đã chuyển ai chưa.
        /// </summary>
        public async Task<Result<RescheduleResultDto>> RescheduleBookingsAsync(
            IReadOnlyList<Guid> bookingIds, Guid targetSlotId, Guid? userId, string? role, CancellationToken ct = default)
        {
            if (bookingIds == null || bookingIds.Count == 0)
                return Result.Failure<RescheduleResultDto>("Chưa chọn ứng viên nào để dời lịch.");

            var targetSlot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(targetSlotId, ct);
            if (targetSlot == null)
                return Result.Failure<RescheduleResultDto>("Không tìm thấy ca phỏng vấn đích.", CommonErrorCodes.NotFound);

            // Kiểm quyền THEO TIN của ca đích. Trước đây hàm này không kiểm quyền gì cả: mọi tài khoản
            // nội bộ đều gọi được, và vì chỉ so khớp RoundNumber (vòng 1 thì tin nào cũng có) nên còn
            // dời được ứng viên của tin người khác sang ca của mình.
            var (canManage, _) = await SchedulingSupport.CanManageAsync(_unitOfWork, targetSlot.JobPostingId, userId, role, ct);
            if (!canManage)
                return Result.Failure<RescheduleResultDto>("Bạn không có quyền xếp lịch cho tin tuyển dụng này.", CommonErrorCodes.Forbidden);

            if (targetSlot.StartTime <= DateTimeOffset.UtcNow)
                return Result.Failure<RescheduleResultDto>("Ca phỏng vấn đích đã diễn ra trong quá khứ.");

            var ids = bookingIds.Distinct().ToList();
            var bookings = (await _unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => ids.Contains(b.Id), ct)).ToList();

            var appIds = bookings.Select(b => b.ApplicationId).Distinct().ToList();
            var appJobById = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .QueryAsync(q => q.Where(a => appIds.Contains(a.Id)).Select(a => new { a.Id, a.JobPostingId }), ct))
                .ToDictionary(a => a.Id, a => a.JobPostingId);

            // Lọc sạch trước khi chiếm chỗ — số chỗ cần chiếm phải là số người THẬT SỰ dời được.
            var failed = new List<RescheduleFailureDto>();
            var valid = new List<InterviewBooking>();

            foreach (var id in ids)
            {
                var booking = bookings.FirstOrDefault(b => b.Id == id);
                if (booking == null)
                {
                    failed.Add(new RescheduleFailureDto { BookingId = id, Message = "Không tìm thấy lịch phỏng vấn." });
                    continue;
                }

                string? error = null;
                if (booking.AvailabilitySlotId == targetSlotId)
                    error = "Ứng viên đã nằm trong ca này rồi.";
                else if (!appJobById.TryGetValue(booking.ApplicationId, out var jobId) || jobId != targetSlot.JobPostingId)
                    error = "Ca phỏng vấn đích không thuộc tin tuyển dụng của ứng viên này.";
                else if (targetSlot.RoundNumber != booking.RoundNumber)
                    error = "Không thể dời sang ca phỏng vấn thuộc vòng thi khác.";
                else if (string.Equals(booking.Status, BookingStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
                    error = "Hồ sơ đã bị loại khỏi quy trình nên không thể xếp lịch lại.";

                if (error != null) failed.Add(new RescheduleFailureDto { BookingId = id, Message = error });
                else valid.Add(booking);
            }

            if (valid.Count == 0)
                return Result.Success(new RescheduleResultDto { MovedCount = 0, Failed = failed });

            var seatsNeeded = valid.Count;
            var nowUtc = DateTimeOffset.UtcNow;

            // CHIẾM CHỖ NGUYÊN TỬ cho cả nhóm. Bản cũ đọc BookedCount rồi mới += 1 qua EF nên đua
            // được với chính nó và với AssignSlot. Một câu UPDATE có điều kiện là cách repo này vốn
            // đã dùng ở AssignSlotCommandHandler — dùng lại đúng mẫu đó.
            var taken = await _unitOfWork.ExecuteSqlRawAsync(
                "UPDATE availability_slots SET booked_count = booked_count + {1}, updated_at = {0} WHERE id = {2} AND booked_count + {1} <= capacity",
                new object[] { nowUtc, seatsNeeded, targetSlot.Id }, ct);
            if (taken == 0)
            {
                return Result.Failure<RescheduleResultDto>(
                    $"Ca phỏng vấn đích không còn đủ {seatsNeeded} chỗ trống.", CommonErrorCodes.Conflict);
            }

            // Ghi nhớ ca cũ của những booking ĐANG giữ chỗ — chỉ những cái này mới phải trả chỗ.
            // Bản cũ trả chỗ vô điều kiện, nên dời một ứng viên đã từ chối (đúng công dụng chính của
            // nút "Dời lịch") sẽ trừ lần thứ hai vào chỗ mà DeclineScheduleCommand đã trả rồi.
            var seatsToRelease = valid
                .Where(b => string.Equals(b.Status, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
                .GroupBy(b => b.AvailabilitySlotId)
                .Where(g => g.Key != targetSlot.Id)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var booking in valid)
            {
                booking.AvailabilitySlotId = targetSlot.Id;
                booking.Status = BookingStatus.Scheduled;
                booking.ConfirmationStatus = BookingConfirmationStatus.Pending;
                booking.DeclineReason = null;
                booking.DeclinedBy = null;
                booking.RespondedAt = null;
                booking.CandidateDismissedAt = null;
                booking.UpdatedAt = nowUtc;
                _unitOfWork.Repository<InterviewBooking>().Update(booking);
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                // Trả lại chỗ vừa chiếm. Nguyên nhân thực tế hay gặp: unique index một booking
                // 'scheduled' mỗi (hồ sơ, vòng) — ứng viên đã có lịch khác cho chính vòng này.
                await _unitOfWork.ExecuteSqlRawAsync(
                    "UPDATE availability_slots SET booked_count = GREATEST(booked_count - {1}, 0), updated_at = {0} WHERE id = {2}",
                    new object[] { DateTimeOffset.UtcNow, seatsNeeded, targetSlot.Id }, ct);
                return Result.Failure<RescheduleResultDto>(
                    "Không thể hoàn tất dời lịch (có thể ứng viên đã có lịch khác ở vòng này). Vui lòng tải lại và thử lại.");
            }

            // Trả chỗ ca cũ SAU khi đã lưu thành công. Thứ tự chiếm-trước-trả-sau là cố ý: trả trước
            // rồi chiếm hụt là đã cho đi cái chỗ ứng viên vẫn đang giữ. Chiếm trước chỉ dư tạm trong
            // một transaction — lệch về phía an toàn, không bao giờ để lọt quá sức chứa.
            foreach (var (slotId, count) in seatsToRelease)
            {
                try
                {
                    await _unitOfWork.ExecuteSqlRawAsync(
                        "UPDATE availability_slots SET booked_count = GREATEST(booked_count - {1}, 0), updated_at = {0} WHERE id = {2}",
                        new object[] { DateTimeOffset.UtcNow, count, slotId }, ct);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Không trả được {Count} chỗ ở khung giờ cũ {SlotId} sau khi dời lịch.", count, slotId);
                }
            }

            foreach (var booking in valid)
            {
                try { await NotifyRescheduledAsync(booking, targetSlot, ct); }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Không gửi được thông báo dời lịch cho booking {BookingId}.", booking.Id);
                }
            }

            return Result.Success(new RescheduleResultDto { MovedCount = valid.Count, Failed = failed });
        }

        /// <summary>Báo cho ứng viên biết lịch đã được dời (bell + realtime + email). Best-effort.</summary>
        private async Task NotifyRescheduledAsync(InterviewBooking booking, AvailabilitySlot targetSlot, CancellationToken ct)
        {
            var app = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(booking.ApplicationId, ct);
            if (app != null)
            {
                var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(app.JobPostingId, ct);
                var local = targetSlot.StartTime.ToOffset(TimeSpan.FromHours(7));
                var whenText = $"{local:HH:mm} ngày {local:dd/MM/yyyy} (giờ VN)";

                if (app.CandidateAccountId.HasValue)
                {
                    var notifRepo = _unitOfWork.Repository<ARI.Domain.Entities.Notification>();
                    await notifRepo.AddAsync(new ARI.Domain.Entities.Notification
                    {
                        CandidateAccountId = app.CandidateAccountId.Value,
                        Type = "schedule_rescheduled",
                        Title = "Lịch phỏng vấn đã được dời",
                        Body = $"Lịch phỏng vấn của bạn đã được chuyển sang thời gian mới: {whenText}. Vui lòng đăng nhập Candidate Portal để XÁC NHẬN.",
                        Link = $"/portal/schedule/{app.Id}",
                        IsRead = false
                    }, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    await _notificationService.PublishUserEventAsync(app.CandidateAccountId.Value, "ReceiveUserNotification",
                        new { Type = "InterviewRescheduled", ApplicationId = app.Id }, ct);
                }

                var subject = $"[ARISP] - Lịch phỏng vấn của bạn đã được dời sang {whenText}";
                var html = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
                        <h3 style='color: #333;'>Chào {app.CandidateName},</h3>
                        <p>Bộ phận nhân sự vừa dời lịch phỏng vấn <strong>vòng {booking.RoundNumber}</strong> cho vị trí <strong>{job?.Title ?? "ứng tuyển"}</strong> của bạn:</p>
                        <p style='text-align: center; font-size: 18px; font-weight: bold; color: #007bff; margin: 24px 0;'>{whenText}</p>
                        <p>Vui lòng đăng nhập Candidate Portal để <strong>xác nhận lịch mới này</strong>.</p>
                        <br/>
                        <p>Trân trọng,</p>
                        <p><strong>Đội ngũ nhân sự ARISP</strong></p>
                    </div>";
                try { await _notificationService.SendEmailAsync(app.CandidateEmail, subject, html, ct); } catch { }
            }
        }

        /// <summary>Lấy danh sách ca phỏng vấn theo job, kèm thống kê đặt lịch. Chỉ chủ tin hoặc admin.</summary>
        public async Task<Result<List<InterviewSlotDetailDto>>> GetSlotsForJobAsync(
            Guid jobPostingId, Guid? userId, string? role, CancellationToken ct = default)
        {
            var (canManage, job) = await SchedulingSupport.CanManageAsync(_unitOfWork, jobPostingId, userId, role, ct);
            if (job == null)
                return Result.Failure<List<InterviewSlotDetailDto>>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (!canManage)
                return Result.Failure<List<InterviewSlotDetailDto>>("Bạn không có quyền xem lịch phỏng vấn của tin này.", CommonErrorCodes.Forbidden);

            var nowUtc = DateTimeOffset.UtcNow;

            var slots = await _unitOfWork.Repository<AvailabilitySlot>()
                .QueryAsync(q => q.Where(s => s.JobPostingId == jobPostingId)
                    .OrderBy(s => s.RoundNumber).ThenBy(s => s.StartTime)
                    .Select(s => new { s.Id, s.JobPostingId, s.RoundNumber, s.StartTime, s.EndTime, s.Timezone, s.Capacity, s.BookedCount }), ct);
            if (slots.Count == 0) return Result.Success(new List<InterviewSlotDetailDto>());

            var slotIds = slots.Select(s => s.Id).ToList();
            var bookings = await _unitOfWork.Repository<InterviewBooking>()
                .QueryAsync(q => q.Where(b => slotIds.Contains(b.AvailabilitySlotId))
                    .Select(b => new { b.AvailabilitySlotId, b.Status, b.ConfirmationStatus }), ct);

            var bookingsBySlot = bookings.GroupBy(b => b.AvailabilitySlotId).ToDictionary(g => g.Key, g => g.ToList());

            return Result.Success(slots.Select(s =>
            {
                var bks = bookingsBySlot.TryGetValue(s.Id, out var bl) ? bl : new();

                // Số chỗ bị chiếm SUY TỪ DÒNG BOOKING chứ không đọc cột `booked_count`: số hiển thị
                // vì thế không thể trôi khỏi thực tế kể cả khi cột bị lệch. Cột vẫn giữ vai trò khoá
                // tương tranh (một câu UPDATE nguyên tử) ở đường ghi — xem AssignSlotCommandHandler.
                // Vị từ phải khớp từng chữ với migration ReconcileSlotBookedCount.
                var seatsTaken = bks.Count(b => b.Status == BookingStatus.Scheduled);

                // Cột và số suy ra lệch nhau là dấu hiệu có đường ghi nào đó quên cộng/trừ. Ghi log
                // để lộ ra thay vì âm thầm, nhưng KHÔNG tự sửa: đây là endpoint đọc, ghi ở đây sẽ
                // đua với câu UPDATE có điều kiện bên đường gán/dời lịch.
                if (s.BookedCount != seatsTaken)
                {
                    _logger?.LogWarning(
                        "booked_count lệch ở khung giờ {SlotId}: cột={Column}, số booking đang giữ chỗ={Derived}.",
                        s.Id, s.BookedCount, seatsTaken);
                }

                return new InterviewSlotDetailDto
                {
                    SlotId = s.Id,
                    JobPostingId = s.JobPostingId,
                    RoundNumber = s.RoundNumber,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Timezone = s.Timezone,
                    Capacity = s.Capacity,
                    BookedCount = seatsTaken,
                    SeatsAvailable = Math.Max(s.Capacity - seatsTaken, 0),
                    ConfirmedCount = bks.Count(b => b.Status == BookingStatus.Scheduled && b.ConfirmationStatus == BookingConfirmationStatus.Confirmed),
                    PendingCount = bks.Count(b => b.Status == BookingStatus.Scheduled && b.ConfirmationStatus == BookingConfirmationStatus.Pending),
                    DeclinedCount = bks.Count(b => b.Status == BookingStatus.Declined),
                    CancelledCount = bks.Count(b => b.Status == BookingStatus.Cancelled),
                    TotalBookingRows = bks.Count,
                    IsOverCapacity = seatsTaken > s.Capacity,
                    IsPast = s.StartTime < nowUtc
                };
            }).ToList());
        }

        /// <summary>
        /// Gộp (Status, ConfirmationStatus, DeclinedBy) thành MỘT trạng thái cho giao diện.
        /// Đây là nơi DUY NHẤT giữ ánh xạ này — giao diện đọc thẳng kết quả, không tự suy luận và
        /// tuyệt đối không dò nội dung DeclineReason (lý do là văn bản ứng viên tự nhập).
        /// </summary>
        internal static string ResolveCandidateState(string? status, string? confirmationStatus, string? declinedBy)
        {
            if (string.Equals(status, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(confirmationStatus, BookingConfirmationStatus.Confirmed, StringComparison.OrdinalIgnoreCase)
                    ? SlotCandidateState.Confirmed
                    : SlotCandidateState.Pending;
            }

            if (string.Equals(status, BookingStatus.Declined, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(declinedBy, BookingDeclinedBy.System, StringComparison.OrdinalIgnoreCase)
                    ? SlotCandidateState.ExpiredNoResponse
                    : SlotCandidateState.DeclinedByCandidate;
            }

            // Còn lại là "cancelled". Chỉ nhân sự loại hồ sơ mới ghi declined_by = staff; giá trị
            // khác (hoặc null với dữ liệu quá cũ) rơi về nhánh huỷ chung chung.
            return string.Equals(declinedBy, BookingDeclinedBy.Staff, StringComparison.OrdinalIgnoreCase)
                ? SlotCandidateState.RejectedByStaff
                : SlotCandidateState.Cancelled;
        }

        /// <summary>Lấy danh sách ứng viên trong một ca phỏng vấn, kèm trạng thái phiên AI và kết quả đánh giá.</summary>
        public async Task<Result<List<SlotCandidateDto>>> GetCandidatesInSlotAsync(
            Guid slotId, Guid? userId, string? role, CancellationToken ct = default)
        {
            var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(slotId, ct);
            if (slot == null)
                return Result.Failure<List<SlotCandidateDto>>("Không tìm thấy ca phỏng vấn.", CommonErrorCodes.NotFound);

            var (canManage, _) = await SchedulingSupport.CanManageAsync(_unitOfWork, slot.JobPostingId, userId, role, ct);
            if (!canManage)
                return Result.Failure<List<SlotCandidateDto>>("Bạn không có quyền xem ứng viên của ca phỏng vấn này.", CommonErrorCodes.Forbidden);

            var bookings = await _unitOfWork.Repository<InterviewBooking>()
                .QueryAsync(q => q.Where(b => b.AvailabilitySlotId == slotId)
                    .Select(b => new { b.Id, b.ApplicationId, b.RoundNumber, b.ConfirmationStatus, b.DeclineReason, b.DeclinedBy, b.Status }), ct);
            if (bookings.Count == 0) return Result.Success(new List<SlotCandidateDto>());

            var appIds = bookings.Select(b => b.ApplicationId).Distinct().ToList();

            // Phiên/đánh giá/mã đều gắn với (hồ sơ, VÒNG) chứ không gắn với ca phỏng vấn — không có
            // khoá ngoại nào tới booking. Vậy nên vòng là mối liên kết mịn nhất có thể dùng, và
            // BẮT BUỘC phải lọc theo nó: trước đây chỉ lọc theo hồ sơ nên một phiên đang chạy ở
            // vòng 2 hiện thành huy hiệu "Đang thực hiện" trên dòng của vòng 1.
            var rounds = bookings.Select(b => b.RoundNumber).Distinct().ToList();

            var apps = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => q.Where(a => appIds.Contains(a.Id))
                    .Select(a => new { a.Id, a.CandidateName, a.CandidateEmail, a.JobPostingId, a.Status }), ct);

            var sessions = await _unitOfWork.Repository<InterviewSession>()
                .QueryAsync(q => q.Where(s => appIds.Contains(s.ApplicationId) && s.SessionType == "real" && rounds.Contains(s.RoundNumber))
                    .Select(s => new { s.Id, s.ApplicationId, s.RoundNumber, s.Status, s.DurationSeconds, s.StartedAt, s.CreatedAt }), ct);

            var evals = await _unitOfWork.Repository<Evaluation>()
                .QueryAsync(q => q.Where(e => appIds.Contains(e.ApplicationId) && e.SessionType == "real" && rounds.Contains(e.RoundNumber))
                    .Select(e => new { e.Id, e.ApplicationId, e.RoundNumber, e.SessionId, e.AiVerdict, e.OverallScore, e.CreatedAt }), ct);

            var nowUtc = DateTimeOffset.UtcNow;
            var activeCodes = await _unitOfWork.Repository<InterviewCode>()
                .QueryAsync(q => q.Where(c => appIds.Contains(c.ApplicationId) && rounds.Contains(c.RoundNumber) && !c.UsedAt.HasValue && c.ExpiresAt > nowUtc)
                    .Select(c => new { c.ApplicationId, c.RoundNumber, c.Code, c.ExpiresAt, c.CreatedAt }), ct);

            var appById = apps.ToDictionary(a => a.Id);

            // Sắp theo THỜI GIAN. Bản cũ dùng OrderByDescending(x => x.Id) trên khoá Guid V4 — một
            // thứ tự ổn định nhưng hoàn toàn ngẫu nhiên, nên có hồ sơ luôn hiện đúng phiên còn hồ sơ
            // khác luôn hiện phiên cũ, và lỗi trông như dữ liệu thật chứ không như lỗi sắp xếp.
            var sessionByAppRound = sessions
                .GroupBy(s => (s.ApplicationId, s.RoundNumber))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartedAt ?? s.CreatedAt).ThenByDescending(s => s.CreatedAt).First());
            var evalsByAppRound = evals
                .GroupBy(e => (e.ApplicationId, e.RoundNumber))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).ToList());
            var codeByAppRound = activeCodes
                .GroupBy(c => (c.ApplicationId, c.RoundNumber))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ExpiresAt).ThenByDescending(c => c.CreatedAt).First());

            return Result.Success(bookings.Select(b =>
            {
                var key = (b.ApplicationId, b.RoundNumber);
                appById.TryGetValue(b.ApplicationId, out var app);
                sessionByAppRound.TryGetValue(key, out var sess);
                codeByAppRound.TryGetValue(key, out var codeObj);

                // Ưu tiên đánh giá của ĐÚNG phiên đang hiển thị; không có thì lấy bản mới nhất của vòng.
                evalsByAppRound.TryGetValue(key, out var roundEvals);
                var eval = sess != null
                    ? roundEvals?.FirstOrDefault(e => e.SessionId == sess.Id) ?? roundEvals?.FirstOrDefault()
                    : roundEvals?.FirstOrDefault();

                // KHÔNG ghi đè ConfirmationStatus/BookingStatus/DeclineReason theo trạng thái hồ sơ
                // nữa. Bản cũ làm vậy nên ba kết cục khác hẳn nhau (ứng viên báo bận / hệ thống huỷ
                // vì quá hạn / nhân sự loại hồ sơ) bị gộp thành một nhãn không phân biệt được, và
                // lý do thật của ứng viên bị xoá khỏi màn hình. Nay trạng thái hồ sơ đi riêng qua
                // ApplicationStatus, còn CandidateState nói rõ lịch bị đóng vì đâu.
                return new SlotCandidateDto
                {
                    ApplicationId = b.ApplicationId,
                    BookingId = b.Id,
                    RoundNumber = b.RoundNumber,
                    CandidateName = app?.CandidateName ?? "—",
                    CandidateEmail = app?.CandidateEmail ?? "—",
                    ConfirmationStatus = b.ConfirmationStatus,
                    DeclineReason = b.DeclineReason,
                    BookingStatus = b.Status,
                    CandidateState = ResolveCandidateState(b.Status, b.ConfirmationStatus, b.DeclinedBy),
                    OccupiesSeat = string.Equals(b.Status, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase),
                    ApplicationStatus = app?.Status,
                    SessionId = sess?.Id,
                    SessionStatus = sess?.Status,
                    DurationSeconds = sess?.DurationSeconds,
                    EvaluationId = eval?.Id,
                    Verdict = eval?.AiVerdict,
                    OverallScore = eval?.OverallScore.HasValue == true ? Convert.ToInt32(eval.OverallScore.Value) : null,
                    InterviewCode = codeObj?.Code,
                    CodeExpiresAt = codeObj?.ExpiresAt
                };
            }).ToList());
        }

        // ────────────────────────────────────────────────────────────────────

        public async Task<Result<StartSessionResponse>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(request.ApplicationId, ct);
            if (application == null)
                return Result.Failure<StartSessionResponse>("Application not found.");

            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            if (jobPosting == null)
                return Result.Failure<StartSessionResponse>("Job posting not found.");

            var roundConfigs = await _unitOfWork.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == jobPosting.Id && r.RoundNumber == request.RoundNumber, ct);
            var roundConfig = roundConfigs.FirstOrDefault() ?? new InterviewRoundConfig
            {
                RoundType = request.RoundNumber == 1 ? "screening" : "technical",
                InterviewCodeTtlHours = 2,
                MaxDurationMinutes = 45
            };

            // Giới hạn phỏng vấn thử theo VÒNG (ADR-038, mặc định 1 lượt/vòng).
            // Interview:PracticeAttemptsPerRound <= 0 = không giới hạn (chỉ dùng dev/test).
            if (request.SessionType == "practice")
            {
                // Vòng trắc nghiệm KHÔNG có phỏng vấn thử: buổi thử là hội thoại với AI (STT/TTS/RAG),
                // không có gì để "thử" với một bài chọn đáp án — và cho thử sẽ lộ chính ngân hàng đề.
                if (InterviewInviteEmail.IsOnlineTest(roundConfig.RoundType))
                    return Result.Failure<StartSessionResponse>("Vòng trắc nghiệm không có phỏng vấn thử.");

                // Lỡ buổi phỏng vấn thật của vòng = trượt vòng đó. Chặn ở đây vì đây là nguồn sự thật:
                // cờ ẩn nút chỉ là lớp giao diện, gọi thẳng API vẫn phải bị từ chối.
                if (await SchedulingSupport.HasMissedRealInterviewAsync(
                        _unitOfWork, application.Id, request.RoundNumber, ct))
                    return Result.Failure<StartSessionResponse>(
                        "Buổi phỏng vấn thật của vòng này đã qua giờ hẹn, bạn không còn lượt phỏng vấn thử.");

                var maxAttempts = _interviewOptions.PracticeAttemptsPerRound;
                if (maxAttempts > 0)
                {
                    var existingPractice = await _unitOfWork.Repository<InterviewSession>().FindAsync(
                        s => s.ApplicationId == application.Id
                             && s.SessionType == "practice"
                             && s.RoundNumber == request.RoundNumber, ct);
                    if (existingPractice.Count() >= maxAttempts)
                        return Result.Failure<StartSessionResponse>("Bạn đã dùng lượt phỏng vấn thử cho vòng này.");
                }

                // Giữ cờ tổng để tương thích ngược (không còn dùng làm điều kiện chặn).
                application.PracticeSessionUsed = true;
                _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);
            }

            // Ngôn ngữ viết báo cáo = ngôn ngữ FE đang dùng (chỉ nhận vi|en), fallback ngôn ngữ phỏng vấn.
            var uiLanguage = (request.UiLanguage ?? string.Empty).Trim().ToLowerInvariant();
            if (uiLanguage != "vi" && uiLanguage != "en") uiLanguage = string.Empty;

            var session = new InterviewSession
            {
                ApplicationId = application.Id,
                RoundNumber = request.RoundNumber,
                RoundType = roundConfig.RoundType,
                SessionType = request.SessionType,
                InterviewLanguage = jobPosting.DetectedLanguage ?? "vi",
                ReportLanguage = string.IsNullOrEmpty(uiLanguage) ? null : uiLanguage,
                Status = "active",
                StartedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<InterviewSession>().AddAsync(session, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            _cache.Remove(AllSessionsCacheKey); // phiên mới — xóa cache để lần load tiếp thấy kết quả mới nhất

            // Đảm bảo CV + JD đã ingest vào RAG (practice = JD+CV; playbook chỉ cho real) trước câu hỏi đầu.
            await EnsureSourcesIngestedAsync(application, jobPosting, ct);

            // Seed Must-Ask questions from Playbook into tracking if it's a real session
            if (request.SessionType == "real")
            {
                var playbooks = await _unitOfWork.Repository<PlaybookDocument>()
                    .FindAsync(p => p.Scope == "job_posting" && p.ScopeRefId == jobPosting.Id && p.DocumentType == "must_ask", ct);
                
                foreach (var playbook in playbooks)
                {
                    if (string.IsNullOrEmpty(playbook.ParsedText)) continue;
                    
                    var mustAskLines = playbook.ParsedText.Split(new[] { "\n", ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var q in mustAskLines)
                    {
                        var track = new MustAskTracking
                        {
                            SessionId = session.Id,
                            PlaybookDocumentId = playbook.Id,
                            QuestionText = q.Trim()
                        };
                        await _unitOfWork.Repository<MustAskTracking>().AddAsync(track, ct);
                    }
                }
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // HeyGen avatar integration (Hybrid Idle Strategy)
            string? heyGenSdp = null;
            string? heyGenSessionId = null;

            if (!string.IsNullOrEmpty(jobPosting.PersonaVoiceId) && !string.IsNullOrEmpty(jobPosting.PersonaStyle))
            {
                try
                {
                    var sdpMessage = await _avatarService.StartSessionAsync(jobPosting.PersonaVoiceId, jobPosting.PersonaStyle, ct);
                    heyGenSdp = sdpMessage.Sdp;
                    heyGenSessionId = "heygen_" + Guid.NewGuid().ToString("N");
                }
                catch
                {
                    // Fail silently, fall back to simple non-avatar
                }
            }

            var response = new StartSessionResponse
            {
                SessionId = session.Id,
                Status = session.Status,
                Language = session.InterviewLanguage,
                HeyGenSdpOffer = heyGenSdp,
                HeyGenSessionId = heyGenSessionId
            };

            return Result.Success(response);
        }

        public async Task<Result<string>> GenerateAndSendNextQuestionAsync(Guid sessionId, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<string>("Session not found.");
            if (session.Status != "active")
                return Result.Failure<string>("Session is not active.");

            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application!.JobPostingId, ct);
            var questions = await _unitOfWork.Repository<Question>().FindAsync(q => q.SessionId == sessionId, ct);
            var sequenceNumber = questions.Count() + 1;

            // Cap an toàn: quá số câu tối đa HOẶC vượt trần thời lượng (ADR-050, practice 20')
            // → KHÔNG cắt phụt; buộc AI sinh lời cảm ơn kết thúc (ForceClosing) rồi mới đóng phiên
            // — ứng viên luôn nhận được lời chào tạm biệt. Đây là lớp enforce server-side độc lập
            // với đồng hồ FE (ứng viên nói quá giờ → câu kế tiếp thành lời chào kết thúc).
            var elapsedMinutes = session.StartedAt.HasValue
                ? (DateTimeOffset.UtcNow - session.StartedAt.Value).TotalMinutes
                : 0;
            var maxMinutes = session.SessionType == "practice" ? _interviewOptions.PracticeMaxDurationMinutes : 0;
            var timeExceeded = maxMinutes > 0 && elapsedMinutes >= maxMinutes;
            var forceClosing = sequenceNumber > 12 || timeExceeded;

            // 1. Gather Weighted RAG Context.
            // CHỈ project ChunkText — KHÔNG load full entity (cột `embedding` kiểu pgvector không
            // materialize được qua Npgsql/EF khi chưa bật UseVector → InvalidCastException).
            var ragContext = new List<string>();
            var cvChunks = await _unitOfWork.Repository<DocumentChunk>()
                .QueryAsync(q => q.Where(c => c.SourceType == "cv" && c.SourceId == application.Id).Select(c => c.ChunkText), ct);
            ragContext.AddRange(cvChunks.Select(t => $"[CV Chunk] {t}"));

            var jdChunks = await _unitOfWork.Repository<DocumentChunk>()
                .QueryAsync(q => q.Where(c => c.SourceType == "jd" && c.SourceId == jobPosting!.Id).Select(c => c.ChunkText), ct);
            ragContext.AddRange(jdChunks.Select(t => $"[JD Chunk] {t}"));

            if (session.SessionType == "real")
            {
                var playbookChunks = await _unitOfWork.Repository<DocumentChunk>()
                    .QueryAsync(q => q.Where(c => c.SourceType == "playbook").Select(c => c.ChunkText), ct);
                ragContext.AddRange(playbookChunks.Select(t => $"[Org Playbook] {t}"));
            }

            // 2. Select Next Question Strategy
            string? mustAskQuestionText = null;
            MustAskTracking? mustAskRecord = null;

            if (session.SessionType == "real")
            {
                var unaskedMustAsks = await _unitOfWork.Repository<MustAskTracking>()
                    .FindAsync(m => m.SessionId == sessionId && m.AskedAt == null, ct);
                mustAskRecord = unaskedMustAsks.FirstOrDefault();
                if (mustAskRecord != null)
                {
                    mustAskQuestionText = mustAskRecord.QuestionText;
                }
            }

            // 3. Assemble Prompt & Generate via AI
            // Load toàn bộ answer của phiên trong 1 query (tránh N+1 — DB remote, mỗi round-trip đắt).
            var sessionAnswers = await _unitOfWork.Repository<Answer>().FindAsync(a => a.SessionId == sessionId, ct);
            var answerByQuestionId = sessionAnswers
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());
            var chatHistory = questions
                .OrderBy(x => x.SequenceNumber)
                .Select(q => new QuestionAnswerDto
                {
                    SequenceNumber = q.SequenceNumber,
                    QuestionText = q.QuestionText,
                    AnswerText = answerByQuestionId.TryGetValue(q.Id, out var a) ? a.Transcript : "[No response]"
                })
                .ToList();

            var aiContext = new QuestionContext
            {
                SessionId = sessionId,
                JobPostingId = jobPosting!.Id,
                ApplicationId = application.Id,
                JobDescription = jobPosting.JobDescription,
                CandidateCv = application.CvText ?? "",
                SessionType = session.SessionType,
                ChatHistory = chatHistory,
                PlaybookStyleGuides = ragContext.Where(r => r.StartsWith("[Org")).ToList(),
                Language = session.InterviewLanguage,
                ForceClosing = forceClosing
            };

            if (mustAskQuestionText != null)
            {
                aiContext.MustAskQuestions = new List<string> { mustAskQuestionText };
            }

            // Call AI provider to generate the question text
            string generatedQuestion = "";
            await foreach (var token in _aiProvider.StreamQuestionAsync(aiContext, ct))
            {
                generatedQuestion += token;
            }

            // AI chủ động kết thúc (đủ độ bao phủ, marker [END_INTERVIEW]) hoặc bị buộc (cap câu hỏi):
            // gửi lời cảm ơn (text + TTS) cho ứng viên TRƯỚC, rồi mới đóng phiên + sinh evaluation.
            const string endMarker = "[END_INTERVIEW]";
            if (forceClosing || generatedQuestion.Contains(endMarker, StringComparison.OrdinalIgnoreCase))
            {
                var aiText = generatedQuestion
                    .Replace(endMarker, "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
                var farewell = await CloseWithFarewellAsync(sessionId, session.InterviewLanguage, aiText, ct);
                return Result.Success(farewell);
            }

            // 4. Save generated question
            var question = new Question
            {
                SessionId = sessionId,
                SequenceNumber = sequenceNumber,
                QuestionText = generatedQuestion,
                QuestionType = mustAskQuestionText != null ? "playbook_must_ask" : "ai_generated",
                DifficultyLevel = questions.LastOrDefault()?.DifficultyLevel ?? 3,
                Source = mustAskQuestionText != null ? "playbook_must_ask" : "ai_generated"
            };

            await _unitOfWork.Repository<Question>().AddAsync(question, ct);

            if (mustAskRecord != null)
            {
                mustAskRecord.AskedAt = DateTimeOffset.UtcNow;
                mustAskRecord.QuestionId = question.Id;
                _unitOfWork.Repository<MustAskTracking>().Update(mustAskRecord);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            // 5. Notify SignalR Clients — đẩy text ngay để FE hiển thị không chờ TTS.
            await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveQuestion", new
            {
                questionId = question.Id,
                sequenceNumber = question.SequenceNumber,
                questionText = question.QuestionText,
                questionType = question.QuestionType,
                difficultyLevel = question.DifficultyLevel
            }, ct);

            // 6. TTS server-side rồi đẩy audio qua SignalR (ReceiveQuestionAudio) — bỏ round-trip
            // FE→BE /tts (kèm 2 query xác thực) khỏi critical path. Lỗi TTS → FE tự fallback.
            try
            {
                var audio = await _ttsService.TextToSpeechBase64PcmAsync(question.QuestionText, string.Empty, ct);
                if (!string.IsNullOrEmpty(audio))
                {
                    await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveQuestionAudio", new
                    {
                        questionId = question.Id,
                        audio
                    }, ct);
                }
            }
            catch
            {
                // TTS best-effort — FE fallback browser TTS khi không nhận được audio.
            }

            return Result.Success(question.QuestionText);
        }

        public async Task<Result<Answer>> SubmitAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default)
        {
            var saved = await SaveAnswerAsync(sessionId, questionId, transcript, responseTimeMs, ct);
            if (saved.IsFailure)
                return saved;

            await AnalyzeAnswerAndAdaptAsync(sessionId, questionId, transcript, ct);
            return saved;
        }

        /// <summary>
        /// Lưu answer NHANH (không gọi LLM) — dùng ở SessionHub để câu hỏi kế tiếp được sinh ngay,
        /// phân tích adaptive chạy sau (AnalyzeAnswerAndAdaptAsync) ngoài critical path latency.
        /// </summary>
        public async Task<Result<Answer>> SaveAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<Answer>("Session not found.");
            if (session.Status != "active")
                return Result.Failure<Answer>("Session is not active.");

            var answer = new Answer
            {
                QuestionId = questionId,
                SessionId = sessionId,
                Transcript = transcript,
                ResponseTimeMs = responseTimeMs
            };

            await _unitOfWork.Repository<Answer>().AddAsync(answer, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success(answer);
        }

        /// <summary>
        /// Phân tích answer để điều chỉnh độ khó (adaptive difficulty) + đẩy ReceiveAnswerAnalysis.
        /// Best-effort: lỗi LLM không ảnh hưởng luồng phỏng vấn.
        /// </summary>
        public async Task AnalyzeAnswerAndAdaptAsync(Guid sessionId, Guid questionId, string transcript, CancellationToken ct = default)
        {
            try
            {
                var question = await _unitOfWork.Repository<Question>().GetByIdAsync(questionId, ct);
                if (question == null) return;

                var analysis = await _aiProvider.AnalyzeAnswerAsync(new AnswerContext
                {
                    QuestionText = question.QuestionText,
                    AnswerTranscript = transcript
                }, ct);

                // Update question difficulty level adaptively
                question.DifficultyLevel = analysis.DifficultyLevel;
                _unitOfWork.Repository<Question>().Update(question);
                await _unitOfWork.SaveChangesAsync(ct);

                await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveAnswerAnalysis", new
                {
                    feedback = analysis.Feedback
                }, ct);
            }
            catch
            {
                // Fallback if AI analysis fails
            }
        }

        public async Task<Result<bool>> EndSessionAsync(Guid sessionId, string status = "completed", CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<bool>("Session not found.");

            // Idempotent: phiên đã "completed" là trạng thái cuối — không đóng/sinh evaluation lần hai
            // (chống race khi enforce server + NotifyTimeout FE cùng bắn — ADR-050).
            if (session.Status == "completed")
                return Result.Success(true);

            session.Status = status;
            session.EndedAt = DateTimeOffset.UtcNow;
            if (session.StartedAt.HasValue)
            {
                session.DurationSeconds = (int)(session.EndedAt.Value - session.StartedAt.Value).TotalSeconds;
            }

            _unitOfWork.Repository<InterviewSession>().Update(session);
            await _unitOfWork.SaveChangesAsync(ct);
            _cache.Remove(AllSessionsCacheKey); // trạng thái phiên thay đổi — xóa cache

            // If session is completed, automatically trigger AI Evaluation report generation
            if (status == "completed")
            {
                await GenerateEvaluationReportAsync(session.Id, ct);
            }

            await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveSessionStatus", new { status }, ct);

            return Result.Success(true);
        }

        /// <summary>
        /// Gửi lời chào kết thúc (text + TTS best-effort) rồi đóng phiên "completed" (ADR-050).
        /// Dùng chung cho: cap câu hỏi / trần thời lượng (GenerateAndSendNextQuestionAsync) và
        /// hết giờ phía FE (PracticeTimeoutCloseAsync). IDEMPOTENT — phiên đã "completed" thì no-op
        /// (chống race khi cả enforce server lẫn trigger FE cùng bắn).
        /// Trả về câu chào đã dùng.
        /// </summary>
        private async Task<string> CloseWithFarewellAsync(Guid sessionId, string? language, string? aiText, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null || session.Status == "completed")
                return string.Empty; // idempotent: đã đóng rồi thì không gửi closing/evaluation lần hai

            var farewell = (aiText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(farewell))
            {
                farewell = (language ?? "vi").StartsWith("vi", StringComparison.OrdinalIgnoreCase)
                    ? "Cảm ơn bạn đã dành thời gian tham gia buổi phỏng vấn hôm nay. Kết quả sẽ được gửi tới bạn trong thời gian sớm nhất. Chúc bạn một ngày tốt lành!"
                    : "Thank you for taking the time to join this interview. Your results will be shared with you soon. Have a great day!";
            }

            // Lưu câu chào trước khi phát — transcript xem lại phải khớp đúng những gì ứng viên nghe (ADR-051).
            session.ClosingText = farewell;
            _unitOfWork.Repository<InterviewSession>().Update(session);
            await _unitOfWork.SaveChangesAsync(ct);

            await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveClosing", new { text = farewell }, ct);

            try
            {
                var closingAudio = await _ttsService.TextToSpeechBase64PcmAsync(farewell, string.Empty, ct);
                if (!string.IsNullOrEmpty(closingAudio))
                {
                    await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveClosingAudio", new { audio = closingAudio }, ct);
                }
            }
            catch
            {
                // TTS best-effort — FE tự fallback browser TTS.
            }

            await EndSessionAsync(sessionId, "completed", ct);
            return farewell;
        }

        /// <summary>
        /// FE báo hết giờ (đồng hồ đếm ngược chạm 0) → AI nói 1 câu kết thúc rồi đóng phiên (ADR-050).
        /// Guard server-side: chỉ chấp nhận khi ĐÃ chạm ~95% trần thời lượng — FE không thể kết thúc
        /// sớm để né phần còn lại. Chỉ áp dụng phiên "practice".
        /// </summary>
        public async Task<Result<bool>> PracticeTimeoutCloseAsync(Guid sessionId, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<bool>("Session not found.");
            if (session.Status == "completed")
                return Result.Success(true); // đã đóng — idempotent

            // Áp dụng cho CẢ buổi thật (ADR-052) với trần riêng của từng loại phiên.
            var maxMinutes = session.SessionType == "practice"
                ? _interviewOptions.PracticeMaxDurationMinutes
                : _interviewOptions.RealMaxDurationMinutes;
            if (maxMinutes > 0 && session.StartedAt.HasValue)
            {
                var elapsedMinutes = (DateTimeOffset.UtcNow - session.StartedAt.Value).TotalMinutes;
                if (elapsedMinutes < maxMinutes * 0.95)
                    return Result.Failure<bool>("Chưa hết thời gian phỏng vấn.");
            }

            await CloseWithFarewellAsync(sessionId, session.InterviewLanguage, null, ct);
            return Result.Success(true);
        }

        /// <summary>
        /// Lưu video buổi phỏng vấn THẬT (Kiosk quay tại chỗ) vào storage + đặt hạn xoá tự động
        /// theo <c>Interview:RecordingRetentionDays</c> (ADR-052). Buổi THỬ không quay video
        /// (ADR-038 điểm 6) nên bị từ chối ở đây.
        /// </summary>
        public async Task<Result<RecordingUploadResponse>> SaveRecordingAsync(
            Guid sessionId, byte[] content, string fileName, string contentType, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<RecordingUploadResponse>("Không tìm thấy phiên phỏng vấn.");
            if (session.SessionType == "practice")
                return Result.Failure<RecordingUploadResponse>("Phỏng vấn thử không quay video.");
            if (content == null || content.Length == 0)
                return Result.Failure<RecordingUploadResponse>("Dữ liệu ghi hình rỗng.");

            var maxBytes = (long)Math.Max(1, _interviewOptions.MaxRecordingSizeMb) * 1024 * 1024;
            if (content.LongLength > maxBytes)
                return Result.Failure<RecordingUploadResponse>($"File ghi hình vượt quá {_interviewOptions.MaxRecordingSizeMb}MB.");

            // Ghi đè bản cũ (nếu upload lại) — không để file mồ côi trong storage.
            if (!string.IsNullOrEmpty(session.RecordingUrl))
            {
                try { await _fileStorage.DeleteAsync(session.RecordingUrl, ct); } catch { /* best-effort */ }
            }

            var safeName = string.IsNullOrWhiteSpace(fileName) ? $"interview-{sessionId}.webm" : fileName;
            // MediaRecorder gửi "video/webm;codecs=vp9,opus" — bỏ tham số, chỉ giữ MIME type gốc.
            var baseContentType = (contentType ?? string.Empty).Split(';')[0].Trim();
            if (string.IsNullOrEmpty(baseContentType)) baseContentType = "video/webm";

            string storageKey;
            try
            {
                storageKey = await _fileStorage.SaveAsync(content, safeName, baseContentType, StorageFolder.Recording, ct);
            }
            catch (Exception ex)
            {
                // Storage lỗi là lỗi nghiệp vụ với Kiosk (hiện cảnh báo "không lưu được bản ghi"),
                // không để văng 500 giữa màn kết thúc phỏng vấn.
                return Result.Failure<RecordingUploadResponse>($"Không lưu được bản ghi hình: {ex.Message}");
            }

            var retentionDays = _interviewOptions.RecordingRetentionDays;
            session.RecordingUrl = storageKey;
            session.RecordingSizeBytes = content.LongLength;
            session.RecordingExpiresAt = retentionDays > 0 ? DateTimeOffset.UtcNow.AddDays(retentionDays) : null;
            session.RecordingDeletedAt = null;
            _unitOfWork.Repository<InterviewSession>().Update(session);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new RecordingUploadResponse
            {
                Saved = true,
                SizeBytes = content.LongLength,
                ExpiresAt = session.RecordingExpiresAt
            });
        }

        /// <summary>Trọng số điểm nghi vấn theo loại tín hiệu — dùng chung khi chấm và khi tổng hợp.</summary>
        private static readonly Dictionary<string, (decimal Weight, string Severity)> CheatSignalWeights = new()
        {
            ["fullscreen_exit"] = (8m, "medium"),   // thoát toàn màn hình
            ["tab_hidden"] = (12m, "high"),         // chuyển tab / thu nhỏ cửa sổ
            ["window_blur"] = (5m, "low"),          // click ra ngoài cửa sổ
            ["shortcut_blocked"] = (3m, "low"),     // bấm phím tắt bị chặn
            ["page_unload"] = (15m, "high"),        // đóng/tải lại trang giữa buổi
        };

        /// <summary>
        /// Ghi nhận tín hiệu nghi vấn của một phiên (Kiosk thoát toàn màn hình, chuyển tab…).
        /// Trước đây `SessionHub.ReportCheatSignal` chỉ phát cảnh báo realtime rồi bỏ — không có gì
        /// xuống DB nên báo cáo luôn trống. Nay lưu thật để tổng hợp vào kết quả đánh giá (ADR-054).
        /// </summary>
        public async Task<Result<int>> RecordCheatSignalAsync(
            Guid sessionId, string signalType, string? payloadJson, CancellationToken ct = default)
        {
            var type = (signalType ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(type))
                return Result.Failure<int>("Thiếu loại tín hiệu.");

            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<int>("Không tìm thấy phiên phỏng vấn.");

            // Payload là dữ liệu do client gửi — chặn phình to, luôn giữ JSON hợp lệ cho cột jsonb.
            var payload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson!.Trim();
            if (payload.Length > 2000 || (!payload.StartsWith("{") && !payload.StartsWith("[")))
                payload = "{}";

            await _unitOfWork.Repository<CheatDetectionSignal>().AddAsync(new CheatDetectionSignal
            {
                SessionId = sessionId,
                SignalType = type,
                Payload = payload
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(await _unitOfWork.Repository<CheatDetectionSignal>()
                .CountAsync(s => s.SessionId == sessionId && s.SignalType == type, ct));
        }

        /// <summary>
        /// Chấm LẠI một phiên đã kết thúc (dev/ops): xoá bản đánh giá cũ rồi chạy lại pipeline chấm
        /// với prompt hiện tại. Dùng khi báo cáo cũ sinh từ prompt lỗi thời (sai ngôn ngữ, thiếu
        /// điểm từng câu). Từ chối nếu HR đã review — không đụng vào kết quả đã chốt.
        /// </summary>
        public async Task<Result<bool>> RegenerateEvaluationAsync(
            Guid sessionId, string? reportLanguage = null, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<bool>("Không tìm thấy phiên phỏng vấn.");

            var existing = (await _unitOfWork.Repository<Evaluation>()
                .FindAsync(e => e.SessionId == sessionId, ct)).ToList();
            var evalIds = existing.Select(e => e.Id).ToList();
            var reviewed = evalIds.Count > 0
                && (await _unitOfWork.Repository<HrReview>().FindAsync(r => evalIds.Contains(r.EvaluationId), ct)).Any();
            if (reviewed)
                return Result.Failure<bool>("Đánh giá đã được HR xác nhận — không chấm lại.");

            var lang = (reportLanguage ?? string.Empty).Trim().ToLowerInvariant();
            if (lang == "vi" || lang == "en")
            {
                session.ReportLanguage = lang;
                _unitOfWork.Repository<InterviewSession>().Update(session);
            }

            foreach (var e in existing) _unitOfWork.Repository<Evaluation>().Delete(e);
            await _unitOfWork.SaveChangesAsync(ct);

            await GenerateEvaluationReportAsync(sessionId, ct);
            return Result.Success(true);
        }

        /// <summary>
        /// Tổng số vòng của job = <c>max(InterviewRoundConfig.RoundNumber)</c>. Job chưa khai báo
        /// vòng nào thì coi như 1 vòng (khớp fallback ở <see cref="StartSessionAsync"/>).
        /// Dùng để xác định "vòng cuối" — điều kiện duy nhất để hồ sơ được đặt "pass" (ADR-053).
        /// </summary>
        private async Task<int> ResolveTotalRoundsAsync(Guid jobPostingId, CancellationToken ct = default)
        {
            var rounds = await _unitOfWork.Repository<InterviewRoundConfig>()
                .QueryAsync(q => q.Where(r => r.JobPostingId == jobPostingId).Select(r => r.RoundNumber), ct);
            return rounds.Count == 0 ? 1 : Math.Max(1, rounds.Max());
        }

        private async Task GenerateEvaluationReportAsync(Guid sessionId, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session!.ApplicationId, ct);
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application!.JobPostingId, ct);
            var questions = await _unitOfWork.Repository<Question>().FindAsync(q => q.SessionId == sessionId, ct);

            var chatHistory = new List<QuestionAnswerDto>();
            foreach (var q in questions.OrderBy(x => x.SequenceNumber))
            {
                var answers = await _unitOfWork.Repository<Answer>().FindAsync(a => a.QuestionId == q.Id, ct);
                chatHistory.Add(new QuestionAnswerDto
                {
                    SequenceNumber = q.SequenceNumber,
                    QuestionText = q.QuestionText,
                    AnswerText = answers.FirstOrDefault()?.Transcript ?? ""
                });
            }

            var evalCtx = new SessionContext
            {
                SessionId = sessionId,
                JobDescription = jobPosting!.JobDescription,
                CandidateCv = application.CvText ?? "",
                SessionType = session.SessionType,
                ChatHistory = chatHistory,
                ScoringRubric = jobPosting.ScoringRubric ?? "{}",
                Language = session.InterviewLanguage ?? jobPosting.DetectedLanguage,
                // Báo cáo viết bằng ngôn ngữ ứng viên đang dùng trên web (ADR-051).
                ReportLanguage = session.ReportLanguage ?? session.InterviewLanguage ?? "vi"
            };

            // Call AI provider to generate Verdict, Score, Reasoning, etc.
            var evalReport = await _aiProvider.GenerateEvaluationAsync(evalCtx, ct);
            
            // Tín hiệu nghi vấn: chấm theo trọng số từng loại (trước đây chỉ "có tín hiệu = 10 điểm"
            // và danh sách bị ghi cứng "[]" nên HR không bao giờ thấy chi tiết) — ADR-054.
            var signals = (await _unitOfWork.Repository<CheatDetectionSignal>()
                .FindAsync(s => s.SessionId == sessionId, ct)).ToList();
            decimal cheatScore = 0;
            foreach (var s in signals)
            {
                cheatScore += CheatSignalWeights.TryGetValue(s.SignalType, out var w) ? w.Weight : 5m;
            }
            cheatScore = Math.Min(100m, cheatScore);

            // Gộp theo loại để HR đọc nhanh: "Thoát toàn màn hình × 3".
            var cheatSignalsJson = System.Text.Json.JsonSerializer.Serialize(
                signals.GroupBy(s => s.SignalType).Select(g => new
                {
                    type = g.Key,
                    severity = CheatSignalWeights.TryGetValue(g.Key, out var w) ? w.Severity : "low",
                    description = $"{g.Count()} lần",
                    timestamp = g.Max(x => x.RecordedAt)
                }));

            // Language Assessment — CHỈ chấm khi thực sự có câu trả lời để chấm; không có dữ liệu
            // thì bỏ trống thay vì để AI đoán bừa một bậc năng lực (ADR-051).
            LanguageAssessment? langAssess = null;
            var hasAnswers = chatHistory.Any(qa => !string.IsNullOrWhiteSpace(qa.AnswerText));
            if (!string.IsNullOrEmpty(jobPosting.DetectedLanguage) && hasAnswers)
            {
                langAssess = await _aiProvider.AssessLanguageProficiencyAsync(evalCtx, ct);
            }

            var evaluation = new Evaluation
            {
                SessionId = sessionId,
                ApplicationId = application.Id,
                RoundNumber = session.RoundNumber,
                SessionType = session.SessionType,
                AiVerdict = evalReport.Verdict,
                OverallScore = evalReport.Score,
                CriterionScores = evalReport.CriterionScoresJson,
                Reasoning = evalReport.Reasoning,
                RecommendedNextStep = evalReport.RecommendedNextStep,
                QuestionAnalyses = evalReport.QuestionAnalysesJson,
                CheatScore = cheatScore,
                CheatSignals = cheatSignalsJson,
                LanguageAssessment = langAssess != null
                    ? System.Text.Json.JsonSerializer.Serialize(new
                    {
                        language = evalCtx.Language,
                        fluency = langAssess.Fluency,
                        grammar = langAssess.Grammar,
                        vocabulary = langAssess.Vocabulary,
                        comprehension = langAssess.Comprehension,
                        overall_score = langAssess.OverallScore,
                        cefr_level = langAssess.CefrLevel,
                        language_adherence = langAssess.LanguageAdherence,
                        evidence = langAssess.Evidence
                    })
                    : null
            };

            await _unitOfWork.Repository<Evaluation>().AddAsync(evaluation, ct);

            // AI KHÔNG tự đổi trạng thái hồ sơ (ADR-053). Trước đây AI chấm "not_pass" là hồ sơ bị
            // đánh rớt ngay trước khi HR kịp xem — trái Phase 6 "HR Review & Confirm". Nay hồ sơ giữ
            // nguyên "interview" cho tới khi HR xác nhận; FE hiện "chờ HR xác nhận" qua pendingHrReview.
            // Buổi thử thì còn không báo HR (ADR-051).
            var isRealSession = session.SessionType == "real";

            await _unitOfWork.SaveChangesAsync(ct);

            if (isRealSession)
            {
                // Notify HR Admin that there is a new evaluation to review
                await _notificationService.PublishGroupEventAsync("hr_admin", "ReceiveSystemEvent", new {
                    Type = "AiEvaluationComplete",
                    EvaluationId = evaluation.Id,
                    ApplicationId = application.Id
                }, ct);
            }
        }

        public async Task<Result<bool>> SubmitHrReviewAsync(Guid hrUserId, ConfirmReviewRequest request, string? frontendBaseUrl = null, CancellationToken ct = default)
        {
            var evaluation = await _unitOfWork.Repository<Evaluation>().GetByIdAsync(request.EvaluationId, ct);
            if (evaluation == null)
                return Result.Failure<bool>("Evaluation report not found.");

            // Validate User & Role for Override actions
            var hrUser = await _unitOfWork.Repository<User>().GetByIdAsync(hrUserId, ct);
            if (hrUser == null)
                return Result.Failure<bool>("HR User not found.");

            bool isOverride = evaluation.AiVerdict != request.FinalVerdict;
            if (isOverride)
            {
                bool isAuthorized = string.Equals(hrUser.Role, AppRoles.HrAdmin, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(hrUser.Role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(hrUser.Role, "hr_admin", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(hrUser.Role, "super_admin", StringComparison.OrdinalIgnoreCase);

                if (!isAuthorized)
                    return Result.Failure<bool>("Only HR Admin or Super Admin can override AI verdict.");

                if (string.IsNullOrEmpty(request.OverrideReason))
                    return Result.Failure<bool>("Override reason is mandatory when changing the AI verdict.");
            }

            var review = new HrReview
            {
                EvaluationId = evaluation.Id,
                ReviewedByUserId = hrUserId,
                FinalVerdict = request.FinalVerdict,
                IsOverride = isOverride,
                OverrideReason = request.OverrideReason,
                ShareRecording = request.ShareRecording,
                ShareTranscript = request.ShareTranscript,
                ShareEvaluation = request.ShareEvaluation,
                ShareFeedback = request.ShareFeedback,
                CandidateFeedback = request.CandidateFeedback
            };

            await _unitOfWork.Repository<HrReview>().AddAsync(review, ct);

            // Update Application status based on final verdict
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(evaluation.ApplicationId, ct);
            if (application != null)
            {
                // Buổi THỬ không chạm pipeline tuyển dụng, kể cả khi có ai đó review nó (ADR-051).
                // "Đạt" CHỈ khi đã qua vòng CUỐI của job (ADR-053): trước đây HR xác nhận pass ở
                // vòng bất kỳ là hồ sơ thành "pass" ngay, rồi mới bị TriggerAutoProgressionAsync ghi
                // đè về "interview" — job không khai báo round config thì không có gì ghi đè nên
                // ứng viên mới xong vòng 1 đã hiện "Đạt".
                if (evaluation.SessionType == "real")
                {
                    var totalRounds = await ResolveTotalRoundsAsync(application.JobPostingId, ct);
                    var isFinalRound = evaluation.RoundNumber >= totalRounds;
                    application.Status = request.FinalVerdict != "pass"
                        ? "not_pass"
                        : (isFinalRound ? "pass" : "interview");
                    _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);
                }

                var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
                var jobTitle = jobPosting?.Title ?? "vị trí ứng tuyển";

                bool hasProgressed = false;
                // Auto-Progression Logic to Round N+1 (ADR-017 / ADR-014)
                if (request.FinalVerdict == "pass" && evaluation.SessionType == "real")
                {
                    hasProgressed = await TriggerAutoProgressionAsync(application, evaluation.RoundNumber, frontendBaseUrl, ct);
                }

                // Auto-send email to Candidate if not progressed
                if (!hasProgressed)
                {
                    // Link trong thư phải trỏ về portal thật của môi trường đang chạy, không phải máy dev.
                    var portalBase = (string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:3000" : frontendBaseUrl).TrimEnd('/');
                    string emailBody;
                    string subject;
                    if (request.FinalVerdict == "pass")
                    {
                        subject = "ARISP - Chúc mừng bạn đã vượt qua vòng phỏng vấn!";
                        emailBody = $$"""
                            <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);">
                                <div style="background: linear-gradient(135deg, #059669, #10b981); padding: 24px; text-align: center; color: white;">
                                    <h2 style="margin: 0; font-size: 20px; font-weight: 600; letter-spacing: 0.5px;">THƯ CHÚC MỪNG VƯỢT QUA VÒNG PHỎNG VẤN</h2>
                                </div>
                                <div style="padding: 32px 24px; background-color: #ffffff; color: #334155; line-height: 1.6;">
                                    <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                                    <p>Chúng tôi vô cùng vui mừng thông báo rằng Anh/Chị đã chính thức vượt qua các vòng đánh giá năng lực của vị trí tuyển dụng <strong>{{jobTitle}}</strong> tại ARISP.</p>
                                    <p>Đội ngũ tuyển dụng đánh giá rất cao năng lực chuyên môn, phong cách làm việc cũng như sự phù hợp của Anh/Chị với định hướng phát triển của chúng tôi.</p>
                                    <p>Đại diện bộ phận Nhân sự (HR) sẽ liên hệ trực tiếp với Anh/Chị trong vòng 1-2 ngày làm việc tới để trao đổi chi tiết về kế hoạch công việc, mức đãi ngộ và gửi Thư mời nhận việc chính thức (Offer Letter).</p>
                                    <p>Cảm ơn Anh/Chị đã luôn dành sự quan tâm và nỗ lực trong suốt hành trình tuyển dụng cùng ARISP.</p>
                                    <div style="text-align: center; margin: 30px 0;">
                                        <a href="{{portalBase}}/candidate/applications/{{application.Id}}" style="background-color: #059669; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block; box-shadow: 0 4px 6px rgba(5,150,105,0.2);">Xem kết quả chi tiết</a>
                                    </div>
                                    <p style="margin-bottom: 0;">Trân trọng,<br><strong>Trưởng Ban Tuyển Dụng ARISP</strong></p>
                                </div>
                                <div style="background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;">
                                    <p style="margin: 0;">Đây là thư điện tử tự động từ hệ thống ARISP. Vui lòng không trả lời trực tiếp thư này.</p>
                                </div>
                            </div>
                            """;
                    }
                    else
                    {
                        subject = "ARISP - Thư cảm ơn tham gia phỏng vấn";
                        emailBody = $$"""
                            <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);">
                                <div style="background: linear-gradient(135deg, #4b5563, #6b7280); padding: 24px; text-align: center; color: white;">
                                    <h2 style="margin: 0; font-size: 20px; font-weight: 600; letter-spacing: 0.5px;">THƯ CẢM ƠN THAM GIA PHỎNG VẤN</h2>
                                </div>
                                <div style="padding: 32px 24px; background-color: #ffffff; color: #334155; line-height: 1.6;">
                                    <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                                    <p>Đội ngũ tuyển dụng ARISP chân thành cảm ơn Anh/Chị đã dành thời gian và tâm huyết tham gia quy trình ứng tuyển vào vị trí <strong>{{jobTitle}}</strong>.</p>
                                    <p>Sau khi cân nhắc kỹ lưỡng dựa trên kết quả phỏng vấn và so sánh với định hướng hiện tại của vị trí, chúng tôi rất tiếc phải thông báo rằng chưa thể đồng hành cùng Anh/Chị trong dự án lần này.</p>
                                    <p>Hồ sơ năng lực của Anh/Chị sẽ được lưu trữ bảo mật trong Cơ sở dữ liệu ứng viên tiềm năng của ARISP. Chúng tôi sẽ chủ động liên hệ ngay khi có những cơ hội nghề nghiệp mới phù hợp hơn với thế mạnh của Anh/Chị.</p>
                                    <div style="text-align: center; margin: 30px 0;">
                                        <a href="{{portalBase}}/candidate/applications/{{application.Id}}" style="background-color: #4b5563; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block; box-shadow: 0 4px 6px rgba(75,85,99,0.2);">Xem thông tin hồ sơ</a>
                                    </div>
                                    <p>Chúc Anh/Chị luôn dồi dào sức khỏe, may mắn và gặt hái được nhiều thành công rực rỡ trên con đường sự nghiệp sắp tới.</p>
                                    <p style="margin-bottom: 0;">Trân trọng,<br><strong>Ban Tuyển Dụng ARISP</strong></p>
                                </div>
                                <div style="background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;">
                                    <p style="margin: 0;">Đây là thư điện tử tự động từ hệ thống ARISP. Vui lòng không trả lời trực tiếp thư này.</p>
                                </div>
                            </div>
                            """;
                    }

                    await _notificationService.SendEmailAsync(application.CandidateEmail, subject, emailBody, ct);
                }

                // Notify candidate in real-time
                if (application.CandidateAccountId.HasValue)
                {
                    await _notificationService.PublishUserEventAsync(application.CandidateAccountId.Value, "ReceiveApplicationStatusUpdate", new { 
                        Id = application.Id, 
                        JobPostingId = application.JobPostingId, 
                        Status = application.Status 
                    }, ct);

                    // Add Notification record
                    var notifRepo = _unitOfWork.Repository<ARI.Domain.Entities.Notification>();
                    var dedupKey = $"hr_review:{evaluation.Id}";
                    var existingNotifs = await notifRepo.FindAsync(n => n.CandidateAccountId == application.CandidateAccountId.Value && n.DedupKey == dedupKey, ct);
                    if (existingNotifs.FirstOrDefault() == null)
                    {
                        var verdict = isOverride ? request.FinalVerdict : evaluation.AiVerdict;
                        var isPass = verdict == "pass";
                        var newNotif = new ARI.Domain.Entities.Notification
                        {
                            CandidateAccountId = application.CandidateAccountId.Value,
                            DedupKey = dedupKey,
                            Type = "result",
                            Title = "Kết quả phỏng vấn",
                            Body = isPass ? "Chúc mừng bạn đã vượt qua vòng phỏng vấn!" : "Rất tiếc, bạn chưa phù hợp với vị trí này.",
                            Link = $"/candidate/applications/{application.Id}",
                            IsRead = false
                        };
                        await notifRepo.AddAsync(newNotif, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }

                    // Trigger bell update
                    await _notificationService.PublishUserEventAsync(application.CandidateAccountId.Value, "ReceiveUserNotification", new { Type = "InterviewResult" }, ct);
                }
            }

            // Save Audit Log (mandatory for confirm/override actions)
            var auditLog = new AuditLog
            {
                ActorUserId = hrUserId,
                Action = isOverride ? "hr_override" : "hr_confirm",
                EntityType = "evaluation",
                EntityId = evaluation.Id,
                Metadata = $"{{\"evaluation_id\":\"{evaluation.Id}\",\"final_verdict\":\"{request.FinalVerdict}\",\"is_override\":{isOverride.ToString().ToLower()}}}"
            };
            await _unitOfWork.Repository<AuditLog>().AddAsync(auditLog, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success(true);
        }

        private async Task<bool> TriggerAutoProgressionAsync(ARI.Domain.Entities.Application application, int currentRoundNumber, string? frontendBaseUrl = null, CancellationToken ct = default)
        {
            var nextRoundNumber = currentRoundNumber + 1;

            // Check if there is configured round configs for N+1
            var nextRoundConfigs = await _unitOfWork.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == application.JobPostingId && r.RoundNumber == nextRoundNumber, ct);

            if (nextRoundConfigs.Any())
            {
                var nextRound = nextRoundConfigs.First();
                // Status = interview (đang trong giai đoạn phỏng vấn vòng kế); chi tiết vòng suy ra từ record.
                application.Status = "interview";
                _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);

                // Tạo lời mời CHỌN LỊCH cho vòng kế (mỗi vòng cần duyệt → chỉ tạo sau khi confirm Pass).
                var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
                var ttlHours = job?.InviteTokenTtlHours is { } h && h > 0 ? h : 48;
                var baseUrl = (string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:3000" : frontendBaseUrl).TrimEnd('/');
                // Chỉ để thoả cột TokenHash (NOT NULL) — không dòng nào còn đối chiếu giá trị này.
                var rawToken = Guid.NewGuid().ToString("N");

                var oldInvites = await _unitOfWork.Repository<InterviewInvite>()
                    .FindAsync(i => i.ApplicationId == application.Id && i.RoundNumber == nextRoundNumber && i.ScheduledAt == null, ct);
                foreach (var old in oldInvites)
                    _unitOfWork.Repository<InterviewInvite>().Delete(old);

                await _unitOfWork.Repository<InterviewInvite>().AddAsync(new InterviewInvite
                {
                    ApplicationId = application.Id,
                    RoundNumber = nextRoundNumber,
                    TokenHash = TokenHashing.Sha256Hex(rawToken),
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(ttlHours),
                }, ct);

                // ADR-048/059: ứng viên KHÔNG tự chọn giờ nữa — chỉ dẫn về Portal để theo dõi, thư mời
                // kèm giờ hẹn sẽ do bước nhân sự xếp lịch gửi riêng. Token invite không phát ra ngoài.
                var portalLink = $"{baseUrl}/candidate/applications/{application.Id}";

                var emailBody = $$"""
                    <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);">
                        <div style="background: linear-gradient(135deg, #1e3a8a, #2563eb); padding: 24px; text-align: center; color: white;">
                            <h2 style="margin: 0; font-size: 20px; font-weight: 600; letter-spacing: 0.5px;">HỆ THỐNG TUYỂN DỤNG THÔNG MINH ARISP</h2>
                        </div>
                        <div style="padding: 32px 24px; background-color: #ffffff; color: #334155; line-height: 1.6;">
                            <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                            <p>Chúc mừng Anh/Chị đã hoàn thành xuất sắc vòng phỏng vấn số <strong>{{currentRoundNumber}}</strong>.</p>
                            <p>Đội ngũ tuyển dụng ARISP trân trọng kính mời Anh/Chị tiếp tục tham gia <strong>Vòng phỏng vấn số {{nextRoundNumber}}</strong>.</p>
                            <p><strong>Bộ phận nhân sự sẽ xếp lịch vòng {{nextRoundNumber}}</strong> và gửi Anh/Chị một thư mời riêng kèm <strong>giờ hẹn cụ thể và địa điểm</strong>. Trong thư đó, Anh/Chị bấm xác nhận tham dự hoặc báo bận để được xếp khung giờ khác.</p>
                            <div style="margin: 30px 0; text-align: center;">
                                <a href="{{portalLink}}" style="background-color: #2563eb; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block; box-shadow: 0 4px 6px rgba(37,99,235,0.2);">Xem tiến trình hồ sơ</a>
                            </div>
                            <p>Nếu gặp bất kỳ khó khăn hoặc cần hỗ trợ kỹ thuật, xin vui lòng phản hồi trực tiếp email này hoặc liên hệ bộ phận hỗ trợ tuyển dụng.</p>
                            <p style="margin-bottom: 0;">Trân trọng,<br><strong>Ban Tuyển Dụng ARISP</strong></p>
                        </div>
                        <div style="background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;">
                            <p style="margin: 0;">Đây là thư điện tử tự động từ hệ thống ARISP. Vui lòng không trả lời trực tiếp thư này.</p>
                        </div>
                    </div>
                    """;

                // Send email invite
                await _notificationService.SendEmailAsync(
                    application.CandidateEmail,
                    $"ARISP - Mời bạn tham gia vòng phỏng vấn số {nextRoundNumber}",
                    emailBody,
                    ct
                );
                return true;
            }
            return false;
        }
    }
}
