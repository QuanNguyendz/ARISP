using System;
using System.Linq;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// ADR-068 — mọi tin luôn có đúng một Hiring Manager chính, và thiếu HM là cổng ĐÓNG (không còn "tin chưa
/// gán HM thì mọi cổng mở"). Test nào dựng tin bằng tay giờ phải dựng luôn HM, nếu không sẽ va vào chốt chặn
/// thay vì luật mà nó đang muốn kiểm.
/// </summary>
internal static class HiringManagerSeed
{
    /// <summary>Tài khoản Hiring Manager ĐANG HOẠT ĐỘNG + dòng HM chính của tin.</summary>
    public static User Primary(InMemoryUnitOfWork uow, Guid jobId, Guid? hmId = null, Guid? addedBy = null)
    {
        var hm = new User
        {
            Id = hmId ?? Guid.NewGuid(),
            Email = $"hm-{Guid.NewGuid():N}@corp.io",
            FullName = "Hiring Manager",
            Role = RoleNames.HiringManager,
            IsActive = true,
        };
        uow.Seed(hm);
        uow.Seed(new JobHiringTeamMember
        {
            JobPostingId = jobId, UserId = hm.Id, RoleOnJob = JobTeamRoles.HiringManager,
            IsPrimary = true, AddedByUserId = addedBy ?? Guid.NewGuid(),
        });
        return hm;
    }

    /// <summary>
    /// Đưa MỌI tin trong <paramref name="uow"/> về đúng bất biến trước khi gọi handler:
    /// <list type="bullet">
    /// <item>tin chưa có HM chính → gán một HM riêng (mỗi tin một người, để luật "HM không dự hai buổi trùng
    /// giờ" không bắn nhầm giữa các tin) kèm khung giờ rảnh rộng cho các vòng 1–5 — test không nói về lịch HM
    /// thì luật khớp giờ không được là thứ làm nó hỏng;</item>
    /// <item>tin đã có dòng HM chính mà thiếu tài khoản (fixture cũ chỉ gieo dòng đội) → bổ sung tài khoản
    /// đang hoạt động, KHÔNG thêm khung giờ (test đó đang tự kiểm lịch HM).</item>
    /// </list>
    /// </summary>
    public static void EnsureForAllJobs(InMemoryUnitOfWork uow)
    {
        var members = uow.Repo<JobHiringTeamMember>().Items;
        var users = uow.Repo<User>().Items;

        foreach (var job in uow.Repo<JobPosting>().Items.ToList())
        {
            var primary = members.FirstOrDefault(m =>
                m.JobPostingId == job.Id && m.IsPrimary && m.DeletedAt == null && m.RoleOnJob == JobTeamRoles.HiringManager);

            if (primary == null)
            {
                var hm = Primary(uow, job.Id, addedBy: job.CreatedByUserId);
                for (var round = 1; round <= 5; round++)
                {
                    uow.Seed(new HiringManagerAvailability
                    {
                        JobPostingId = job.Id, RoundNumber = round, HiringManagerUserId = hm.Id,
                        StartTime = DateTimeOffset.UtcNow.AddDays(-1), EndTime = DateTimeOffset.UtcNow.AddDays(90),
                    });
                }
            }
            else if (users.All(u => u.Id != primary.UserId))
            {
                uow.Seed(new User
                {
                    Id = primary.UserId, Email = $"hm-{primary.UserId:N}@corp.io", FullName = "Hiring Manager",
                    Role = RoleNames.HiringManager, IsActive = true,
                });
            }
        }
    }
}
