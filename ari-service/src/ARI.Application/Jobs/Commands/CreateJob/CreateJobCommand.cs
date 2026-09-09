using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.RecruitmentRequests;
using ARI.Application.Services;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ARI.Application.Jobs.Commands.CreateJob
{
    /// <summary>HR tạo job posting kèm cấu hình vòng phỏng vấn + ingest JD vào RAG.</summary>
    public record CreateJobCommand(CreateJobPostingRequest Request, Guid UserId) : IRequest<Result<JobPostingResponse>>;

    public class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, Result<JobPostingResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRagIngestionService _ragIngestion;
        private readonly INotificationService _notificationService;
        private readonly ILogger<CreateJobCommandHandler> _logger;

        public CreateJobCommandHandler(
            IUnitOfWork unitOfWork,
            IRagIngestionService ragIngestion,
            INotificationService notificationService,
            ILogger<CreateJobCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _ragIngestion = ragIngestion;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<Result<JobPostingResponse>> Handle(CreateJobCommand command, CancellationToken ct)
        {
            var request = command.Request;

            var validationError = JobsSupport.ValidateJobRequest(request, existingDeadline: null, isUpdate: false);
            if (validationError != null)
                return Result.Failure<JobPostingResponse>(validationError);

            var creator = await _unitOfWork.Repository<User>().GetByIdAsync(command.UserId, ct);
            if (creator == null)
                return Result.Failure<JobPostingResponse>("User not found for the current token.", CommonErrorCodes.Unauthorized);

            // === ADR-063: mọi tin phải bắt nguồn từ một phiếu yêu cầu tuyển dụng đã duyệt ===
            //
            // Cưỡng chế ở ĐÂY chứ không bằng ràng buộc NOT NULL trên cột: cột phải để null được cho
            // những tin có trước ADR-063 (xem chú thích ở entity). Handler là nơi duy nhất phân biệt
            // được "tin mới" với "dữ liệu lịch sử".
            if (request.RecruitmentRequestId is not { } requestId)
                return Result.Failure<JobPostingResponse>(
                    "Tin tuyển dụng phải được tạo từ một phiếu yêu cầu tuyển dụng đã duyệt.");

            // Phiếu phải đã duyệt VÀ người dựng tin phải là Recruiter được phân công (hoặc admin).
            // Dùng chung `RecruitmentRequestAccess` với trình soạn JD của ADR-064 — hai chỗ trả lời
            // cùng một câu hỏi, để mỗi bên tự viết lại thì chỉ cần một bên được nới là quyền rò qua
            // đường đó.
            var (recruitmentRequest, accessError, accessCode) =
                await RecruitmentRequestAccess.LoadExecutableAsync(
                    _unitOfWork, requestId, command.UserId, creator.Role, ct);

            if (recruitmentRequest == null)
                return accessCode == null
                    ? Result.Failure<JobPostingResponse>(accessError!)
                    : Result.Failure<JobPostingResponse>(accessError!, accessCode);

            // Một phiếu chỉ sinh một tin. DB cũng có unique index chặn hai request đồng thời; kiểm
            // ở đây để trả về câu tiếng Việt thay vì lỗi ràng buộc thô.
            var existing = await _unitOfWork.Repository<JobPosting>().CountAsync(
                j => j.RecruitmentRequestId == requestId && j.DeletedAt == null, ct);
            if (existing > 0)
                return Result.Failure<JobPostingResponse>(
                    "Phiếu này đã có tin tuyển dụng rồi.", CommonErrorCodes.Conflict);

            var detectedLang = JobDescriptionLanguageDetector.Detect(request.JobDescription);

            var job = new JobPosting
            {
                CreatedByUserId = command.UserId,
                Title = request.Title!.Trim(),
                Department = request.Department?.Trim(),
                JobDescription = request.JobDescription!.Trim(),
                JdFileUrl = request.JdFileUrl,
                JdFileName = request.JdFileName,
                JdFileFormat = request.JdFileFormat,
                InterviewMode = request.InterviewMode,
                Status = "draft",
                IsPublicListing = request.IsPublicListing,
                DetectedLanguage = detectedLang,
                LanguageRequirement = !string.IsNullOrWhiteSpace(request.LanguageRequirement)
                                        ? request.LanguageRequirement
                                        : (detectedLang == "vi" ? "Tiếng Việt" : "Yêu cầu ngôn ngữ " + detectedLang),
                RescheduleDeadlineHours = request.RescheduleDeadlineHours,
                InviteTokenTtlHours = request.InviteTokenTtlHours,
                ScoringRubric = request.ScoringRubric.HasValue ? request.ScoringRubric.Value.GetRawText() : null,
                InterviewPassScore = Math.Clamp(request.InterviewPassScore, 0, 100),
                PersonaName = request.PersonaName,
                PersonaVoiceId = request.PersonaVoiceId,
                PersonaStyle = request.PersonaStyle,
                Location = request.Location,
                WorkMode = request.WorkMode,
                SalaryMin = request.SalaryMin,
                SalaryMax = request.SalaryMax,
                SalaryCurrency = string.IsNullOrWhiteSpace(request.SalaryCurrency) ? "VND" : request.SalaryCurrency,
                SalaryIsNegotiable = request.SalaryIsNegotiable,
                EmploymentType = request.EmploymentType,
                ExperienceLevel = request.ExperienceLevel,
                Skills = request.Skills ?? new List<string>(),
                JobCategory = request.JobCategory?.ToLower(),
                ApplicationDeadline = request.ApplicationDeadline,
                IsUrgent = request.IsUrgent,
                Vacancies = request.Vacancies.HasValue && request.Vacancies.Value > 0 ? request.Vacancies : null,
                RecruitmentRequestId = requestId
            };

            await _unitOfWork.Repository<JobPosting>().AddAsync(job, ct);

            // Người lập phiếu TRỞ THÀNH Hiring Manager của tin, tự động.
            //
            // Đây là mắt xích không được để hở: ADR-061 quy định cổng ký duyệt JD chỉ tồn tại khi
            // tin CÓ người được gán làm HM ("cờ bật cổng suy ra từ việc có ai được gán"). Nếu
            // Recruiter phải nhớ gán tay thì quên một lần là tin đi thẳng ra job board mà không ai
            // ký duyệt — mà bản thân phiếu đã nói rõ ai là người có nhu cầu tuyển.
            await _unitOfWork.Repository<JobHiringTeamMember>().AddAsync(new JobHiringTeamMember
            {
                JobPostingId = job.Id,
                UserId = recruitmentRequest.RequestedByUserId,
                RoleOnJob = JobTeamRoles.HiringManager,
                IsPrimary = true,
                AddedByUserId = command.UserId,
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            // Ingest JD vào RAG (chunk+embed+pgvector) để retrieve khi phỏng vấn. Không chặn
            // tạo job nếu RAG service lỗi — chỉ ghi log (có thể re-ingest sau).
            if (!string.IsNullOrWhiteSpace(job.JobDescription))
            {
                try
                {
                    await _ragIngestion.IngestAsync("jd", job.Id, job.JobDescription, ct: ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RAG ingest JD thất bại cho job {JobId} — bỏ qua, có thể re-ingest sau.", job.Id);
                }
            }

            var roundDtos = new List<RoundConfigDto>();
            foreach (var round in request.RoundConfigs.OrderBy(r => r.RoundNumber))
            {
                var config = new InterviewRoundConfig
                {
                    JobPostingId = job.Id,
                    RoundNumber = round.RoundNumber,
                    RoundType = round.RoundType,
                    InterviewLanguage = round.InterviewLanguage ?? detectedLang,
                    InterviewCodeTtlHours = round.InterviewCodeTtlHours,
                    MaxDurationMinutes = round.MaxDurationMinutes
                };
                await _unitOfWork.Repository<InterviewRoundConfig>().AddAsync(config, ct);
                roundDtos.Add(RoundConfigDto.FromEntity(config));
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // Gửi thông báo SignalR cho Recruiter vừa tạo job
            await _notificationService.PublishUserEventAsync(command.UserId, "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = job.Status, Title = job.Title }, ct);

            return Result.Success(JobPostingResponse.FromEntity(job, roundDtos));
        }
    }
}
