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
using ARI.Application.Emails;
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
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        // Cache key cho danh sách toàn bộ ứng tuyển (HR view).
        private const string AllApplicationsCacheKey = "applications:all";

        /// <summary>
        /// Máy trạng thái của hồ sơ ứng tuyển — dùng cho <c>PATCH /api/applications/{id}/status</c>.
        ///
        /// MỌI trạng thái được ghi ở bất kỳ đâu đều phải có mặt làm KHOÁ ở đây, kể cả trạng thái
        /// kết thúc: bảng này vừa là "đi từ đâu tới đâu được" vừa là danh sách trạng thái hợp lệ
        /// (xem <see cref="UpdateApplicationStatusAsync"/>). Trước đây <c>cv_rejected</c> được
        /// <see cref="RejectApplicationAsync"/> ghi nhưng KHÔNG phải khoá, nên mọi hồ sơ bị loại ở
        /// vòng CV rơi vào nhánh "Transition mapping … is not configured" — không thao tác lại
        /// được và thông báo lỗi thì không nói được vì sao.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> AllowedStatusTransitions = new(StringComparer.OrdinalIgnoreCase)
        {
            { ApplicationStatuses.Invited, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.CvSubmitted, ApplicationStatuses.CvRejected, ApplicationStatuses.Withdrawn } },
            { ApplicationStatuses.CvSubmitted, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.HmReview, ApplicationStatuses.Screening, ApplicationStatuses.CvRejected, ApplicationStatuses.Withdrawn } },
            // Cổng duyệt của Hiring Manager (ADR-061). Thiếu KHOÁ này là mọi hồ sơ đang chờ HM rơi
            // đúng vào nhánh "Transition mapping … is not configured" mà chú thích trên đã đi chữa
            // cho cv_rejected — kẹt vĩnh viễn, không rút được, không mở lại được.
            // `→ cv_submitted` là đường rút lại việc gửi duyệt (gửi nhầm người, gửi nhầm hồ sơ).
            { ApplicationStatuses.HmReview, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.Screening, ApplicationStatuses.CvSubmitted, ApplicationStatuses.CvRejected, ApplicationStatuses.Withdrawn } },
            { ApplicationStatuses.CvRejected, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.CvSubmitted } }, // mở lại hồ sơ bị loại nhầm
            { ApplicationStatuses.Screening, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.Interview, ApplicationStatuses.NotPass, ApplicationStatuses.Withdrawn } },
            { ApplicationStatuses.Interview, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.Pass, ApplicationStatuses.NotPass, ApplicationStatuses.Withdrawn } },
            { ApplicationStatuses.Pass, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.Offer, ApplicationStatuses.Withdrawn } },
            { ApplicationStatuses.Offer, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.Hired, ApplicationStatuses.OfferDeclined, ApplicationStatuses.NotPass, ApplicationStatuses.Withdrawn } },
            { ApplicationStatuses.Hired, new(StringComparer.OrdinalIgnoreCase) },          // điểm kết thúc thành công
            { ApplicationStatuses.OfferDeclined, new(StringComparer.OrdinalIgnoreCase) },  // ứng viên từ chối / hết hạn
            { ApplicationStatuses.NotPass, new(StringComparer.OrdinalIgnoreCase) { ApplicationStatuses.Screening, ApplicationStatuses.Interview } },
            { ApplicationStatuses.Withdrawn, new(StringComparer.OrdinalIgnoreCase) } // terminal state
        };

        public ApplicationService(
            IUnitOfWork unitOfWork,
            IRagIngestionService ragIngestion,
            IEmailService emailService,
            INotificationService notificationService,
            Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
            IMemoryCache cache,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _ragIngestion = ragIngestion;
            _emailService = emailService;
            _notificationService = notificationService;
            _scopeFactory = scopeFactory;
            _cache = cache;
            _configuration = configuration;
        }

        /// <summary>Gốc Candidate Portal cho link trong email — không hardcode localhost vào thư gửi đi.</summary>
        private string PortalBaseUrl => FrontendUrls.Candidate(_configuration);

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
                HmDecision = application.HmDecision,
                HmDecisionNote = application.HmDecisionNote,
                HmDecidedAt = application.HmDecidedAt,
                CurrentRound = currentRound,
                CoverLetter = application.CoverLetter,
                NoticePeriod = application.NoticePeriod,
                InterviewScore = interviewScore,
                InterviewDate = interviewDate,
                MatchScore = application.CvJdAnalysis?.MatchScore,
                CvJdSummary = application.CvJdAnalysis?.Summary,
                CvCriterionScores = MapCvCriterionScores(application.CvJdAnalysis?.CriterionScores)
            };
        }


        /// <summary>
        /// Đọc điểm tiêu chí chấm CV để hiển thị (ADR-060). Đọc được CẢ dạng phẳng cũ lẫn dạng có ảnh
        /// chụp, nên hồ sơ chấm trước khi khai rubric vẫn hiện đúng.
        /// </summary>
        private static List<CvCriterionScoreDto> MapCvCriterionScores(string? json)
            => ARI.Application.Playbooks.ScoringRubricSupport.ParseForDisplay(json)
                .Select(c => new CvCriterionScoreDto
                {
                    Key = c.Key,
                    Score = c.Score,
                    Label = c.Label,
                    Weight = c.Weight,
                })
                .ToList();

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
                <a href='{PortalBaseUrl}/candidate/applications/{application.Id}' style='background-color: #4f46e5; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 15px;'>Xem hồ sơ ứng tuyển</a>
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
                        NoticePeriod = a.NoticePeriod,
                        HmDecision = a.HmDecision,
                        HmDecisionNote = a.HmDecisionNote,
                        HmDecidedAt = a.HmDecidedAt
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
            public string? HmDecision { get; set; }
            public string? HmDecisionNote { get; set; }
            public DateTimeOffset? HmDecidedAt { get; set; }
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

            var mapped = apps.Select(app =>
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
                    HmDecision = app.HmDecision,
                    HmDecisionNote = app.HmDecisionNote,
                    HmDecidedAt = app.HmDecidedAt,
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

            // Trạng thái CHI TIẾT của vòng đang diễn ra (ADR-067). Tính sau khi đã có `CurrentRound`
            // vì nó là đầu vào; gom một lượt cho cả danh sách chứ không hỏi từng dòng.
            try
            {
                var stages = await ARI.Application.Applications.ApplicationStageStatus.ComputeAsync(
                    _unitOfWork,
                    mapped.Select(m => (m.Id, m.JobPostingId, m.Status, m.CurrentRound)).ToList(),
                    ct);
                foreach (var m in mapped)
                    if (stages.TryGetValue(m.Id, out var stage)) m.StageStatus = stage;
            }
            catch
            {
                // Không có trạng thái chi tiết thì bảng vẫn dùng được (giao diện rơi về `Status`).
                // Một cột phụ hỏng không được làm hỏng cả danh sách ứng viên.
            }

            return mapped;
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
                        NoticePeriod = a.NoticePeriod,
                        HmDecision = a.HmDecision,
                        HmDecisionNote = a.HmDecisionNote,
                        HmDecidedAt = a.HmDecidedAt
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

            return await GetApplicationsForJobsAsync(jobIds, ct);
        }

        /// <summary>
        /// Danh sách ứng viên thuộc một tập tin tuyển dụng CHO TRƯỚC — phạm vi do
        /// <see cref="JobAccess.ScopedJobIdsAsync"/> tính ở tầng gọi, không phải do client khai.
        /// Tập rỗng trả về danh sách rỗng (khác với "không giới hạn", vốn đi đường
        /// <see cref="GetAllApplicationsAsync"/>).
        /// </summary>
        public async Task<Result<List<ApplicationResponse>>> GetApplicationsForJobsAsync(
            IReadOnlyCollection<Guid> jobPostingIds, CancellationToken ct = default)
        {
            var jobIds = jobPostingIds as HashSet<Guid> ?? jobPostingIds.ToHashSet();

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
                        NoticePeriod = a.NoticePeriod,
                        HmDecision = a.HmDecision,
                        HmDecisionNote = a.HmDecisionNote,
                        HmDecidedAt = a.HmDecidedAt
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
        /// Mở một vòng phỏng vấn cho hồ sơ: tạo <see cref="InterviewInvite"/> đánh dấu "vòng đang hoạt động"
        /// (mọi nơi đọc vòng hiện tại đều lấy <c>max(RoundNumber)</c> của bảng này) và nâng hồ sơ khỏi
        /// giai đoạn duyệt CV. <b>Không gửi email</b> — thư duy nhất gửi cho ứng viên là thư mời kèm giờ hẹn
        /// ở bước xếp lịch (ADR-059); trước đây hàm này còn gửi một thư "nhân sự sẽ xếp lịch sau" nên ứng
        /// viên nhận hai thư rời rạc, thư đầu không có thông tin nào dùng được.
        /// </summary>
        public async Task<Result<bool>> OpenRoundForSchedulingAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển này.");

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            var ttlHours = job?.InviteTokenTtlHours is { } h && h > 0 ? h : 48;

            // Một invite còn hiệu lực / (application, round): xoá invite cũ CHƯA gắn lịch của vòng này.
            // Invite đã có lịch (ScheduledAt != null) là dấu vết lịch sử, giữ nguyên.
            var oldInvites = await _unitOfWork.Repository<InterviewInvite>()
                .FindAsync(i => i.ApplicationId == applicationId && i.RoundNumber == roundNumber && i.ScheduledAt == null, ct);
            foreach (var old in oldInvites)
                _unitOfWork.Repository<InterviewInvite>().Delete(old);

            var invite = new InterviewInvite
            {
                ApplicationId = applicationId,
                RoundNumber = roundNumber,
                // Cột TokenHash là NOT NULL từ thời ứng viên tự đặt lịch bằng link có token. Luồng đó đã bị
                // ADR-048 gỡ bỏ và không dòng code nào còn đối chiếu giá trị này — sinh ngẫu nhiên để thoả
                // ràng buộc, KHÔNG phát tán ra ngoài dưới bất kỳ dạng nào.
                TokenHash = TokenHashing.Sha256Hex(Guid.NewGuid().ToString("N")),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(ttlHours),
            };
            await _unitOfWork.Repository<InterviewInvite>().AddAsync(invite, ct);

            // Qua vòng CV → mở giai đoạn sơ loại/phỏng vấn (và bật phỏng vấn thử).
            // `hm_review` cũng thuộc giai đoạn CV (ApplicationStatuses.CvPhase) nên được nhấc lên
            // cùng — nếu không, hồ sơ đã qua cổng HM sẽ mắc lại ở trạng thái chờ duyệt vĩnh viễn.
            if (ApplicationStatuses.IsCvPhase(application.Status))
                application.Status = ApplicationStatuses.Screening;
            application.UpdatedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);

            return Result<bool>.Success(true);
        }

        /// <summary>
        /// Chấp nhận hồ sơ ứng tuyển: chuyển trạng thái sang screening + mở phỏng vấn thử. KHÔNG gửi email ở
        /// bước này — ứng viên chỉ nhận chuông báo qua CV; email mời phỏng vấn (gộp chúc mừng qua CV + lịch hẹn
        /// + 2 nút xác nhận/từ chối) được gửi MỘT LẦN DUY NHẤT khi HR gán khung giờ (AssignSlotCommand).
        /// </summary>
        public async Task<Result<bool>> AcceptApplicationAsync(Guid applicationId, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển này.");

            // Hồ sơ ở giai đoạn CV thì duyệt thẳng như cũ. Hồ sơ đang nằm ở cổng Hiring Manager thì
            // CHỈ đi tiếp được khi cổng đã mở (HM duyệt, hoặc quản trị viên vượt cổng có lý do) —
            // ADR-061. Cổng mở nằm ở cột HmDecision chứ không phải ở Status: Status nói hồ sơ đang
            // ở đâu, HmDecision nói đã được phép đi tiếp chưa.
            var inCvPhase = ApplicationStatuses.Is(application.Status, ApplicationStatuses.CvSubmitted)
                            || ApplicationStatuses.Is(application.Status, ApplicationStatuses.Invited);
            var gateOpen = ApplicationStatuses.Is(application.Status, ApplicationStatuses.HmReview)
                           && HmDecision.IsOpen(application.HmDecision);

            if (!inCvPhase && !gateOpen)
            {
                return ApplicationStatuses.Is(application.Status, ApplicationStatuses.HmReview)
                    ? Result<bool>.Failure("Hồ sơ đang chờ Hiring Manager duyệt. Chưa xếp lịch được.")
                    : Result<bool>.Failure("Chỉ có thể duyệt hồ sơ ứng tuyển ở trạng thái mới nộp (cv_submitted) hoặc được mời (invited).");
            }

            // Nâng trạng thái + tạo token đánh dấu vòng, NHƯNG không gửi email ở đây (sendEmail: false) —
            // chỉ gửi 1 email duy nhất khi gán lịch (email đó đã gộp chúc mừng qua CV + lịch + 2 nút).
            var inviteResult = await OpenRoundForSchedulingAsync(applicationId, 1, ct);
            if (inviteResult.IsFailure)
            {
                return Result<bool>.Failure(inviteResult.Error);
            }

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            var jobTitle = job?.Title ?? "Vị trí tuyển dụng";

            // CỐ Ý KHÔNG báo gì cho ứng viên ở bước này (ADR-067).
            //
            // Việc mở vòng để xếp lịch là chuyển động NỘI BỘ: Recruiter duyệt hồ sơ → Hiring Manager
            // duyệt → hồ sơ quay về hàng chờ xếp lịch. Ứng viên chỉ được báo MỘT lần, khi đã có giờ
            // hẹn cụ thể (`AssignSlotCommand` gửi chuông + thư mời). Trước đây bước này bắn chuông
            // "hồ sơ đã qua vòng duyệt CV" ngay lúc duyệt, nên ứng viên nhận tin vui rồi ngồi im
            // không biết bao lâu — và nếu Hiring Manager từ chối sau đó thì tin vui ấy thành sai.
            //
            // `jobTitle` vẫn tra ở trên vì thông báo lỗi và audit dùng tới.
            _ = jobTitle;

            return Result<bool>.Success(true);
        }

        /// <summary>
        /// Từ từ chối hồ sơ ứng tuyển ở vòng duyệt CV: chuyển trạng thái sang not_pass và gửi email cảm ơn.
        /// </summary>
        public async Task<Result<bool>> RejectApplicationAsync(
            Guid applicationId, CancellationToken ct = default,
            EmailOverride? emailOverride = null, Guid? actorUserId = null)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result<bool>.Failure("Không tìm thấy hồ sơ ứng tuyển này.");

            if (ApplicationStatuses.IsTerminal(application.Status))
            {
                return Result<bool>.Failure("Hồ sơ này đã ở trạng thái kết thúc (đã từ chối / loại / hoàn thành).");
            }

            application.Status = ApplicationStatuses.IsCvPhase(application.Status)
                ? ApplicationStatuses.CvRejected
                : ApplicationStatuses.NotPass;
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
                // Nội dung dựng ở ApplicationRejectedEmail — CÙNG builder mà trình soạn thảo dùng
                // để xem trước, nên bản nhân sự nhìn thấy và bản ứng viên nhận là một (ADR-061).
                var mail = ApplicationRejectedEmail.Build(application, job, PortalBaseUrl);
                await CandidateEmailSender.SendAsync(
                    _unitOfWork, _notificationService,
                    EmailTemplateKeys.ApplicationRejected,
                    new RenderedEmail(mail.Subject, mail.Html, application.CandidateEmail, application.CandidateName),
                    emailOverride,
                    applicationId: application.Id, jobPostingId: application.JobPostingId,
                    sentByUserId: actorUserId, ct);
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
        /// Eligible = vòng này KHÔNG phải trắc nghiệm, chưa lỡ buổi thật, và chưa dùng lượt thử của vòng.
        /// </summary>
        public async Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result.Failure<bool>("Application not found.");

            // Vòng trắc nghiệm không hỗ trợ phỏng vấn thử (buổi thử là hội thoại với AI).
            var roundConfig = (await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(
                    r => r.JobPostingId == application.JobPostingId && r.RoundNumber == roundNumber, ct))
                .FirstOrDefault();
            if (ARI.Application.Scheduling.InterviewInviteEmail.IsOnlineTest(roundConfig?.RoundType))
                return Result.Success(false);

            // Đã lỡ buổi thật của vòng này → coi như trượt vòng, không mở phỏng vấn thử nữa.
            if (await ARI.Application.Scheduling.SchedulingSupport.HasMissedRealInterviewAsync(
                    _unitOfWork, applicationId, roundNumber, ct))
                return Result.Success(false);

            var used = await _unitOfWork.Repository<InterviewSession>().FindAsync(
                s => s.ApplicationId == applicationId && s.SessionType == "practice" && s.RoundNumber == roundNumber, ct);

            return Result.Success(!used.Any());
        }
    }
}
