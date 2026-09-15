using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Common.Security
{
    /// <summary>Mức quyền của một nhân sự trên MỘT tin tuyển dụng. Thứ tự tăng dần có ý nghĩa.</summary>
    public enum JobAccessLevel
    {
        /// <summary>Không liên quan tới tin này — không được đọc gì.</summary>
        None = 0,

        /// <summary>
        /// Thành viên đội tuyển dụng của tin (ADR-061) — ĐỌC được hồ sơ, lịch, báo cáo AI của tin,
        /// nhưng KHÔNG sửa được chính tin đó. Hiring Manager thường ở mức này.
        /// </summary>
        TeamMember = 1,

        /// <summary>Chủ tin (người tạo) — vận hành toàn bộ phễu của tin.</summary>
        Owner = 2,

        /// <summary>HR Admin / Super Admin — mọi tin trong công ty.</summary>
        Admin = 3
    }

    /// <summary>
    /// Cổng phân quyền theo TÀI NGUYÊN cho tin tuyển dụng.
    ///
    /// VÌ SAO Ở TẦNG APPLICATION CHỨ KHÔNG PHẢI POLICY ASP.NET: id của tin đến từ ~5 hình dạng
    /// route khác nhau (<c>{id}</c>, <c>{jobId}</c>, suy từ <c>applicationId</c>, từ <c>slotId</c>,
    /// từ <c>evaluationId</c>). Một <c>IAuthorizationHandler</c> sẽ phải đoán route value cho từng
    /// dạng. Ngoài ra codebase hiện có 0 custom authorization handler — giữ nguyên phong cách để
    /// mọi cổng quyền nằm cùng một nơi và test được bằng bộ giả <c>IUnitOfWork</c> sẵn có.
    ///
    /// VÌ SAO TRẢ VỀ MỨC CHỨ KHÔNG PHẢI BOOL: đọc và ghi không cùng ngưỡng. Sửa/xoá/đổi trạng thái
    /// tin cần <see cref="JobAccessLevel.Owner"/> trở lên; xem hồ sơ và báo cáo của tin chỉ cần
    /// <see cref="JobAccessLevel.TeamMember"/>. Trước đây vị từ này bị chép thành hai bản
    /// <c>CanManageAsync</c> giống hệt nhau (Scheduling và OnlineTest) cộng thêm 7 bản nội tuyến,
    /// nên thêm một mức quyền mới đồng nghĩa với sửa 9 chỗ và chắc chắn sót.
    /// </summary>
    public static class JobAccess
    {
        /// <summary>
        /// Mức quyền của <paramref name="userId"/> trên tin <paramref name="jobPostingId"/>.
        /// <paramref name="role"/> nhận cả giá trị claim JWT lẫn giá trị DB (xem <see cref="RoleNames.IsAdmin"/>).
        /// </summary>
        public static async Task<(bool ok, JobPosting? job, JobAccessLevel level)> EvaluateAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
        {
            var job = await unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null) return (false, null, JobAccessLevel.None);

            // Quản trị viên không cần danh tính cụ thể để ĐỌC, nhưng mọi lệnh ghi đều cần ActorId
            // để ghi audit log, nên vẫn đòi userId hợp lệ như hành vi cũ.
            if (userId is not { } uid || uid == Guid.Empty)
                return (false, job, JobAccessLevel.None);

            if (RoleNames.IsAdmin(role))
                return (true, job, JobAccessLevel.Admin);

            if (job.CreatedByUserId == uid)
                return (true, job, JobAccessLevel.Owner);

            // Thành viên đội tuyển dụng của tin (ADR-061). Query filter toàn cục đã loại dòng
            // đã gỡ (deleted_at IS NULL), nên gỡ người khỏi đội là mất quyền ngay.
            var onTeam = await unitOfWork.Repository<JobHiringTeamMember>()
                .CountAsync(m => m.JobPostingId == jobPostingId && m.UserId == uid, ct);
            if (onTeam > 0)
                return (true, job, JobAccessLevel.TeamMember);

            return (false, job, JobAccessLevel.None);
        }

        /// <summary>
        /// Hiring Manager CHÍNH của tin — người mà chữ ký duyệt chặn phễu và là người chốt kết quả
        /// phỏng vấn.
        ///
        /// ADR-068: mọi tin LUÔN có một người ở vị trí này (người lập phiếu được gán lúc tạo tin; gỡ
        /// không được, chỉ chuyển được). Trả <c>null</c> chỉ còn là dữ liệu hỏng/tin cũ chưa được gán —
        /// và khi đó mọi cổng duyệt ĐÓNG, không mở. Chỗ cần chặn phễu dùng
        /// <see cref="RequireActiveHiringManagerAsync"/> thay vì tự kiểm null.
        /// </summary>
        public static async Task<JobHiringTeamMember?> PrimaryHiringManagerAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, CancellationToken ct)
        {
            var members = await unitOfWork.Repository<JobHiringTeamMember>().FindAsync(
                m => m.JobPostingId == jobPostingId
                     && m.IsPrimary
                     && m.RoleOnJob == JobTeamRoles.HiringManager, ct);
            return members.FirstOrDefault();
        }

        /// <summary>
        /// Hiring Manager chính của tin cùng tình trạng tài khoản của họ (ADR-068).
        ///
        /// Tách <see cref="HiringManagerState.Inactive"/> khỏi <see cref="HiringManagerState.Missing"/>:
        /// khoá tài khoản là thao tác an ninh nên KHÔNG bị chặn, và người bị khoá vẫn nằm ở vị trí HM
        /// cho tới khi HR Leader chuyển tin. Hai trường hợp cần câu hướng dẫn khác nhau ("gán" hay
        /// "chuyển"), còn với cổng duyệt thì như nhau: đều đóng.
        /// </summary>
        public static async Task<(JobHiringTeamMember? member, User? user, HiringManagerState state)>
            HiringManagerStatusAsync(IUnitOfWork unitOfWork, Guid jobPostingId, CancellationToken ct)
        {
            var hm = await PrimaryHiringManagerAsync(unitOfWork, jobPostingId, ct);
            if (hm == null) return (null, null, HiringManagerState.Missing);

            var user = await unitOfWork.Repository<User>().GetByIdAsync(hm.UserId, ct);
            var active = user != null
                         && user.DeletedAt == null
                         && user.IsActive
                         && RoleNames.Is(user.Role, RoleNames.HiringManager);
            return (hm, user, active ? HiringManagerState.Active : HiringManagerState.Inactive);
        }

        /// <summary>
        /// Hiring Manager chính CÒN HOẠT ĐỘNG của tin, hoặc câu lỗi nói rõ HR Leader phải làm gì.
        ///
        /// Đây là chốt chặn MẶC ĐỊNH ĐÓNG của ADR-068 — cùng tinh thần <c>DbChangeRouter</c>: thiếu người
        /// duyệt thì phễu dừng lại và nói to lên, chứ không lặng lẽ bỏ qua cổng như trước đây (khi đó một
        /// Recruiter gỡ HM khỏi đội là tự mở mọi cổng đang kiểm chính mình).
        /// </summary>
        public static async Task<(JobHiringTeamMember? member, string? error)> RequireActiveHiringManagerAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, CancellationToken ct)
        {
            var (hm, _, state) = await HiringManagerStatusAsync(unitOfWork, jobPostingId, ct);
            return state switch
            {
                HiringManagerState.Active => (hm, null),
                HiringManagerState.Inactive => (null, JobAccessErrors.HiringManagerInactive),
                _ => (null, JobAccessErrors.HiringManagerMissing),
            };
        }

        /// <summary>
        /// Người gọi có phải Hiring Manager CHÍNH của tin không. Quản trị viên KHÔNG tự động thoả: họ
        /// có đường riêng là "làm thay" (kèm lý do + audit), để dấu vết phân biệt rõ "HM đã duyệt" với
        /// "quản trị viên duyệt thay".
        /// </summary>
        public static async Task<bool> IsPrimaryHiringManagerAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? userId, CancellationToken ct)
        {
            if (userId is not { } uid || uid == Guid.Empty) return false;
            var hm = await PrimaryHiringManagerAsync(unitOfWork, jobPostingId, ct);
            return hm != null && hm.UserId == uid;
        }

        /// <summary>
        /// Có quyền QUẢN LÝ tin này không (chủ tin hoặc quản trị viên).
        ///
        /// Giữ nguyên hình dạng tuple của hai bản <c>CanManageAsync</c> cũ để chỗ gọi chỉ đổi tên,
        /// không đổi cấu trúc. Ngưỡng là <see cref="JobAccessLevel.Owner"/>: thành viên đội tuyển
        /// dụng đọc được tin nhưng KHÔNG sửa được nó.
        /// </summary>
        public static async Task<(bool ok, JobPosting? job)> CanManageAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
        {
            var (_, job, level) = await EvaluateAsync(unitOfWork, jobPostingId, userId, role, ct);
            return (level >= JobAccessLevel.Owner, job);
        }

        /// <summary>Có quyền ĐỌC dữ liệu thuộc tin này không (thành viên đội, chủ tin, hoặc quản trị viên).</summary>
        public static async Task<(bool ok, JobPosting? job)> CanViewAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
        {
            var (_, job, level) = await EvaluateAsync(unitOfWork, jobPostingId, userId, role, ct);
            return (level >= JobAccessLevel.TeamMember, job);
        }

        /// <summary>
        /// Mức quyền trên một HỒ SƠ, suy từ tin mà hồ sơ đó thuộc về. Phần lớn dữ liệu nhạy cảm
        /// (CV, bảng điểm, transcript, báo cáo AI) gắn với hồ sơ chứ không gắn thẳng với tin, nên
        /// đây là lối vào thường dùng nhất.
        /// <paramref name="application"/> trả về <c>null</c> khi không tìm thấy hồ sơ — người gọi
        /// phân biệt được 404 với 403 thay vì trả đồng một mã lỗi.
        /// </summary>
        public static async Task<(ARI.Domain.Entities.Application? application, JobPosting? job, JobAccessLevel level)>
            EvaluateApplicationAsync(
                IUnitOfWork unitOfWork, Guid applicationId, Guid? userId, string? role, CancellationToken ct)
        {
            var application = await unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .GetByIdAsync(applicationId, ct);
            if (application == null) return (null, null, JobAccessLevel.None);

            var (_, job, level) = await EvaluateAsync(unitOfWork, application.JobPostingId, userId, role, ct);
            return (application, job, level);
        }

        /// <summary>
        /// Tập id tin mà người này được phép thấy. <b><c>null</c> nghĩa là KHÔNG giới hạn</b>
        /// (quản trị viên) — người gọi phải phân biệt được "không giới hạn" với "không thấy tin nào",
        /// vì trả về tập rỗng cho quản trị viên sẽ làm trống mọi màn hình của họ.
        ///
        /// Dùng cho các endpoint DANH SÁCH, nơi trước đây phạm vi do client tự khai qua
        /// <c>?mine=true</c> — tức là bỏ tham số đi là thấy dữ liệu của cả công ty.
        /// </summary>
        public static async Task<HashSet<Guid>?> ScopedJobIdsAsync(
            IUnitOfWork unitOfWork, Guid? userId, string? role, CancellationToken ct)
        {
            if (RoleNames.IsAdmin(role)) return null;

            if (userId is not { } uid || uid == Guid.Empty)
                return new HashSet<Guid>();

            var owned = await unitOfWork.Repository<JobPosting>()
                .QueryAsync(q => q.Where(j => j.CreatedByUserId == uid).Select(j => j.Id), ct);

            // Hợp thêm tin được gán vào đội tuyển dụng — nguồn phạm vi DUY NHẤT của Hiring Manager,
            // và cũng áp cho Recruiter (một recruiter có thể được mời vào đội của tin người khác).
            var assigned = await unitOfWork.Repository<JobHiringTeamMember>()
                .QueryAsync(q => q.Where(m => m.UserId == uid).Select(m => m.JobPostingId), ct);

            var scope = owned.ToHashSet();
            scope.UnionWith(assigned);
            return scope;
        }
    }

    /// <summary>
    /// Thông báo lỗi dùng chung cho các cổng phân quyền, để mọi endpoint trả về cùng một câu chữ
    /// thay vì mỗi chỗ tự nghĩ một câu (người dùng không đoán được đâu là "không tồn tại" đâu là
    /// "không được phép").
    /// </summary>
    public static class JobAccessErrors
    {
        public const string ApplicationNotFound = "Không tìm thấy hồ sơ ứng tuyển này.";
        public const string ApplicationForbidden = "Bạn không có quyền xem hồ sơ thuộc tin tuyển dụng này.";
        public const string ApplicationManageForbidden = "Bạn không có quyền thao tác trên hồ sơ thuộc tin tuyển dụng này.";
        public const string EvaluationNotFound = "Không tìm thấy báo cáo đánh giá này.";
        public const string EvaluationForbidden = "Bạn không có quyền xem báo cáo đánh giá thuộc tin tuyển dụng này.";

        /// <summary>ADR-068 — tin không có Hiring Manager chính (tin cũ chưa được gán).</summary>
        public const string HiringManagerMissing =
            "Tin này chưa có Hiring Manager phụ trách. HR Leader cần gán Hiring Manager cho tin trước khi đi tiếp.";

        /// <summary>ADR-068 — Hiring Manager chính đã bị khoá, xoá hoặc không còn vai trò Hiring Manager.</summary>
        public const string HiringManagerInactive =
            "Hiring Manager của tin này không còn hoạt động. HR Leader cần chuyển tin cho một Hiring Manager khác.";
    }

    /// <summary>Giá trị chuỗi của <see cref="HiringManagerState"/> trong DTO trả về cho frontend.</summary>
    public static class HiringManagerStateNames
    {
        public const string Active = "active";
        public const string Inactive = "inactive";
        public const string Missing = "missing";

        public static string Of(HiringManagerState state) => state switch
        {
            HiringManagerState.Active => Active,
            HiringManagerState.Inactive => Inactive,
            _ => Missing,
        };
    }

    /// <summary>Tình trạng vị trí Hiring Manager chính của một tin (ADR-068).</summary>
    public enum HiringManagerState
    {
        /// <summary>Có người, tài khoản đang hoạt động và đúng vai trò — cổng duyệt vận hành bình thường.</summary>
        Active = 0,

        /// <summary>Có người nhưng đã bị khoá / xoá / đổi vai trò — cổng đóng, chờ HR Leader chuyển.</summary>
        Inactive = 1,

        /// <summary>Không có ai (dữ liệu có trước ADR-063 chưa được gán) — cổng đóng, chờ HR Leader gán.</summary>
        Missing = 2,
    }
}
