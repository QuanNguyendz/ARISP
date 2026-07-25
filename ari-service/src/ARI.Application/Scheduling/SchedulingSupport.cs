using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Scheduling
{
    /// <summary>Helpers dùng chung của feature Scheduling.</summary>
    internal static class SchedulingSupport
    {
        /// <summary>Kiểm tra staff có quyền quản lý slot của job này không (chủ tin hoặc admin).</summary>
        public static async Task<(bool ok, JobPosting? job)> CanManageAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
        {
            var job = await unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null) return (false, null);
            if (userId is not { } uid || uid == Guid.Empty) return (false, job);
            var isAdmin = role == AppRoles.SuperAdmin || role == AppRoles.HrAdmin;
            return (isAdmin || job.CreatedByUserId == uid, job);
        }
    }
}
