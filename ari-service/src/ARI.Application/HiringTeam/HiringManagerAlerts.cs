using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.Offers;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.HiringTeam
{
    /// <summary>
    /// Báo HR Leader khi có tin mà vị trí Hiring Manager chính KHÔNG còn người hành động được (ADR-068).
    ///
    /// Khoá / xoá / đổi vai trò một tài khoản là thao tác an ninh nên CỐ Ý không bị chặn — nhân viên
    /// nghỉ việc hay tài khoản bị lộ thì phải khoá được ngay. Cái giá là các tin người đó đang phụ trách
    /// đóng cổng (không ai duyệt được) cho tới khi HR Leader chuyển HM. Vì thế phải NÓI RA ngay lúc đó,
    /// kèm danh sách tin — nếu không, thứ đầu tiên ai đó nhận ra là một phễu đứng im không rõ lý do.
    /// </summary>
    internal static class HiringManagerAlerts
    {
        /// <summary>Tin chưa lưu trữ mà <paramref name="userId"/> đang giữ vị trí Hiring Manager chính.</summary>
        public static async Task<List<JobPosting>> OpenJobsHeldByAsync(IUnitOfWork uow, Guid userId, CancellationToken ct)
        {
            var jobIds = (await uow.Repository<JobHiringTeamMember>().QueryAsync(
                    q => q.Where(m => m.UserId == userId && m.IsPrimary && m.RoleOnJob == JobTeamRoles.HiringManager)
                          .Select(m => m.JobPostingId), ct))
                .ToList();
            if (jobIds.Count == 0) return new List<JobPosting>();

            return (await uow.Repository<JobPosting>().FindAsync(
                    j => jobIds.Contains(j.Id) && j.Status != "archived", ct))
                .OrderBy(j => j.Title)
                .ToList();
        }

        /// <summary>
        /// Gửi mọi HR Leader + Super Admin đang hoạt động MỘT thông báo liệt kê các tin cần chuyển HM.
        /// Chỉ ghi vào UnitOfWork — người gọi lưu cùng giao dịch với thao tác gây ra nó.
        /// </summary>
        public static async Task NotifyAdminsAsync(
            IUnitOfWork uow, IReadOnlyList<JobPosting> jobs, string reason, string dedupPrefix, CancellationToken ct)
        {
            if (jobs.Count == 0) return;

            var admins = await uow.Repository<User>().FindAsync(
                u => (u.Role == RoleNames.HrAdmin || u.Role == RoleNames.SuperAdmin) && u.IsActive, ct);

            const int shown = 5;
            var titles = string.Join(", ", jobs.Take(shown).Select(j => $"\"{j.Title}\""))
                         + (jobs.Count > shown ? $" và {jobs.Count - shown} tin khác" : string.Empty);

            foreach (var admin in admins)
            {
                // Một tin thì mở thẳng tin đó; nhiều tin thì mở danh sách tin.
                var link = jobs.Count == 1
                    ? StaffLinks.Job(admin.Role, jobs[0].Id)
                    : RoleNames.Is(admin.Role, RoleNames.SuperAdmin) ? "/super-admin/dashboard" : "/hr/jobs";

                await OfferSupport.NotifyStaffAsync(
                    uow, admin.Id, "system", "Tin cần chuyển Hiring Manager",
                    $"{reason} Cần chuyển Hiring Manager cho: {titles}.",
                    link, $"{dedupPrefix}:{admin.Id}", ct);
            }
        }
    }
}
