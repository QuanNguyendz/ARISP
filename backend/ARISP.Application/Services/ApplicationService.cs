using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Common;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.Application.Services
{
    public class ApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRagIngestionService _ragIngestion;
        private readonly IEmailService _emailService;
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

        public ApplicationService(IUnitOfWork unitOfWork, IRagIngestionService ragIngestion, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _ragIngestion = ragIngestion;
            _emailService = emailService;
        }

        /// <summary>
        /// Hàm tiện ích dùng chung để Map Entity sang Response (Tránh lặp code)
        /// </summary>
        private static ApplicationResponse MapToResponse(ARISP.Domain.Entities.Application application, JobPosting? jobPosting)
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
                CvJdAnalysisId = application.CvJdAnalysisId
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

            var application = new ARISP.Domain.Entities.Application
            {
                JobPostingId = request.JobPostingId,
                CandidateAccountId = request.CandidateAccountId,
                CandidateEmail = request.CandidateEmail,
                CandidateName = request.CandidateName,
                CandidatePhone = request.CandidatePhone,
                CvFileUrl = request.CvFileUrl,
                CvText = request.CvText,
                DesiredLocation = request.DesiredLocation,
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

            await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().AddAsync(application, ct);
            await _unitOfWork.SaveChangesAsync(ct);

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

            return Result.Success(response);
        }

        public async Task<Result<List<ApplicationResponse>>> GetAllApplicationsAsync(CancellationToken ct = default)
        {
            // Projection ở tầng SQL — KHÔNG kéo cột text lớn (CvText/CoverLetter/DemographicData)
            // vốn khiến danh sách rất nặng và timeout khi đọc stream từ Postgres.
            var applications = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
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
                    }), ct);

            return Result.Success(await MapApplicationsAsync(applications, null, ct));
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
        }

        /// <summary>
        /// Map danh sách projection → ApplicationResponse (CvText = null cho list).
        /// JobTitle lấy từ <paramref name="jobTitleOverride"/> nếu cùng 1 job, ngược lại batch query tiêu đề.
        /// </summary>
        private async Task<List<ApplicationResponse>> MapApplicationsAsync(
            List<AppListProjection> apps, string? jobTitleOverride, CancellationToken ct)
        {
            Dictionary<Guid, string> jobDict;
            if (jobTitleOverride != null)
            {
                jobDict = new Dictionary<Guid, string>();
            }
            else
            {
                var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
                jobDict = (await _unitOfWork.Repository<JobPosting>()
                        .QueryAsync(q => q.Where(j => jobIds.Contains(j.Id)).Select(j => new { j.Id, j.Title }), ct))
                    .ToDictionary(j => j.Id, j => j.Title);
            }

            var analysisIds = apps.Where(a => a.CvJdAnalysisId.HasValue).Select(a => a.CvJdAnalysisId!.Value).Distinct().ToList();
            var scoreByAnalysisId = analysisIds.Count == 0
                ? new Dictionary<Guid, int>()
                : (await _unitOfWork.Repository<CvJdAnalysis>()
                        .QueryAsync(q => q.Where(c => analysisIds.Contains(c.Id)).Select(c => new { c.Id, c.MatchScore }), ct))
                    .ToDictionary(c => c.Id, c => c.MatchScore);

            return apps.Select(app => new ApplicationResponse
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
                MatchScore = app.CvJdAnalysisId.HasValue && scoreByAnalysisId.TryGetValue(app.CvJdAnalysisId.Value, out var ms)
                    ? ms
                    : (int?)null,
            }).ToList();
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

            var applications = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
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

            var applications = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
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
                    }), ct);

            return Result.Success(await MapApplicationsAsync(applications, null, ct));
        }

        /// <summary>
        /// Retrieves detailed application by its Guid ID.
        /// </summary>
        public async Task<Result<ApplicationResponse>> GetApplicationByIdAsync(Guid id, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(id, ct);
            if (application == null)
                return Result.Failure<ApplicationResponse>("Application not found.");

            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);

            return Result.Success(MapToResponse(application, jobPosting));
        }

        /// <summary>
        /// Updates the application status after validating transition rules.
        /// </summary>
        public async Task<Result<ApplicationResponse>> UpdateApplicationStatusAsync(Guid id, string newStatus, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(newStatus))
                return Result.Failure<ApplicationResponse>("Target status cannot be empty.");

            var repository = _unitOfWork.Repository<ARISP.Domain.Entities.Application>();
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

            // Get job info for the response
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);

            return Result.Success(MapToResponse(application, jobPosting));
        }

        /// <summary>Hash token mời phỏng vấn bằng SHA256 (lưu DB an toàn). Dùng chung với controller đặt lịch.</summary>
        public static string HashInviteToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }

        /// <summary>
        /// Gửi lời mời phỏng vấn theo vòng: tạo InterviewInvite (token hoá), email link CHỌN LỊCH
        /// trên thiết bị cá nhân của ứng viên (base URL theo môi trường, không hardcode localhost).
        /// </summary>
        /// <param name="frontendBaseUrl">Base URL portal ứng viên (controller truyền từ config).</param>
        /// <param name="roundNumber">Vòng cần mời (mặc định 1).</param>
        public async Task<Result<bool>> SendInterviewInviteAsync(Guid applicationId, string frontendBaseUrl, int roundNumber = 1, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
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
                TokenHash = HashInviteToken(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(ttlHours),
            };
            await _unitOfWork.Repository<InterviewInvite>().AddAsync(invite, ct);

            var scheduleLink = $"{baseUrl}/portal/schedule/{applicationId}?token={rawToken}&round={roundNumber}";

            var subject = "[ARISP] - Lời mời phỏng vấn: chọn lịch hẹn";
            var htmlMessage = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee;'>
            <h3 style='color: #333;'>Chào {application.CandidateName},</h3>
            <p>Chúc mừng bạn! Hồ sơ ứng tuyển của bạn đã thông qua vòng duyệt hồ sơ (CV Review).</p>
            <p>Vui lòng truy cập đường dẫn dưới đây trên thiết bị cá nhân của bạn để <strong>chọn khung giờ phỏng vấn</strong> (vòng {roundNumber}). Sau khi chọn lịch, bạn có thể luyện tập với chế độ <em>phỏng vấn thử</em> trước ngày hẹn.</p>
            <p style='text-align: center; margin: 30px 0;'>
                <a href='{scheduleLink}' style='padding: 12px 25px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>Chọn lịch phỏng vấn</a>
            </p>
            <p style='color: #666; font-size: 12px;'><i>Lưu ý: Buổi phỏng vấn thật diễn ra tại văn phòng — bạn sẽ nhập mã phỏng vấn (Interview Code) do nhân sự cấp tại chỗ, không cần đăng nhập email. Đường dẫn này dành riêng cho bạn, hết hạn sau {ttlHours} giờ.</i></p>
            <br/>
            <p>Trân trọng,</p>
            <p><strong>Đội ngũ nhân sự ARISP</strong></p>
        </div>";

            try
            {
                await _emailService.SendEmailAsync(application.CandidateEmail, subject, htmlMessage);

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
        /// Còn được phỏng vấn thử cho vòng <paramref name="roundNumber"/> không (1 lượt / vòng).
        /// Eligible = chưa có phiên practice nào của vòng này.
        /// </summary>
        public async Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (application == null)
                return Result.Failure<bool>("Application not found.");

            var used = await _unitOfWork.Repository<InterviewSession>().FindAsync(
                s => s.ApplicationId == applicationId && s.SessionType == "practice" && s.RoundNumber == roundNumber, ct);

            return Result.Success(!used.Any());
        }
    }
}
