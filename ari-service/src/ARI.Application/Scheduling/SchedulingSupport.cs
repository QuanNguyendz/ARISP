using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Scheduling
{
    /// <summary>Helpers dùng chung của feature Scheduling — chuyển verbatim từ 2 controller cũ.</summary>
    internal static class SchedulingSupport
    {
        /// <summary>
        /// Xác thực quyền truy cập hồ sơ (phía ứng viên): token lời mời hợp lệ HOẶC candidate
        /// đăng nhập sở hữu hồ sơ (theo account id hoặc email claim).
        /// </summary>
        public static async Task<(bool ok, ARI.Domain.Entities.Application? app, string? error)> AuthorizeCandidateAsync(
            IUnitOfWork unitOfWork, Guid applicationId, int round, string? token,
            Guid? accountId, string? email, CancellationToken ct)
        {
            var app = await unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (app == null) return (false, null, "Không tìm thấy hồ sơ ứng tuyển.");

            // 1) Token lời mời
            if (!string.IsNullOrWhiteSpace(token))
            {
                var hash = TokenHashing.Sha256Hex(token);
                var invites = await unitOfWork.Repository<InterviewInvite>().FindAsync(
                    i => i.ApplicationId == applicationId && i.RoundNumber == round && i.TokenHash == hash, ct);
                var invite = invites.FirstOrDefault();
                if (invite != null && invite.ExpiresAt > DateTimeOffset.UtcNow)
                    return (true, app, null);
                if (invite != null) return (false, app, "Lời mời đã hết hạn. Vui lòng liên hệ nhân sự để được gửi lại.");
            }

            // 2) Candidate đăng nhập sở hữu hồ sơ
            var byAccount = accountId.HasValue && app.CandidateAccountId == accountId.Value;
            var byEmail = !string.IsNullOrEmpty(email) &&
                          string.Equals(app.CandidateEmail, email, StringComparison.OrdinalIgnoreCase);
            if (byAccount || byEmail) return (true, app, null);

            return (false, app, "Bạn không có quyền truy cập lịch của hồ sơ này (thiếu token hợp lệ hoặc chưa đăng nhập).");
        }

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
