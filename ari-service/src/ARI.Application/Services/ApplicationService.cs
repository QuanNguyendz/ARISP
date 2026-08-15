using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Services
{
    public class ApplicationService : IApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRagIngestionService _ragIngestion;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;

        // Cache key cho danh sách toàn bộ ứng tuyển (HR view).
        private const string AllApplicationsCacheKey = "applications:all";

        // Define valid status transitions in a static dictionary
        private static readonly Dictionary<string, HashSet<string>> AllowedStatusTransitions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "invited", new(StringComparer.OrdinalIgnoreCase) { "cv_submitted", "withdrawn" } },
            { "cv_submitted", new(StringComparer.OrdinalIgnoreCase) { "screening", "withdrawn" } },
            { "screening", new(StringComparer.OrdinalIgnoreCase) { "interview", "not_pass", "withdrawn" } },
            { "interview", new(StringComparer.OrdinalIgnoreCase) { "pass", "not_pass", "withdrawn" } },
            { "pass", new(StringComparer.OrdinalIgnoreCase) { "withdrawn" } },
            { "not_pass", new(StringComparer.OrdinalIgnoreCase) { "screening", "interview" } },
            { "withdrawn", new(StringComparer.OrdinalIgnoreCase) } // terminal state
        };

        public ApplicationService(
            IUnitOfWork unitOfWork,
            IRagIngestionService ragIngestion,
            IEmailService emailService,
            INotificationService notificationService,
            Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
            IMemoryCache cache)
        {
            _unitOfWork = unitOfWork;
            _ragIngestion = ragIngestion;
            _emailService = emailService;
            _notificationService = notificationService;
            _scopeFactory = scopeFactory;
            _cache = cache;
        }

        /// <summary>
        /// Hàm tiện ích dùng chung để Map Entity sang Response (Tránh lặp code)
        /// </summary>
        private static ApplicationResponse MapToResponse(ARI.Domain.Entities.Application application, JobPosting? jobPosting, int? currentRound = null, decimal? interviewScore = null, DateTimeOffset? interviewDate = null)
        {
            return new ApplicationResponse
            {
                Id = application.Id,
                JobPostingId = application.JobPostingId,
                JobTitle = jobPosting?.Title ?? "Unknown Job",
                CandidateEmail = application.CandidateEmail,
                CandidateName = application.CandidateName,
                CandidatePhone = application.CandidatePhone,
                CvFileUrl = application.CvFileUrl,
                CvText = application.CvText,
                Source = application.Source,
                Status = application.Status,
                PracticeSessionUsed = application.PracticeSessionUsed,
                CreatedAt = application.CreatedAt,
                CvJdAnalysisId = application.CvJdAnalysisId,
                CurrentRound = currentRound,
                CoverLetter = application.CoverLetter,
                NoticePeriod = application.NoticePeriod,
                InterviewScore = interviewScore,
                InterviewDate = interviewDate,
                MatchScore = application.CvJdAnalysis?.MatchScore,
                CvJdSummary = application.CvJdAnalysis?.Summary
            };
        }

        public async Task<Result<ApplicationResponse>> SubmitApplicationAsync(SubmitApplicationRequest request, string source = "invited", CancellationToken ct = default)
        {
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(request.JobPostingId, ct);
            if (jobPosting == null)
                return Result.Failure<ApplicationResponse>("Job posting not found.");

            if (jobPosting.Status != "active")
                return Result.Failure<ApplicationResponse>("Tin tuyển dụng này hiện không hoạt động hoặc đã bị đóng.");

            if (jobPosting.ApplicationDeadline.HasValue && jobPosting.ApplicationDeadline.Value <= DateTimeOffset.UtcNow)
                return Result.Failure<ApplicationResponse>("Tin tuyển dụng này đã hết hạn nộp hồ sơ.");

            var application = new ARI.Domain.Entities.Application
            {
                JobPostingId = request.JobPostingId,
                CandidateAccountId = request.CandidateAccountId,
                CandidateEmail = request.CandidateEmail,
                CandidateName = request.CandidateName,
                CandidatePhone = request.CandidatePhone,
                CvFileUrl = request.CvFileUrl,
                CvText = request.CvText,
                CoverLetter = request.CoverLetter,
                NoticePeriod = request.NoticePeriod,
                Source = source,
                Status = "cv_submitted"
            };

            // Auto-link CvJdAnalysis if it exists
            if (!string.IsNullOrEmpty(request.CvFileHash))
            {
                var analyses = await _unitOfWork.Repository<CvJdAnalysis>()
                    .FindAsync(x => x.JobPostingId == request.JobPostingId && x.CvHash == request.CvFileHash, ct);
                var analysis = System.Linq.Enumerable.FirstOrDefault(analyses);
                if (analysis != null)
                {
                    application.CvJdAnalysisId = analysis.Id;
                }
            }

            await _unitOfWork.Repository<ARI.Domain.Entities.Application>().AddAsync(application, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            _cache.Remove(AllApplicationsCacheKey); // xóa cache để lần load tiếp theo lấy dữ liệu mới nhất

            // Auto-trigger background CV-JD analysis if it does not already exist
            if (application.CvJdAnalysisId == null && !string.IsNullOrEmpty(application.CvFileUrl))
            {
                var fileUrl = application.CvFileUrl;
                var extension = System.IO.Path.GetExtension(fileUrl).ToLower();
                var isTestOrDummy = fileUrl.Contains("test", StringComparison.OrdinalIgnoreCase) || 
                                    fileUrl.Contains("dummy", StringComparison.OrdinalIgnoreCase) || 
                                    fileUrl.Contains("mock", StringComparison.OrdinalIgnoreCase) || 
                                    fileUrl.Contains("example", StringComparison.OrdinalIgnoreCase);
                var isValidExtension = extension == ".pdf" || extension == ".docx" || extension == ".doc";

                if (!isTestOrDummy && isValidExtension)
                {
                    var appId = application.Id;
                    var jobId = application.JobPostingId;
                    var hash = request.CvFileHash;
                    
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                        using var scope = _scopeFactory.CreateScope();
                        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                        var cvJdSvc = scope.ServiceProvider.GetRequiredService<CvJdAnalysisService>();
                        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        
                        var bytes = await storage.ReadAllBytesAsync(fileUrl);
                        if (bytes != null && bytes.Length > 0)
                        {
                            using var ms = new System.IO.MemoryStream(bytes);
                            var fileName = System.IO.Path.GetFileName(fileUrl);
                            var analysisResult = await cvJdSvc.AnalyzeAndCacheAsync(jobId, ms, fileName, CancellationToken.None);
                            if (!analysisResult.IsFailure)
                            {
                                var appRepo = uow.Repository<ARI.Domain.Entities.Application>();
                                var app = await appRepo.GetByIdAsync(appId);
                                if (app != null)
                                {
                                    app.CvJdAnalysisId = analysisResult.Value.Id;
                                    await uow.SaveChangesAsync();
                                    
                                    // Notify candidate and recruiters that background analysis completed
                                    var notifSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                                    if (app.CandidateAccountId.HasValue)
                                    {
                                        await notifSvc.PublishUserEventAsync(app.CandidateAccountId.Value, "ReceiveUserNotification", new { Type = "AiAnalysisComplete" }, CancellationToken.None);
                                    }
                                    
                                    if (jobPosting != null)
                                    {
                                        var responseDto = MapToResponse(app, jobPosting);
                                        await notifSvc.PublishUserEventAsync(jobPosting.CreatedByUserId, "ReceiveApplicationStatusUpdate", responseDto, CancellationToken.None);
                                        await notifSvc.PublishGroupEventAsync("hr_admin", "ReceiveApplicationStatusUpdate", responseDto, CancellationToken.None);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Background task must not crash
                    }
                });
            }
        }

            // Chunk + embed + lưu pgvector CV — do RAG service (Python) sở hữu (ADR-039).
            if (!string.IsNullOrEmpty(request.CvText))
            {
                await _ragIngestion.IngestAsync("cv", application.Id, request.CvText!, ct: ct);
            }

            var response = new ApplicationResponse
            {
                Id = application.Id,
                JobPostingId = application.JobPostingId,
                JobTitle = jobPosting?.Title ?? "Unknown Job",
                CandidateEmail = application.CandidateEmail,
                CandidateName = application.CandidateName,
                CandidatePhone = application.CandidatePhone,
                CvFileUrl = application.CvFileUrl,
                CvText = application.CvText,
                Source = application.Source,
                Status = application.Status,
                PracticeSessionUsed = application.PracticeSessionUsed,
                CreatedAt = application.CreatedAt,
                CvJdAnalysisId = application.CvJdAnalysisId
            };

            // Trigger Realtime Notifications
            // For real-time toast, fetch settings if possible. 
            // In GetStaffNotificationsQuery, we already sync DB notifications.
            
            var recruiter = await _unitOfWork.Repository<User>().GetByIdAsync(jobPosting.CreatedByUserId, ct);
            var recSettings = recruiter != null && !string.IsNullOrEmpty(recruiter.SettingsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<ARI.Application.DTOs.StaffSettingsDto>(recruiter.SettingsJson) ?? new ARI.Application.DTOs.StaffSettingsDto()
                : new ARI.Application.DTOs.StaffSettingsDto();

            if (recSettings.ReceivePush)
            {
                await _notificationService.PublishUserEventAsync(jobPosting.CreatedByUserId, "ReceiveNewApplication", response, ct);
            }
            
            // Note: Since hr_admin group push goes to all hr_admins, we can't easily filter by individual push settings here for the realtime toast.
            // But GetStaffNotificationsQuery will respect their push settings when generating DB notifications.
            await _notificationService.PublishGroupEventAsync("hr_admin", "ReceiveNewApplication", response, ct);

            // Notify the candidate themselves if they are logged in (self-applied)
            if (request.CandidateAccountId.HasValue)
            {
                var candidateAccount = await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(request.CandidateAccountId.Value, ct);
                var settings = candidateAccount != null && !string.IsNullOrEmpty(candidateAccount.SettingsJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<ARI.Application.DTOs.CandidateSettingsDto>(candidateAccount.SettingsJson) ?? new ARI.Application.DTOs.CandidateSettingsDto()
                    : new ARI.Application.DTOs.CandidateSettingsDto();

                if (settings.ApplicationUpdate.Push)
                {
                    // Trigger application status update
                    await _notificationService.PublishUserEventAsync(request.CandidateAccountId.Value, "ReceiveApplicationStatusUpdate", response, ct);
                    
                    // Add a Notification record so it shows up in the bell
                    var notifRepo = _unitOfWork.Repository<ARI.Domain.Entities.Notification>();
                    var dedupKey = $"applied:{application.Id}";
                    var existingNotifs = await notifRepo.FindAsync(n => n.CandidateAccountId == request.CandidateAccountId.Value && n.DedupKey == dedupKey, ct);
                    if (existingNotifs.FirstOrDefault() == null)
                    {
                        var newNotif = new ARI.Domain.Entities.Notification
                        {
                            CandidateAccountId = request.CandidateAccountId.Value,
                            DedupKey = dedupKey,
                            Type = "applied",
                            Title = "Ứng tuyển thành công",
                            Body = $"Bạn đã nộp hồ sơ thành công vào vị trí {jobPosting?.Title}.",
                            Link = $"/candidate/applications/{application.Id}",
                            IsRead = false
                        };
                        await notifRepo.AddAsync(newNotif, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }

                    // Trigger bell update
                    await _notificationService.PublishUserEventAsync(request.CandidateAccountId.Value, "ReceiveUserNotification", new { Type = "ApplicationSubmitted" }, ct);
                }
            }

            var emailCandidateAccount = application.CandidateAccountId.HasValue 
                ? await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(application.CandidateAccountId.Value, ct)
                : null;
            var emailSettings = emailCandidateAccount != null && !string.IsNullOrEmpty(emailCandidateAccount.SettingsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<ARI.Application.DTOs.CandidateSettingsDto>(emailCandidateAccount.SettingsJson) ?? new ARI.Application.DTOs.CandidateSettingsDto()
                : new ARI.Application.DTOs.CandidateSettingsDto();

            if (emailSettings.ApplicationUpdate.Email)
            {
                var candidateSubject = $"[ARISP] - Xác nhận nộp hồ sơ ứng tuyển vị trí {jobPosting?.Title}";
                var candidateHtmlMessage = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
            <h2 style='color: #4f46e5; margin-top: 0;'>Xác nhận nộp hồ sơ ứng tuyển</h2>
            <p style='color: #475569; font-size: 15px;'>Chào <strong>{application.CandidateName}</strong>,</p>
            <p style='color: #475569; font-size: 15px;'>Chúc mừng bạn đã nộp hồ sơ ứng tuyển thành công vào vị trí <strong>{jobPosting?.Title}</strong>.</p>
            <p style='color: #475569; font-size: 15px;'>Hồ sơ của bạn đã được chuyển tới bộ phận Tuyển dụng của chúng tôi. Chúng tôi sẽ xem xét và phản hồi lại cho bạn trong thời gian sớm nhất.</p>
            <p style='color: #475569; font-size: 15px;'>Bạn có thể theo dõi trạng thái hồ sơ của mình trực tiếp trên Candidate Portal:</p>
            <div style='text-align: center; margin: 28px 0;'>
                <a href='http://localhost:3000/candidate/applications/{application.Id}' style='background-color: #4f46e5; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>Xem hồ sơ ứng tuyển</a>
            </div>
            <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;' />
            <p style='color: #94a3b8; font-size: 13px; margin: 0;'>Thư điện tử tự động từ Đội ngũ nhân sự ARISP.</p>
        </div>";
                try { await _emailService.SendEmailAsync(application.CandidateEmail, candidateSubject, candidateHtmlMessage); } catch { }
            }

            return Result.Success(response);
        }

        public async Task<Result<List<ApplicationResponse>>> GetAllApplicationsAsync(CancellationToken ct = default)
        {
            // Cache 60 giây để tránh truy vấn 7-8 lần liên tiếp tới Supabase (mỗi RTT ~100-150ms).
            // Cache bị xóa tự động khi có submit/update/reject/accept ứng tuyển.
            if (_cache.TryGetValue(AllApplicationsCacheKey, out List<ApplicationResponse>? cached) && cached != null)
                return Result.Success(cached);

            // Projection ở tầng SQL — KHÔNG kéo cột text lớn (CvText/CoverLetter/DemographicData)
            // vốn khiến danh sách rất nặng và timeout khi đọc stream từ Postgres.
            var applications = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => q
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new AppListProjection
                    {
                        Id = a.Id,
                        JobPostingId = a.JobPostingId,
                        CandidateEmail = a.CandidateEmail,
                        CandidateName = a.CandidateName,
                        CandidatePhone = a.CandidatePhone,
                        CvFileUrl = a.CvFileUrl,
                        Source = a.Source,
                        Status = a.Status,
                        PracticeSessionUsed = a.PracticeSessionUsed,
                        CreatedAt = a.CreatedAt,
                        CvJdAnalysisId = a.CvJdAnalysisId,
                        CoverLetter = a.CoverLetter,
                        NoticePeriod = a.NoticePeriod
                    }), ct);

            var result = await MapApplicationsAsync(applications, null, ct);

            _cache.Set(AllApplicationsCacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
                SlidingExpiration = null
            });

            return Result.Success(result);
        }

        /// <summary>Cột nhẹ cho danh sách ứng viên (không gồm text lớn).</summary>
        private sealed class AppListProjection
        {
            public Guid Id { get; set; }
            public Guid JobPostingId { get; set; }
            public string CandidateEmail { get; set; } = string.Empty;
            public string CandidateName { get; set; } = string.Empty;
            public string? CandidatePhone { get; set; }
            public string? CvFileUrl { get; set; }
            public string Source { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public bool PracticeSessionUsed { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public Guid? CvJdAnalysisId { get; set; }
            public string? CoverLetter { get; set; }
            public string? NoticePeriod { get; set; }
        }

        private sealed class BookingProjection
        {
            public Guid ApplicationId { get; set; }
            public int RoundNumber { get; set; }
            public Guid AvailabilitySlotId { get; set; }
        }

        /// <summary>
        /// Map danh sách projection → ApplicationResponse (CvText = null cho list).
        /// JobTitle lấy từ <paramref name="jobTitleOverride"/> nếu cùng 1 job, ngược lại batch query tiêu đề.
        /// </summary>
        private async Task<List<ApplicationResponse>> MapApplicationsAsync(
            List<AppListProjection> apps, string? jobTitleOverride, CancellationToken ct)
        {
            if (apps == null || apps.Count == 0) return new List<ApplicationResponse>();

            var appIds = apps.Select(a => a.Id).ToList();
            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
            var analysisIds = apps.Where(a => a.CvJdAnalysisId.HasValue).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var candidateEmails = apps
                .Where(a => !string.IsNullOrWhiteSpace(a.CandidateEmail))
                .Select(a => a.CandidateEmail.Trim())
                .Distinct()
                .ToList();
            var candidateEmailsLower = candidateEmails.Select(e => e.ToLower()).Distinct().ToList();

            var candidatesTask = Task.Run(async () =>
            {
                var dict = new Dictionary<string, CandidateAccount>();
                try
                {
                    if (candidateEmails.Count == 0) return dict;
                    using var scope = _scopeFactory.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var allVariations = candidateEmails.Concat(candidateEmailsLower).Distinct().ToList();

                    var rawCandidates = await uow.Repository<CandidateAccount>()
                        .QueryAsync(q => q.Where(c => allVariations.Contains(c.Email)).Select(c => new
                        {
                            c.Id, c.Email, c.FullName, c.Headline, c.About,
                            c.Location, c.DateOfBirth, c.SettingsJson,
                            c.LinkedinUrl, c.GithubUrl, c.PortfolioUrl,
                            c.SkillsJson, c.ExperienceJson, c.EducationJson
                        }), ct);

                    foreach (var group in rawCandidates.Where(c => !string.IsNullOrEmpty(c.Email)).GroupBy(c => c.Email.Trim().ToLower()))
                    {
                        var first = group.First();
                        dict[group.Key] = new CandidateAccount
                        {
                            Id = first.Id, Email = first.Email, FullName = first.FullName, Headline = first.Headline, About = first.About,
                            Location = first.Location, DateOfBirth = first.DateOfBirth, SettingsJson = first.SettingsJson,
                            LinkedinUrl = first.LinkedinUrl, GithubUrl = first.GithubUrl, PortfolioUrl = first.PortfolioUrl,
                            SkillsJson = first.SkillsJson, ExperienceJson = first.ExperienceJson, EducationJson = first.EducationJson
                        };
                    }
                }
                catch { }
                return dict;
            });

            var jobTask = Task.Run(async () =>
            {
                var dict = new Dictionary<Guid, string>();
                try
                {
                    if (jobTitleOverride != null || jobIds.Count == 0) return dict;
                    using var scope = _scopeFactory.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var jobs = await uow.Repository<JobPosting>()
                        .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id)).Select(j => new { j.Id, j.Title }), ct);
                    foreach (var j in jobs) dict[j.Id] = j.Title;
                }
                catch { }
                return dict;
            });

            var analysisTask = Task.Run(async () =>
            {
                var dict = new Dictionary<Guid, (int MatchScore, string Summary)>();
                try
                {
                    if (analysisIds.Count == 0) return dict;
                    using var scope = _scopeFactory.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var list = await uow.Repository<CvJdAnalysis>()
                        .QueryAsync(q => q.Where(c => analysisIds.Contains(c.Id)).Select(c => new { c.Id, c.MatchScore, c.Summary }), ct);
                    foreach (var a in list) dict[a.Id] = (a.MatchScore, a.Summary);
                }
                catch { }
                return dict;
            });

            var bookingsTask = Task.Run(async () =>
            {
                var list = new List<BookingProjection>();
                try
                {
                    if (appIds.Count == 0) return list;
                    using var scope = _scopeFactory.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    return await uow.Repository<InterviewBooking>()
                        .QueryAsync(q => q
                            .Where(b => appIds.Contains(b.ApplicationId) && b.Status == "scheduled")
                            .Select(b => new BookingProjection { ApplicationId = b.ApplicationId, RoundNumber = b.RoundNumber, AvailabilitySlotId = b.AvailabilitySlotId }), ct);
                }
                catch { return list; }
            });

            var invitesTask = Task.Run(async () =>
            {
                var dict = new Dictionary<Guid, int>();
                try
                {
                    if (appIds.Count == 0) return dict;
                    using var scope = _scopeFactory.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var invites = await uow.Repository<InterviewInvite>()
                        .QueryAsync(q => q.Where(i => appIds.Contains(i.ApplicationId)).Select(i => new { i.ApplicationId, i.RoundNumber }), ct);
                    foreach (var g in invites.GroupBy(i => i.ApplicationId))
                    {
                        dict[g.Key] = g.Max(i => i.RoundNumber);
                    }
                }
                catch { }
                return dict;
            });

            var sessionsTask = Task.Run(async () =>
            {
                var dict = new Dictionary<Guid, int>();
                try
                {
                    if (appIds.Count == 0) return dict;
                    using var scope = _scopeFactory.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var sessions = await uow.Repository<InterviewSession>()
                        .QueryAsync(q => q.Where(s => appIds.Contains(s.ApplicationId)).Select(s => new { s.ApplicationId, s.RoundNumber }), ct);
                    foreach (var g in sessions.GroupBy(s => s.ApplicationId))
                    {
                        dict[g.Key] = g.Max(s => s.RoundNumber);
                    }
                }
                catch { }
                return dict;
            });

            var evalsTask = Task.Run(async () =>
            {
                var dict = new Dictionary<(Guid ApplicationId, int RoundNumber), int?>();
                try
                {
                    if (appIds.Count == 0) return dict;
                    using var scope = _scopeFactory.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var evals = await uow.Repository<Evaluation>()
                        .QueryAsync(q => q
                            .Where(e => appIds.Contains(e.ApplicationId) && e.SessionType == "real")
                            .Select(e => new { e.ApplicationId, e.RoundNumber, e.OverallScore }), ct);
                    foreach (var g in evals.GroupBy(e => (e.ApplicationId, e.RoundNumber)))
                    {
                        var firstScore = g.First().OverallScore;
                        dict[g.Key] = firstScore.HasValue ? Convert.ToInt32(firstScore.Value) : null;
                    }
                }
                catch { }
                return dict;
            });

            await Task.WhenAll(jobTask, analysisTask, bookingsTask, invitesTask, sessionsTask, evalsTask, candidatesTask);

            var jobDict = await jobTask;
            var analysisDataById = await analysisTask;
            var scheduledBookings = await bookingsTask;
            var highestRoundInvites = await invitesTask;
            var highestRoundSessions = await sessionsTask;
            var evalDict = await evalsTask;
            var candidateDictByEmail = await candidatesTask;

            var bookedAppIds = scheduledBookings.Select(b => b.ApplicationId).ToHashSet();
            var bookingSlotLookup = new Dictionary<(Guid ApplicationId, int RoundNumber), Guid>();
            foreach (var g in scheduledBookings.GroupBy(b => (b.ApplicationId, b.RoundNumber)))
            {
                bookingSlotLookup[g.Key] = g.First().AvailabilitySlotId;
            }

            var slotIds2 = bookingSlotLookup.Values.Distinct().ToList();
            var slotStartDict = new Dictionary<Guid, DateTimeOffset>();
            if (slotIds2.Count > 0)
            {
                try
                {
                    var slots = await _unitOfWork.Repository<AvailabilitySlot>()
                        .QueryAsync(q => q.Where(s => slotIds2.Contains(s.Id)).Select(s => new { s.Id, s.StartTime }), ct);
                    foreach (var s in slots) slotStartDict[s.Id] = s.StartTime;
                }
                catch { }
            }

            return apps.Select(app =>
            {
                int? currentRound = null;
                if (app.Status != "cv_submitted" && app.Status != "invited" && app.Status != "cv_rejected")
                {
                    if (app.Status == "screening")
                    {
                        currentRound = 1;
                    }
                    else
                    {
                        var maxInviteRound = highestRoundInvites.TryGetValue(app.Id, out var ir) ? ir : 0;
                        var maxSessionRound = highestRoundSessions.TryGetValue(app.Id, out var sr) ? sr : 0;
                        currentRound = Math.Max(maxInviteRound, maxSessionRound);
                        if (currentRound == 0) currentRound = 1;
                    }
                }

                DateTimeOffset? interviewDate = null;
                if (currentRound.HasValue)
                {
                    if (bookingSlotLookup.TryGetValue((app.Id, currentRound.Value), out var slotId2)
                        && slotStartDict.TryGetValue(slotId2, out var startTime))
                    {
                        interviewDate = startTime;
                    }
                }

                var resp = new ApplicationResponse
                {
                    Id = app.Id,
                    JobPostingId = app.JobPostingId,
                    JobTitle = jobTitleOverride ?? (jobDict.TryGetValue(app.JobPostingId, out var title) ? title : "Unknown Job"),
                    CandidateEmail = app.CandidateEmail,
                    CandidateName = app.CandidateName,
                    CandidatePhone = app.CandidatePhone,
                    CvFileUrl = app.CvFileUrl,
                    CvText = null, // danh sách không trả CvText (xem chi tiết ở GetApplicationById)
                    Source = app.Source,
                    Status = app.Status,
                    PracticeSessionUsed = app.PracticeSessionUsed,
                    CreatedAt = app.CreatedAt,
                    CvJdAnalysisId = app.CvJdAnalysisId,
                    MatchScore = app.CvJdAnalysisId.HasValue && analysisDataById.TryGetValue(app.CvJdAnalysisId.Value, out var val)
                        ? val.MatchScore
                        : (int?)null,
                    CvJdSummary = app.CvJdAnalysisId.HasValue && analysisDataById.TryGetValue(app.CvJdAnalysisId.Value, out var val2)
                        ? val2.Summary
                        : null,
                    HasScheduledInterview = bookedAppIds.Contains(app.Id),
                    CurrentRound = currentRound,
                    CoverLetter = app.CoverLetter,
                    NoticePeriod = app.NoticePeriod,
                    InterviewScore = currentRound.HasValue && evalDict.TryGetValue((app.Id, currentRound.Value), out var iscr) ? iscr : null,
                    InterviewDate = interviewDate
                };

                var emailKey = (app.CandidateEmail ?? "").Trim().ToLower();
                if (!string.IsNullOrEmpty(emailKey) && candidateDictByEmail.TryGetValue(emailKey, out var cAcc))
                {
                    PopulateCandidateProfileFields(resp, cAcc);
                }

                return resp;
            }).ToList();
        }

        private static void PopulateCandidateProfileFields(ApplicationResponse response, CandidateAccount c)
        {
            response.CandidateHeadline = c.Headline;
            response.CandidateAbout = c.About;
            response.CandidateLocation = c.Location;
            response.CandidateDateOfBirth = c.DateOfBirth;

            bool allowHr = true;
            if (!string.IsNullOrEmpty(c.SettingsJson))
            {
                try
                {
                    var settings = System.Text.Json.JsonSerializer.Deserialize<CandidateSettingsDto>(c.SettingsJson, CandidatePortal.PortalSupport.JsonOpts);
                    if (settings != null)
                    {
                        allowHr = settings.AllowHrViewProfile;
                    }
                }
                catch { }
            }

            response.AllowHrViewProfile = allowHr;

            if (allowHr)
            {
                response.CandidateLinkedinUrl = c.LinkedinUrl;
                response.CandidateGithubUrl = c.GithubUrl;
                response.CandidatePortfolioUrl = c.PortfolioUrl;

                try
                {
                    if (!string.IsNullOrEmpty(c.SkillsJson))
                        response.CandidateSkills = System.Text.Json.JsonSerializer.Deserialize<List<string>>(c.SkillsJson) ?? new();
                    if (!string.IsNullOrEmpty(c.ExperienceJson))
                        response.CandidateExperience = System.Text.Json.JsonSerializer.Deserialize<List<CandidateExperienceItem>>(c.ExperienceJson) ?? new();
                    if (!string.IsNullOrEmpty(c.EducationJson))
                        response.CandidateEducation = System.Text.Json.JsonSerializer.Deserialize<List<CandidateEducationItem>>(c.EducationJson) ?? new();
                }
                catch { }
            }
        }

        /// <summary>
        /// Lấy danh sách ứng viên (Application) của MỘT job posting cụ thể, kèm điểm match CV–JD.
        /// Dùng cho màn Recruiter "Ứng viên theo job".
        /// </summary>
        public async Task<Result<List<ApplicationResponse>>> GetApplicationsByJobAsync(Guid jobPostingId, CancellationToken ct = default)
        {
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (jobPosting == null)
                return Result.Failure<List<ApplicationResponse>>("Job posting not found.");

            var applications = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => q
                    .Where(a => a.JobPostingId == jobPostingId)
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new AppListProjection
                    {
                        Id = a.Id,
                        JobPostingId = a.JobPostingId,
                        CandidateEmail = a.CandidateEmail,
                        CandidateName = a.CandidateName,
                        CandidatePhone = a.CandidatePhone,
                        CvFileUrl = a.CvFileUrl,
                        Source = a.Source,
                        Status = a.Status,
                        PracticeSessionUsed = a.PracticeSessionUsed,
                        CreatedAt = a.CreatedAt,
                        CvJdAnalysisId = a.CvJdAnalysisId,
                        CoverLetter = a.CoverLetter,
                        NoticePeriod = a.NoticePeriod
                    }), ct);

            return Result.Success(await MapApplicationsAsync(applications, jobPosting.Title, ct));
        }

        /// <summary>
        /// Danh sách ứng viên thuộc các job do <paramref name="creatorUserId"/> tạo (Recruiter workspace).
        /// Gộp ứng viên theo toàn bộ tin của recruiter, kèm điểm match CV–JD.
        /// </summary>
        public async Task<Result<List<ApplicationResponse>>> GetApplicationsForCreatorAsync(Guid creatorUserId, CancellationToken ct = default)
        {
            var jobIds = (await _unitOfWork.Repository<JobPosting>()
                    .QueryAsync(q => q.Where(j => j.CreatedByUserId == creatorUserId).Select(j => j.Id), ct))
                .ToHashSet();

            if (jobIds.Count == 0)
                return Result.Success(new List<ApplicationResponse>());

            var applications = await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .QueryAsync(q => q
                    .Where(a => jobIds.Contains(a.JobPostingId))
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new AppListProjection
                    {
                        Id = a.Id,
                        JobPostingId = a.JobPostingId,
                        CandidateEmail = a.CandidateEmail,
                        CandidateName = a.CandidateName,
                        CandidatePhone = a.CandidatePhone,
                        CvFileUrl = a.CvFileUrl,
                        Source = a.Source,
                        Status = a.Status,
                        PracticeSessionUsed = a.PracticeSessionUsed,
                        CreatedAt = a.CreatedAt,
                        CvJdAnalysisId = a.CvJdAnalysisId,
                        CoverLetter = a.CoverLetter,
                        NoticePeriod = a.NoticePeriod
                    }), ct);

            return Result.Success(await MapApplicationsAsync(applications, null, ct));
        }

        /// <summary>
        /// Retrieves detailed application by its Guid ID.
        /// </summary>
        public async Task<Result<ApplicationResponse>> GetApplicationByIdAsync(Guid id, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(id, ct);
            if (application == null)
                return Result.Failure<ApplicationResponse>("Application not found.");

            if (application.CvJdAnalysisId.HasValue && application.CvJdAnalysis == null)
            {
                application.CvJdAnalysis = await _unitOfWork.Repository<CvJdAnalysis>().GetByIdAsync(application.CvJdAnalysisId.Value, ct);
            }

            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);

            int? currentRound = null;
            if (application.Status != "cv_submitted" && application.Status != "invited" && application.Status != "cv_rejected")
            {
                if (application.Status == "screening")
                {
                    currentRound = 1;
                }
                else
                {
                    var maxInviteRound = (await _unitOfWork.Repository<InterviewInvite>()
                        .FindAsync(i => i.ApplicationId == id, ct))
                        .Select(i => i.RoundNumber).DefaultIfEmpty(0).Max();

                    var maxSessionRound = (await _unitOfWork.Repository<InterviewSession>()
                        .FindAsync(s => s.ApplicationId == id, ct))
                        .Select(s => s.RoundNumber).DefaultIfEmpty(0).Max();

                    currentRound = Math.Max(maxInviteRound, maxSessionRound);
                    if (currentRound == 0) currentRound = 1;
                }
            }

            decimal? score = null;
            DateTimeOffset? interviewDate = null;
            string? scheduleConfirmationStatus = null;
            string? scheduleDeclineReason = null;
            if (currentRound.HasValue)
            {
                var eval = (await _unitOfWork.Repository<Evaluation>()
                    .FindAsync(e => e.ApplicationId == id && e.RoundNumber == currentRound.Value && e.SessionType == "real", ct))
                    .FirstOrDefault();
                score = eval?.OverallScore;

                // Lịch thi thực tế: lấy StartTime từ AvailabilitySlot qua InterviewBooking
                var booking = (await _unitOfWork.Repository<InterviewBooking>()
                    .FindAsync(b => b.ApplicationId == id && b.RoundNumber == currentRound.Value && b.Status == "scheduled", ct))
                    .FirstOrDefault();
                if (booking != null)
                {
                    var slot = await _unitOfWork.Repository<AvailabilitySlot>().GetByIdAsync(booking.AvailabilitySlotId, ct);
                    interviewDate = slot?.StartTime;
                    scheduleConfirmationStatus = booking.ConfirmationStatus;
                }
                else
                {
                    // Không còn lịch scheduled → nếu vừa bị ứng viên báo bận, đưa lý do lên cho nhân sự xếp lại.
                    var declined = (await _unitOfWork.Repository<InterviewBooking>()
                        .FindAsync(b => b.ApplicationId == id && b.RoundNumber == currentRound.Value && b.Status == "declined", ct))
                        .OrderByDescending(b => b.RespondedAt ?? b.UpdatedAt)
                        .FirstOrDefault();
                    scheduleDeclineReason = declined?.DeclineReason;
                }
            }

            var response = MapToResponse(application, jobPosting, currentRound, score, interviewDate);
            // Cờ đủ điều kiện cấp Interview Code: đã đặt lịch phỏng vấn thật (booking "scheduled").
            var scheduled = await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.ApplicationId == id && b.Status == "scheduled", ct);
            response.HasScheduledInterview = scheduled.Any();
            response.ScheduleConfirmationStatus = scheduleConfirmationStatus;
            response.ScheduleDeclineReason = scheduleDeclineReason;

            var emailClean = application.CandidateEmail.Trim();
            var cAccs = await _unitOfWork.Repository<CandidateAccount>()
                .QueryAsync(q => q.Where(c => c.Email == emailClean), ct);
            if (cAccs.FirstOrDefault() is { } cAcc)
            {
                PopulateCandidateProfileFields(response, cAcc);
            }

            return Result.Success(response);
        }

        /// <summary>
        /// Updates the application status after validating transition rules.
        /// </summary>
        public async Task<Result<ApplicationResponse>> UpdateApplicationStatusAsync(Guid id, string newStatus, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(newStatus))
                return Result.Failure<ApplicationResponse>("Target status cannot be empty.");

            var repository = _unitOfWork.Repository<ARI.Domain.Entities.Application>();
            var application = await repository.GetByIdAsync(id, ct);
            if (application == null)
                return Result.Failure<ApplicationResponse>("Application not found.");

            string currentStatus = application.Status?.Trim() ?? string.Empty;
            newStatus = newStatus.Trim().ToLowerInvariant();

            // 1. Tối ưu: Lấy danh sách trạng thái hợp lệ trực tiếp từ bộ Keys của Dictionary tĩnh
            if (!AllowedStatusTransitions.ContainsKey(newStatus))
            {
                return Result.Failure<ApplicationResponse>($"Status '{newStatus}' is invalid. Allowed values: {string.Join(", ", AllowedStatusTransitions.Keys)}");
            }

            // 2. Prevent updating if status is unchanged
            if (string.Equals(currentStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<ApplicationResponse>($"Application is already in '{newStatus}' status.");
            }

            // 3. Validate status transition
            if (AllowedStatusTransitions.TryGetValue(currentStatus, out var allowedNextStates))
            {
                if (!allowedNextStates.Contains(newStatus))
                {
                    return Result.Failure<ApplicationResponse>($"Cannot transition application status from '{currentStatus}' to '{newStatus}'.");
                }
            }
            else
            {
                return Result.Failure<ApplicationResponse>($"Transition mapping for current status '{currentStatus}' is not configured.");
            }

            // 4. Update status and save
            application.Status = newStatus;
            application.UpdatedAt = DateTimeOffset.UtcNow;

            repository.Update(application);
            await _unitOfWork.SaveChangesAsync(ct);
            _cache.Remove(AllApplicationsCacheKey);

            // Get job info for the response
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            var response = MapToResponse(application, jobPosting);
            
            if (application.CandidateAccountId.HasValue)
            {
                await _notificationService.PublishUserEventAsync(application.CandidateAccountId.Value, "ReceiveApplicationStatusUpdate", response, ct);
            }

            return Result.Success(response);
        }

        /// <summary>
        /// Gửi lời mời phỏng vấn theo vòng: tạo InterviewInvite (token hoá), email link CHỌN LỊCH
        /// trên thiết bị cá nhân của ứng viên (base URL theo môi trường, không hardcode localhost).
        /// </summary>
        /// <param name="frontendBaseUrl">Base URL portal ứng viên (controller truyền từ config).</param>
        /// <param name="roundNumber">Vòng cần mời (mặc định 1).</param>
        /// <param name="sendEmail">
        /// Có gửi email báo "qua vòng CV" hay không. Luồng duyệt CV → gán lịch chỉ gửi MỘT email duy nhất
        /// (email gộp ở bước gán slot đã kèm chúc mừng qua CV + lịch + 2 nút), nên <c>AcceptApplicationAsync</c>
        /// gọi với <c>sendEmail: false</c> (chỉ đổi trạng thái + tạo token + chuông). Standalone "Mời" vẫn gửi.
        /// </param>
        public async Task<Result<bool>> SendInterviewInviteAsync(Guid applicationId, string frontendBaseUrl, int roundNumber = 1, CancellationToken ct = default, bool sendEmail = true)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển này.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            var ttlHours = job?.InviteTokenTtlHours is { } h && h > 0 ? h : 48;
            var baseUrl = (string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:3000" : frontendBaseUrl).TrimEnd('/');

            // Sinh token thật (gửi email) + lưu hash. Một invite còn hiệu lực / (application, round):
            // vô hiệu hoá invite cũ chưa dùng của vòng này trước khi tạo mới.
            var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var oldInvites = await _unitOfWork.Repository<InterviewInvite>()
                .FindAsync(i => i.ApplicationId == applicationId && i.RoundNumber == roundNumber && i.ScheduledAt == null, ct);
            foreach (var old in oldInvites)
                _unitOfWork.Repository<InterviewInvite>().Delete(old);

            var invite = new InterviewInvite
            {
                ApplicationId = applicationId,
                RoundNumber = roundNumber,
                TokenHash = TokenHashing.Sha256Hex(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(ttlHours),
            };
            await _unitOfWork.Repository<InterviewInvite>().AddAsync(invite, ct);

            // ADR-048: ứng viên KHÔNG tự chọn lịch nữa — nhân sự sẽ gán giờ cụ thể. Email chỉ báo qua vòng CV
            // + hướng dẫn theo dõi Portal; giờ hẹn thật sẽ gửi ở email "Lịch phỏng vấn đã được xếp".
            var portalLink = $"{baseUrl}/candidate/applications";

            var subject = "[ARISP] - Hồ sơ của bạn đã qua vòng duyệt CV";
            var htmlMessage = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
            <h3 style='color: #333;'>Chào {application.CandidateName},</h3>
            <p>Chúc mừng bạn! Hồ sơ ứng tuyển của bạn đã thông qua vòng duyệt hồ sơ (CV Review) — vòng {roundNumber}.</p>
            <p><strong>Bộ phận nhân sự sẽ xếp lịch phỏng vấn</strong> và gửi thông báo giờ hẹn cụ thể cho bạn trong thời gian tới. Vui lòng theo dõi email và Candidate Portal.</p>
            <p style='text-align: center; margin: 30px 0;'>
                <a href='{portalLink}' style='padding: 12px 25px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>Vào Candidate Portal</a>
            </p>
            <p style='color: #666; font-size: 12px;'><i>Lưu ý: Buổi phỏng vấn thật diễn ra tại văn phòng — bạn sẽ nhập mã phỏng vấn (Interview Code) do nhân sự cấp tại chỗ. Sau khi có lịch, bạn có thể luyện tập với chế độ phỏng vấn thử trước ngày hẹn.</i></p>
            <br/>
            <p>Trân trọng,</p>
            <p><strong>Đội ngũ nhân sự ARISP</strong></p>
        </div>";

            var emailCandidateAccount = application.CandidateAccountId.HasValue 
                ? await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(application.CandidateAccountId.Value, ct)
                : null;
            var settings = emailCandidateAccount != null && !string.IsNullOrEmpty(emailCandidateAccount.SettingsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<ARI.Application.DTOs.CandidateSettingsDto>(emailCandidateAccount.SettingsJson) ?? new ARI.Application.DTOs.CandidateSettingsDto()
                : new ARI.Application.DTOs.CandidateSettingsDto();

            try
            {
                if (sendEmail && settings.InterviewInvite.Email)
                {
                    await _emailService.SendEmailAsync(application.CandidateEmail, subject, htmlMessage);
                }

                // Mời phỏng vấn = đã qua CV → mở giai đoạn sơ loại/phỏng vấn (và bật phỏng vấn thử).
                // Nâng từ invited/cv_submitted → screening để PracticeAvailable = true.
                if (string.Equals(application.Status, "cv_submitted", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(application.Status, "invited", StringComparison.OrdinalIgnoreCase))
                    application.Status = "screening";
                application.UpdatedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(ct);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure($"Lỗi khi gọi dịch vụ gửi email: {ex.Message}");
            }
        }

        /// <summary>
        /// Chấp nhận hồ sơ ứng tuyển: chuyển trạng thái sang screening + mở phỏng vấn thử. KHÔNG gửi email ở
        /// bước này — ứng viên chỉ nhận chuông báo qua CV; email mời phỏng vấn (gộp chúc mừng qua CV + lịch hẹn
        /// + 2 nút xác nhận/từ chối) được gửi MỘT LẦN DUY NHẤT khi HR gán khung giờ (AssignSlotCommand).
        /// </summary>
        public async Task<Result<bool>> AcceptApplicationAsync(Guid applicationId, string frontendBaseUrl, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển này.");

            if (!string.Equals(application.Status, "cv_submitted", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(application.Status, "invited", StringComparison.OrdinalIgnoreCase))
            {
                return Result<bool>.Failure("Chỉ có thể duyệt hồ sơ ứng tuyển ở trạng thái mới nộp (cv_submitted) hoặc được mời (invited).");
            }

            // Nâng trạng thái + tạo token đánh dấu vòng, NHƯNG không gửi email ở đây (sendEmail: false) —
            // chỉ gửi 1 email duy nhất khi gán lịch (email đó đã gộp chúc mừng qua CV + lịch + 2 nút).
            var inviteResult = await SendInterviewInviteAsync(applicationId, frontendBaseUrl, 1, ct, sendEmail: false);
            if (inviteResult.IsFailure)
            {
                return Result<bool>.Failure(inviteResult.Error);
            }

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            var jobTitle = job?.Title ?? "Vị trí tuyển dụng";

            var pushCandidateAccount = application.CandidateAccountId.HasValue 
                ? await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(application.CandidateAccountId.Value, ct)
                : null;
            var pushSettings = pushCandidateAccount != null && !string.IsNullOrEmpty(pushCandidateAccount.SettingsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<ARI.Application.DTOs.CandidateSettingsDto>(pushCandidateAccount.SettingsJson) ?? new ARI.Application.DTOs.CandidateSettingsDto()
                : new ARI.Application.DTOs.CandidateSettingsDto();

            // Tạo Notification trong database cho ứng viên
            var response = MapToResponse(application, job, 1);
            if (application.CandidateAccountId.HasValue && pushSettings.InterviewInvite.Push)
            {
                var notifRepo = _unitOfWork.Repository<ARI.Domain.Entities.Notification>();
                var dedupKey = $"cv_accepted:{application.Id}";
                var existingNotifs = await notifRepo.FindAsync(n => n.CandidateAccountId == application.CandidateAccountId.Value && n.DedupKey == dedupKey, ct);
                if (!existingNotifs.Any())
                {
                    var newNotif = new ARI.Domain.Entities.Notification
                    {
                        CandidateAccountId = application.CandidateAccountId.Value,
                        DedupKey = dedupKey,
                        Type = "result",
                        Title = "Hồ sơ ứng tuyển được chấp nhận",
                        Body = $"Chúc mừng! Hồ sơ vị trí {jobTitle} đã qua vòng duyệt CV. Nhân sự sẽ xếp lịch và gửi email mời phỏng vấn kèm lịch hẹn cho bạn.",
                        Link = $"/candidate/applications/{application.Id}",
                        IsRead = false
                    };
                    await notifRepo.AddAsync(newNotif, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                // Trigger bell update
                await _notificationService.PublishUserEventAsync(application.CandidateAccountId.Value, "ReceiveUserNotification", new { Type = "CvAccepted" }, ct);
            }

            return Result<bool>.Success(true);
        }

        /// <summary>
        /// Từ từ chối hồ sơ ứng tuyển ở vòng duyệt CV: chuyển trạng thái sang not_pass và gửi email cảm ơn.
        /// </summary>
        public async Task<Result<bool>> RejectApplicationAsync(Guid applicationId, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển này.");

            if (string.Equals(application.Status, "cv_rejected", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(application.Status, "not_pass", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(application.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(application.Status, "withdrawn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(application.Status, "pass", StringComparison.OrdinalIgnoreCase))
            {
                return Result<bool>.Failure("Hồ sơ này đã ở trạng thái kết thúc (đã từ chối / loại / hoàn thành).");
            }

            bool isCvPhase = string.Equals(application.Status, "cv_submitted", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(application.Status, "invited", StringComparison.OrdinalIgnoreCase);

            application.Status = isCvPhase ? "cv_rejected" : "not_pass";
            application.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);

            // Đóng mọi lịch phỏng vấn chưa khép của ứng viên — kể cả lịch đã "declined", vì hồ sơ
            // bị loại thì không còn lịch nào còn ý nghĩa.
            var activeBookings = await _unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => b.ApplicationId == applicationId && b.Status != BookingStatus.Cancelled, ct);

            // CHỈ booking đang thật sự giữ chỗ mới phải trả chỗ. Trước đây trừ cho MỌI booking trong
            // danh sách trên, nên hồ sơ đã từ chối lịch (chỗ vốn đã được trả ở DeclineScheduleCommand
            // / ScheduleConfirmationHostedService) bị trừ lần thứ hai → booked_count tụt xuống dưới
            // số chỗ thực sự đang bị chiếm, và không có cơ chế nào đối soát lại nên lệch vĩnh viễn.
            var slotsToRelease = new List<Guid>();

            foreach (var booking in activeBookings)
            {
                if (string.Equals(booking.Status, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
                    slotsToRelease.Add(booking.AvailabilitySlotId);

                booking.Status = BookingStatus.Cancelled;
                booking.ConfirmationStatus = BookingConfirmationStatus.Declined;
                booking.DeclinedBy = BookingDeclinedBy.Staff;
                booking.DeclineReason = "Ứng viên đã bị loại khỏi quy trình tuyển dụng.";
                booking.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<InterviewBooking>().Update(booking);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            // Trả chỗ SAU khi trạng thái "cancelled" đã được lưu, và bằng SQL nguyên tử thay vì
            // đọc-rồi-ghi qua EF: cùng lúc có thể có người khác đang gán ứng viên vào chính ca này.
            // Thứ tự này khớp ScheduleConfirmationHostedService — save lỗi thì không chỗ nào bị trả oan.
            foreach (var slotId in slotsToRelease)
            {
                try
                {
                    await _unitOfWork.ExecuteSqlRawAsync(
                        "UPDATE availability_slots SET booked_count = GREATEST(booked_count - 1, 0), updated_at = {0} WHERE id = {1}",
                        new object[] { DateTimeOffset.UtcNow, slotId }, ct);
                }
                catch { /* best-effort — không chặn việc loại hồ sơ */ }
            }

            _cache.Remove(AllApplicationsCacheKey);

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            var jobTitle = job?.Title ?? "Vị trí tuyển dụng";
            
            var emailCandidateAccount = application.CandidateAccountId.HasValue 
                ? await _unitOfWork.Repository<CandidateAccount>().GetByIdAsync(application.CandidateAccountId.Value, ct)
                : null;
            var settings = emailCandidateAccount != null && !string.IsNullOrEmpty(emailCandidateAccount.SettingsJson)
                ? System.Text.Json.JsonSerializer.Deserialize<ARI.Application.DTOs.CandidateSettingsDto>(emailCandidateAccount.SettingsJson) ?? new ARI.Application.DTOs.CandidateSettingsDto()
                : new ARI.Application.DTOs.CandidateSettingsDto();

            if (settings.ApplicationUpdate.Email)
            {
                var subject = $"[ARISP] - Thư cảm ơn ứng tuyển vị trí {jobTitle}";
                var htmlMessage = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
            <h3 style='color: #333;'>Chào {application.CandidateName},</h3>
            <p>Cảm ơn bạn đã quan tâm đến cơ hội nghề nghiệp tại ARISP và dành thời gian nộp hồ sơ ứng tuyển cho vị trí <strong>{jobTitle}</strong>.</p>
            <p>Chúng tôi rất ấn tượng với hồ sơ và kinh nghiệm của bạn. Tuy nhiên, sau khi xem xét kỹ lưỡng các yêu cầu hiện tại của công việc, chúng tôi rất tiếc chưa thể tiến xa hơn với bạn trong đợt tuyển dụng này.</p>
            <p>Thông tin của bạn sẽ được lưu giữ trong hệ thống cơ sở dữ liệu tài năng của chúng tôi. Nếu có các cơ hội phù hợp hơn trong tương lai, chúng tôi sẽ chủ động liên hệ lại.</p>
            <div style='text-align: center; margin: 28px 0;'>
                <a href='http://localhost:3000/candidate/applications/{application.Id}' style='background-color: #4f46e5; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>Xem hồ sơ của bạn</a>
            </div>
            <p>Chúc bạn luôn nhiều sức khỏe và may mắn trên con đường sự nghiệp của mình.</p>
            <br/>
            <p>Trân trọng,</p>
            <p><strong>Đội ngũ nhân sự ARISP</strong></p>
        </div>";
                try { await _emailService.SendEmailAsync(application.CandidateEmail, subject, htmlMessage); } catch { }
            }

            // Gửi SignalR thông báo và tạo Notification trong database cho ứng viên
            var response = MapToResponse(application, job);
            if (application.CandidateAccountId.HasValue && settings.ApplicationUpdate.Push)
            {
                await _notificationService.PublishUserEventAsync(application.CandidateAccountId.Value, "ReceiveApplicationStatusUpdate", response, ct);

                var notifRepo = _unitOfWork.Repository<ARI.Domain.Entities.Notification>();
                var dedupKey = $"cv_rejected:{application.Id}";
                var existingNotifs = await notifRepo.FindAsync(n => n.CandidateAccountId == application.CandidateAccountId.Value && n.DedupKey == dedupKey, ct);
                if (!existingNotifs.Any())
                {
                    var newNotif = new ARI.Domain.Entities.Notification
                    {
                        CandidateAccountId = application.CandidateAccountId.Value,
                        DedupKey = dedupKey,
                        Type = "result",
                        Title = "Kết quả ứng tuyển",
                        Body = $"Thư cảm ơn ứng tuyển vị trí {jobTitle} đã được gửi tới email của bạn.",
                        Link = $"/candidate/applications/{application.Id}",
                        IsRead = false
                    };
                    await notifRepo.AddAsync(newNotif, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                // Trigger bell update
                await _notificationService.PublishUserEventAsync(application.CandidateAccountId.Value, "ReceiveUserNotification", new { Type = "CvRejected" }, ct);
            }

            return Result<bool>.Success(true);
        }

        /// <summary>
        /// Còn được phỏng vấn thử cho vòng <paramref name="roundNumber"/> không (1 lượt / vòng).
        /// Eligible = chưa có phiên practice nào của vòng này.
        /// </summary>
        public async Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result.Failure<bool>("Application not found.");

            var used = await _unitOfWork.Repository<InterviewSession>().FindAsync(
                s => s.ApplicationId == applicationId && s.SessionType == "practice" && s.RoundNumber == roundNumber, ct);

            return Result.Success(!used.Any());
        }
    }
}
