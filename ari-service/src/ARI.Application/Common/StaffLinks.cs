using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Common
{
    /// <summary>
    /// Đường dẫn màn hình nhân sự cho link trong THÔNG BÁO, chọn theo VAI TRÒ người nhận.
    ///
    /// Mỗi vai trò có workspace riêng (<c>/hr</c>, <c>/recruiter</c>, <c>/hm</c>, <c>/super-admin</c>) và
    /// <c>ProtectedRoute</c> ở frontend so vai trò CHÍNH XÁC — link sai workspace là người nhận bấm vào
    /// thì gặp trang 403. Trước đây mỗi chỗ tự viết cứng một link (`/recruiter/my-jobs/…` cho mọi chủ
    /// tin, `/hr/offers` cho cả Recruiter), nên tin do HR tạo hay thư do HM soạn đều dẫn sai chỗ.
    ///
    /// Super Admin không có màn tin / thư mời riêng: link rơi về dashboard của họ thay vì 403.
    /// </summary>
    public static class StaffLinks
    {
        public static string Job(string? role, Guid jobId)
        {
            if (RoleNames.Is(role, RoleNames.Recruiter)) return $"/recruiter/my-jobs/{jobId}";
            if (RoleNames.Is(role, RoleNames.HiringManager)) return $"/hm/jobs/{jobId}";
            if (RoleNames.Is(role, RoleNames.SuperAdmin)) return "/super-admin/dashboard";
            return $"/hr/jobs/{jobId}";
        }

        public static string Candidate(string? role, Guid applicationId)
        {
            if (RoleNames.Is(role, RoleNames.Recruiter)) return $"/recruiter/candidates/{applicationId}";
            if (RoleNames.Is(role, RoleNames.HiringManager)) return $"/hm/candidates/{applicationId}";
            if (RoleNames.Is(role, RoleNames.SuperAdmin)) return "/super-admin/dashboard";
            return $"/hr/candidates/{applicationId}";
        }

        public static string Offers(string? role)
        {
            if (RoleNames.Is(role, RoleNames.Recruiter)) return "/recruiter/offers";
            if (RoleNames.Is(role, RoleNames.HiringManager)) return "/hm/offers";
            if (RoleNames.Is(role, RoleNames.SuperAdmin)) return "/super-admin/dashboard";
            return "/hr/offers";
        }

        public static async Task<string> JobAsync(IUnitOfWork uow, Guid recipientUserId, Guid jobId, CancellationToken ct)
            => Job(await RoleOfAsync(uow, recipientUserId, ct), jobId);

        public static async Task<string> CandidateAsync(IUnitOfWork uow, Guid recipientUserId, Guid applicationId, CancellationToken ct)
            => Candidate(await RoleOfAsync(uow, recipientUserId, ct), applicationId);

        public static async Task<string> OffersAsync(IUnitOfWork uow, Guid recipientUserId, CancellationToken ct)
            => Offers(await RoleOfAsync(uow, recipientUserId, ct));

        private static async Task<string?> RoleOfAsync(IUnitOfWork uow, Guid userId, CancellationToken ct)
            => (await uow.Repository<User>().GetByIdAsync(userId, ct))?.Role;
    }
}
