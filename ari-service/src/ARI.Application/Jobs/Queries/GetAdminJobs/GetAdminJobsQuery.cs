using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Jobs.Queries.GetAdminJobs
{
    /// <summary>
    /// Danh sách job dành cho nhân sự nội bộ (bao gồm cả draft, closed...).
    ///
    /// Phạm vi do SERVER quyết định theo vai trò (<see cref="JobAccess.ScopedJobIdsAsync"/>);
    /// <paramref name="MineOnly"/> chỉ là bộ lọc giao diện "chỉ hiện tin tôi tạo", thu hẹp thêm
    /// bên trong phạm vi đã được phép. Trước đây phạm vi do CLIENT khai qua <c>?mine=true</c>,
    /// nên bỏ tham số đi là thấy mọi tin của công ty.
    /// </summary>
    public record GetAdminJobsQuery(Guid? UserId, string? Role, bool MineOnly)
        : IRequest<Result<List<JobPostingListItemResponse>>>;

    public class GetAdminJobsQueryHandler : IRequestHandler<GetAdminJobsQuery, Result<List<JobPostingListItemResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAdminJobsQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<JobPostingListItemResponse>>> Handle(GetAdminJobsQuery request, CancellationToken ct)
        {
            var scope = await JobAccess.ScopedJobIdsAsync(_unitOfWork, request.UserId, request.Role, ct);
            var mineUid = request.MineOnly ? request.UserId : null;

            // Projection thẳng sang DTO danh sách — KHÔNG kéo JobDescription/ScoringRubric/persona/JD file.
            var jobList = await _unitOfWork.Repository<JobPosting>().QueryAsync(q =>
            {
                var query = scope == null ? q : q.Where(j => scope.Contains(j.Id));
                if (mineUid.HasValue) query = query.Where(j => j.CreatedByUserId == mineUid.Value);
                return query
                    .OrderByDescending(j => j.CreatedAt)
                    .Select(j => new JobPostingListItemResponse
                    {
                        Id = j.Id,
                        Title = j.Title,
                        Department = j.Department,
                        InterviewMode = j.InterviewMode,
                        Status = (j.Status == "active" && j.ApplicationDeadline.HasValue && j.ApplicationDeadline.Value <= DateTimeOffset.UtcNow) ? "closed" : j.Status,
                        DetectedLanguage = j.DetectedLanguage,
                        LanguageRequirement = j.LanguageRequirement,
                        CreatedAt = j.CreatedAt,
                        PublishedAt = j.PublishedAt,
                        ApplicationDeadline = j.ApplicationDeadline,
                        Location = j.Location,
                        WorkMode = j.WorkMode,
                        EmploymentType = j.EmploymentType,
                        ExperienceLevel = j.ExperienceLevel,
                        JobCategory = j.JobCategory,
                        IsUrgent = j.IsUrgent ?? false,
                        Vacancies = j.Vacancies,
                        Skills = j.Skills,
                        SalaryMin = j.SalaryMin,
                        SalaryMax = j.SalaryMax,
                        SalaryCurrency = j.SalaryCurrency,
                        SalaryIsNegotiable = j.SalaryIsNegotiable ?? false,
                        CreatedByUserId = j.CreatedByUserId,
                        RejectionReason = j.RejectionReason,
                        HmSignOffStatus = j.HmSignOffStatus,
                    });
            }, ct);

            // Đếm ứng viên theo tin bằng SQL GROUP BY (không nạp Application/CvText).
            var jobIds = jobList.Select(j => j.Id).ToList();
            var countByJob = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                    .QueryAsync(q => q.Where(a => jobIds.Contains(a.JobPostingId))
                        .GroupBy(a => a.JobPostingId)
                        .Select(g => new { JobId = g.Key, Count = g.Count() }), ct))
                .ToDictionary(x => x.JobId, x => x.Count);

            // Hiring Manager chính của từng tin (ADR-061) — một truy vấn cho cả danh sách, không N+1.
            var primaryHmByJob = (await _unitOfWork.Repository<JobHiringTeamMember>()
                    .QueryAsync(q => q.Where(m => jobIds.Contains(m.JobPostingId)
                                                  && m.IsPrimary
                                                  && m.RoleOnJob == JobTeamRoles.HiringManager)
                        .Select(m => new { m.JobPostingId, m.UserId }), ct))
                .ToDictionary(m => m.JobPostingId, m => m.UserId);

            // Tên người tạo tin + tên Hiring Manager gộp chung một lượt đọc bảng users.
            var userIds = jobList.Select(j => j.CreatedByUserId)
                .Concat(primaryHmByJob.Values)
                .Distinct()
                .ToList();
            var usersById = (await _unitOfWork.Repository<User>()
                    .QueryAsync(q => q.Where(u => userIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email, u.Role }), ct))
                .ToDictionary(u => u.Id, u => new
                {
                    Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName,
                    // Trước đây câu tam phân ở đây bỏ sót hiring_manager nên cột "Người tạo" hiện
                    // thẳng chuỗi thô `hiring_manager` ra màn hình.
                    RoleLabel = RoleNames.DisplayLabel(u.Role)
                });

            foreach (var dto in jobList)
            {
                dto.Skills ??= new List<string>();
                dto.ApplicantCount = countByJob.TryGetValue(dto.Id, out var c) ? c : 0;
                dto.CreatedByName = usersById.TryGetValue(dto.CreatedByUserId, out var creator)
                    ? $"{creator.Name} ({creator.RoleLabel})"
                    : null;

                if (primaryHmByJob.TryGetValue(dto.Id, out var hmUserId))
                {
                    dto.RequiresHmApproval = true;
                    dto.HiringManagerName = usersById.TryGetValue(hmUserId, out var hm) ? hm.Name : null;
                }
            }

            return Result.Success(jobList);
        }
    }
}
