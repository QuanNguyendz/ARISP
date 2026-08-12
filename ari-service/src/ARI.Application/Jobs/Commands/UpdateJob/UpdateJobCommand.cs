using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Services;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Jobs.Commands.UpdateJob
{
    /// <summary>
    /// HR cập nhật job posting kèm cấu hình vòng phỏng vấn. Chỉ người tạo hoặc
    /// SuperAdmin/HrAdmin; không cho cập nhật khi đã archived. Recruiter chỉ được
    /// sửa tin khi còn nháp (draft) hoặc bị từ chối (rejected) — đã gửi HR duyệt
    /// (pending) hay đã duyệt (active...) thì khoá.
    /// </summary>
    public record UpdateJobCommand(Guid Id, CreateJobPostingRequest Request, Guid UserId, string? Role)
        : IRequest<Result<JobPostingResponse>>;

    public class UpdateJobCommandHandler : IRequestHandler<UpdateJobCommand, Result<JobPostingResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public UpdateJobCommandHandler(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        public async Task<Result<JobPostingResponse>> Handle(UpdateJobCommand command, CancellationToken ct)
        {
            var request = command.Request;

            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(command.Id, ct);
            if (job == null)
                return Result.Failure<JobPostingResponse>("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);

            // 1. Validate ownership & roles
            var isAuthorized = command.Role == AppRoles.SuperAdmin ||
                               command.Role == AppRoles.HrAdmin ||
                               job.CreatedByUserId == command.UserId;
            if (!isAuthorized)
                return Result.Failure<JobPostingResponse>("Bạn không có quyền cập nhật tin tuyển dụng này.", CommonErrorCodes.Forbidden);

            // 2. Không cho update nếu Status == "archived"
            if (string.Equals(job.Status, "archived", StringComparison.OrdinalIgnoreCase))
                return Result.Failure<JobPostingResponse>("Không thể cập nhật tin tuyển dụng đã lưu trữ (archived).");

            // 2b. Recruiter chỉ được sửa tin khi còn nháp hoặc bị từ chối — đã gửi HR duyệt (pending)
            //     hoặc đã duyệt (active...) thì khoá. SuperAdmin/HrAdmin không bị giới hạn này.
            var isPrivileged = command.Role == AppRoles.SuperAdmin || command.Role == AppRoles.HrAdmin;
            if (!isPrivileged &&
                !string.Equals(job.Status, "draft", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(job.Status, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<JobPostingResponse>(
                    "Tin đã gửi HR duyệt hoặc đã được duyệt nên không thể chỉnh sửa. Chỉ sửa được tin ở trạng thái nháp hoặc khi bị từ chối.",
                    CommonErrorCodes.Forbidden);
            }

            // 3. Validation logic (Đồng bộ với CreateJob)
            var validationError = JobsSupport.ValidateJobRequest(request, job.ApplicationDeadline, isUpdate: true);
            if (validationError != null)
                return Result.Failure<JobPostingResponse>(validationError);

            var detectedLang = JobDescriptionLanguageDetector.Detect(request.JobDescription);

            // 4. Update fields
            job.Title = request.Title!.Trim();
            job.Department = request.Department?.Trim();
            job.JobDescription = request.JobDescription!.Trim();
            // Chỉ ghi đè file JD khi request có gửi (tránh xoá file cũ khi edit không đổi JD)
            if (!string.IsNullOrWhiteSpace(request.JdFileUrl))
            {
                job.JdFileUrl = request.JdFileUrl;
                job.JdFileName = request.JdFileName;
                job.JdFileFormat = request.JdFileFormat;
            }
            job.InterviewMode = request.InterviewMode;
            job.IsPublicListing = request.IsPublicListing;
            job.DetectedLanguage = detectedLang;
            job.LanguageRequirement = !string.IsNullOrWhiteSpace(request.LanguageRequirement)
                                        ? request.LanguageRequirement
                                        : (detectedLang == "vi" ? "Tiếng Việt" : "Yêu cầu ngôn ngữ " + detectedLang);
            job.RescheduleDeadlineHours = request.RescheduleDeadlineHours;
            job.InviteTokenTtlHours = request.InviteTokenTtlHours;
            job.ScoringRubric = request.ScoringRubric.HasValue ? request.ScoringRubric.Value.GetRawText() : null;
            job.PersonaName = request.PersonaName;
            job.PersonaVoiceId = request.PersonaVoiceId;
            job.PersonaStyle = request.PersonaStyle;
            job.Location = request.Location;
            job.WorkMode = request.WorkMode;
            job.SalaryMin = request.SalaryMin;
            job.SalaryMax = request.SalaryMax;
            job.SalaryCurrency = string.IsNullOrWhiteSpace(request.SalaryCurrency) ? "VND" : request.SalaryCurrency;
            job.SalaryIsNegotiable = request.SalaryIsNegotiable;
            job.EmploymentType = request.EmploymentType;
            job.ExperienceLevel = request.ExperienceLevel;
            job.Skills = request.Skills ?? new List<string>();
            job.JobCategory = request.JobCategory?.ToLower();
            job.ApplicationDeadline = request.ApplicationDeadline;
            job.IsUrgent = request.IsUrgent;
            job.Vacancies = request.Vacancies.HasValue && request.Vacancies.Value > 0 ? request.Vacancies : null;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<JobPosting>().Update(job);
            await _unitOfWork.SaveChangesAsync(ct);

            // 5. Re-create InterviewRoundConfig nếu có sự thay đổi
            var existingRounds = await _unitOfWork.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == command.Id, ct);
            var existingList = existingRounds.OrderBy(r => r.RoundNumber).ToList();
            var requestList = request.RoundConfigs.OrderBy(r => r.RoundNumber).ToList();

            bool roundsChanged = existingList.Count != requestList.Count;
            if (!roundsChanged)
            {
                for (int i = 0; i < existingList.Count; i++)
                {
                    var ext = existingList[i];
                    var req = requestList[i];
                    if (ext.RoundNumber != req.RoundNumber ||
                        ext.RoundType != req.RoundType ||
                        ext.InterviewLanguage != (req.InterviewLanguage ?? detectedLang) ||
                        ext.InterviewCodeTtlHours != req.InterviewCodeTtlHours ||
                        ext.MaxDurationMinutes != req.MaxDurationMinutes)
                    {
                        roundsChanged = true;
                        break;
                    }
                }
            }

            var finalRoundDtos = new List<RoundConfigDto>();
            if (roundsChanged)
            {
                foreach (var round in existingRounds)
                {
                    _unitOfWork.Repository<InterviewRoundConfig>().Delete(round);
                }
                await _unitOfWork.SaveChangesAsync(ct);

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
                    finalRoundDtos.Add(RoundConfigDto.FromEntity(config));
                }
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else
            {
                finalRoundDtos = existingList.Select(RoundConfigDto.FromEntity).ToList();
            }

            // Gửi thông báo SignalR cho Recruiter vừa update job (nếu có)
            await _notificationService.PublishUserEventAsync(command.UserId, "ReceiveJobPostingUpdate", new { JobId = job.Id, Status = job.Status, Title = job.Title }, ct);

            // Nếu Job đang active (đang hiển thị công khai), thì thông báo cho tất cả ứng viên để cập nhật Job Board
            if (job.Status == "active")
            {
                await _notificationService.PublishAllEventAsync("ReceivePublicJobUpdate", new { JobId = job.Id, Status = job.Status }, ct);
            }

            return Result.Success(JobPostingResponse.FromEntity(job, finalRoundDtos));
        }
    }
}
