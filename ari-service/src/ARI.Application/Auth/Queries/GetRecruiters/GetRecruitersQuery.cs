using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Queries.GetRecruiters
{
    /// <summary>
    /// Danh sách Recruiter kèm khối lượng công việc — màn "Quản lý Recruiter" của HR Lead.
    ///
    /// CHỈ ĐỌC. Toàn bộ vòng đời tài khoản (tạo, đổi vai trò, khoá/mở khoá) nằm ở
    /// <c>AdminController</c> với policy <c>SuperAdminOnly</c> (ADR-023: pre-provisioning;
    /// ADR-041: HR xin cấp tài khoản qua AccountRequest rồi Super Admin duyệt). Query này cố ý
    /// không mở thêm quyền nào — nó chỉ trả lời "ai đang gánh việc gì, chỗ nào đang tồn đọng".
    /// </summary>
    public record GetRecruitersQuery : IRequest<Result<List<RecruiterOverviewDto>>>;

    public class GetRecruitersQueryHandler
        : IRequestHandler<GetRecruitersQuery, Result<List<RecruiterOverviewDto>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetRecruitersQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<RecruiterOverviewDto>>> Handle(GetRecruitersQuery request, CancellationToken ct)
        {
            var recruiters = (await _unitOfWork.Repository<User>()
                .FindAsync(u => u.Role == AppRoles.Recruiter, ct)).ToList();

            if (recruiters.Count == 0)
                return Result.Success(new List<RecruiterOverviewDto>());

            var recruiterIds = recruiters.Select(r => r.Id).ToHashSet();

            // Nạp theo lô rồi gộp trong bộ nhớ: số recruiter của một doanh nghiệp là hàng chục,
            // gọi vòng lặp mỗi người một query sẽ thành N+1 vô ích.
            var jobs = (await _unitOfWork.Repository<JobPosting>()
                .FindAsync(j => j.DeletedAt == null && recruiterIds.Contains(j.CreatedByUserId), ct)).ToList();

            var jobIds = jobs.Select(j => j.Id).ToHashSet();
            var jobOwner = jobs.ToDictionary(j => j.Id, j => j.CreatedByUserId);

            var applications = jobIds.Count == 0
                ? new List<Domain.Entities.Application>()
                : (await _unitOfWork.Repository<Domain.Entities.Application>()
                    .FindAsync(a => a.DeletedAt == null && jobIds.Contains(a.JobPostingId), ct)).ToList();

            var appIds = applications.Select(a => a.Id).ToHashSet();

            // "Tồn đọng" = có đánh giá buổi THẬT mà chưa ai xác nhận. Buổi thử bị loại vì nó là
            // không gian riêng của ứng viên, nhân sự không thấy và cũng không phải duyệt (ADR-051).
            var evaluations = appIds.Count == 0
                ? new List<Evaluation>()
                : (await _unitOfWork.Repository<Evaluation>()
                    .FindAsync(e => appIds.Contains(e.ApplicationId) && e.SessionType == "real", ct)).ToList();

            var reviewedEvalIds = evaluations.Count == 0
                ? new HashSet<Guid>()
                : (await _unitOfWork.Repository<HrReview>()
                    .FindAsync(r => evaluations.Select(e => e.Id).Contains(r.EvaluationId), ct))
                    .Select(r => r.EvaluationId).ToHashSet();

            var appToOwner = applications
                .Where(a => jobOwner.ContainsKey(a.JobPostingId))
                .ToDictionary(a => a.Id, a => jobOwner[a.JobPostingId]);

            var pendingByOwner = evaluations
                .Where(e => !reviewedEvalIds.Contains(e.Id) && appToOwner.ContainsKey(e.ApplicationId))
                .GroupBy(e => appToOwner[e.ApplicationId])
                .ToDictionary(g => g.Key, g => g.Count());

            var candidatesByOwner = applications
                .Where(a => jobOwner.ContainsKey(a.JobPostingId))
                .GroupBy(a => jobOwner[a.JobPostingId])
                .ToDictionary(g => g.Key, g => g.Count());

            var result = recruiters
                .Select(user =>
                {
                    var ownJobs = jobs.Where(j => j.CreatedByUserId == user.Id).ToList();
                    return new RecruiterOverviewDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Department = user.Department,
                        IsActive = user.IsActive,
                        LockReason = user.LockReason,
                        LastLoginAt = user.LastLoginAt,
                        CreatedAt = user.CreatedAt,
                        JobsTotal = ownJobs.Count,
                        JobsActive = ownJobs.Count(j => j.Status == "active"),
                        JobsDraft = ownJobs.Count(j => j.Status == "draft"),
                        CandidatesTotal = candidatesByOwner.TryGetValue(user.Id, out var candidates) ? candidates : 0,
                        PendingReviews = pendingByOwner.TryGetValue(user.Id, out var pending) ? pending : 0,
                    };
                })
                // Người đang tồn đọng nhiều việc lên trước — đó là thứ HR Lead cần thấy ngay.
                .OrderByDescending(r => r.PendingReviews)
                .ThenByDescending(r => r.CandidatesTotal)
                .ThenBy(r => r.FullName)
                .ToList();

            return Result.Success(result);
        }
    }
}
