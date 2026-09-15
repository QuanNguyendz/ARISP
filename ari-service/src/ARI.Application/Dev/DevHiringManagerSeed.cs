using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Dev
{
    /// <summary>
    /// DEV-ONLY: Hiring Manager dùng chung cho các tin sandbox (ADR-068).
    ///
    /// Mọi tin đều phải có Hiring Manager chính — kể cả tin seed. Trước đây hai tin sandbox không có HM
    /// nào, nên chúng chạy đúng "đường không HM" (vào phòng thẳng, không cổng nào) mà nay đã bị gỡ khỏi
    /// hệ thống: seed không còn phản ánh luồng thật. Tài khoản này ĐĂNG NHẬP ĐƯỢC để thử phòng chờ —
    /// HM vào phòng rồi bấm cho ứng viên vào.
    /// </summary>
    internal static class DevHiringManagerSeed
    {
        public const string Email = "hm.dev@arisp.local";
        public const string Password = "Hm123456!";

        /// <summary>Tạo (hoặc dùng lại) tài khoản HM dev và gán làm HM chính của tin nếu tin chưa có.</summary>
        public static async Task<User> EnsureAsync(
            IUnitOfWork uow, IPasswordHasher passwordHasher, JobPosting job, CancellationToken ct)
        {
            var hm = (await uow.Repository<User>().FindAsync(u => u.Email == Email, ct)).FirstOrDefault();
            if (hm == null)
            {
                hm = new User
                {
                    Email = Email,
                    PasswordHash = passwordHasher.Hash(Password),
                    Role = RoleNames.HiringManager,
                    FullName = "HM Dev",
                    IsActive = true,
                };
                await uow.Repository<User>().AddAsync(hm, ct);
                await uow.SaveChangesAsync(ct);
            }
            else if (string.IsNullOrEmpty(hm.PasswordHash))
            {
                hm.PasswordHash = passwordHasher.Hash(Password);
                uow.Repository<User>().Update(hm);
            }

            var hasPrimary = (await uow.Repository<JobHiringTeamMember>().FindAsync(
                    m => m.JobPostingId == job.Id && m.IsPrimary && m.RoleOnJob == JobTeamRoles.HiringManager, ct))
                .Any();
            if (!hasPrimary)
            {
                await uow.Repository<JobHiringTeamMember>().AddAsync(new JobHiringTeamMember
                {
                    JobPostingId = job.Id,
                    UserId = hm.Id,
                    RoleOnJob = JobTeamRoles.HiringManager,
                    IsPrimary = true,
                    AddedByUserId = job.CreatedByUserId,
                }, ct);
            }

            // Tin sandbox đã `active` — đánh dấu đã ký để màn HM không hiện nó như tin đang chờ ký.
            if (string.IsNullOrEmpty(job.HmSignOffStatus))
            {
                job.HmSignOffStatus = HmSignOffStatus.Approved;
                job.HmSignOffByUserId = hm.Id;
                job.HmSignOffAt = DateTimeOffset.UtcNow;
                uow.Repository<JobPosting>().Update(job);
            }

            await uow.SaveChangesAsync(ct);
            return hm;
        }
    }
}
