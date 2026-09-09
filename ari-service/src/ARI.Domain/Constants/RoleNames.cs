using System;
using System.Collections.Generic;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Giá trị vai trò <b>LƯU TRONG DB</b> (cột <c>users.role</c>) — luôn snake_case chữ thường.
    ///
    /// Hệ thống có HAI bộ từ vựng cho cùng một khái niệm: bộ này (giá trị DB) và
    /// <see cref="AppRoles"/> (giá trị claim <c>role</c> trong JWT, viết hoa chữ đầu).
    /// Hai bộ đó KHÔNG thay thế cho nhau được, và việc lẫn lộn chúng đã gây lỗi thật:
    /// <c>GetRecruitersQuery</c> so <c>u.Role == AppRoles.Recruiter</c> ("Recruiter") ngay trong
    /// một biểu thức EF — dịch ra SQL <c>=</c> phân biệt hoa thường trên Postgres — nên màn
    /// "Phân công &amp; tải tuyển dụng" luôn trả về rỗng dù công ty có bao nhiêu recruiter.
    ///
    /// QUY TẮC: truy vấn DB thì dùng lớp này; kiểm tra claim/policy thì dùng <see cref="AppRoles"/>;
    /// chuyển giữa hai bên thì dùng <see cref="ToClaim"/> — không viết switch tay ở nơi khác nữa.
    /// </summary>
    public static class RoleNames
    {
        public const string SuperAdmin = "super_admin";
        public const string HrAdmin = "hr_admin";
        public const string Recruiter = "recruiter";

        /// <summary>
        /// Trưởng bộ phận có nhu cầu tuyển — người ra quyết định tuyển hay không (ADR-061).
        /// Phạm vi dữ liệu tính theo đội tuyển dụng của từng tin (<c>job_hiring_team_members</c>),
        /// KHÔNG theo phòng ban: <c>users.department</c> chỉ là nhãn tự do dùng để gợi ý lúc gán.
        /// </summary>
        public const string HiringManager = "hiring_manager";

        /// <summary>Mọi vai trò nhân sự nội bộ hợp lệ — dùng cho ràng buộc CHECK ở DB.</summary>
        public static readonly string[] All = { SuperAdmin, HrAdmin, Recruiter, HiringManager };

        /// <summary>
        /// Vai trò mà Super Admin cấp được cho tài khoản nhân viên. <see cref="SuperAdmin"/> KHÔNG
        /// nằm ở đây: quyền quản trị tối cao không cấp qua màn quản lý tài khoản.
        /// </summary>
        public static readonly string[] AssignableStaff = { HrAdmin, Recruiter, HiringManager };

        private static readonly HashSet<string> AllSet = new(All, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Chuẩn hoá một giá trị vai trò về đúng dạng lưu DB, chấp nhận cả dạng claim
        /// ("Hr_admin"), dạng hoa toàn phần ("HR_ADMIN") và khoảng trắng thừa.
        /// Trả <c>null</c> nếu không nhận ra — người gọi tự quyết định coi đó là lỗi hay bỏ qua.
        /// </summary>
        public static string? NormalizeDbRole(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim().ToLowerInvariant();
            return AllSet.Contains(normalized) ? normalized : null;
        }

        /// <summary>
        /// Giá trị DB → giá trị claim <c>role</c> trong JWT. Đây là bảng ánh xạ DUY NHẤT giữa hai
        /// bộ từ vựng; giá trị lạ được trả nguyên văn để token cũ/dữ liệu lệch không làm hỏng đăng nhập.
        /// </summary>
        public static string ToClaim(string? dbRole) => NormalizeDbRole(dbRole) switch
        {
            SuperAdmin => AppRoles.SuperAdmin,
            HrAdmin => AppRoles.HrAdmin,
            Recruiter => AppRoles.Recruiter,
            HiringManager => AppRoles.HiringManager,
            _ => dbRole ?? string.Empty
        };

        /// <summary>
        /// So khớp vai trò bất kể giá trị đến từ DB hay từ claim. Dùng khi không kiểm soát được
        /// nguồn của <paramref name="actual"/> (ví dụ role đọc ra từ command đi qua controller).
        /// </summary>
        public static bool Is(string? actual, string dbRole) =>
            NormalizeDbRole(actual) is { } normalized && string.Equals(normalized, dbRole, StringComparison.Ordinal);

        /// <summary>
        /// Quản trị viên — thấy và thao tác được trên mọi tin tuyển dụng, không cần là chủ tin
        /// hay thành viên đội tuyển dụng. Nhận cả dạng DB lẫn dạng claim.
        /// </summary>
        public static bool IsAdmin(string? role) => Is(role, SuperAdmin) || Is(role, HrAdmin);

        /// <summary>
        /// Nhãn tiếng người của vai trò, dùng ở thư và ở các chỗ backend ghép sẵn chuỗi hiển thị.
        ///
        /// Gom về đây vì câu switch này từng được chép tay ở nhiều nơi và mỗi bản sót một vai khác
        /// nhau: thư chào mừng gán nhãn "Recruiter" cho tài khoản Hiring Manager, còn danh sách tin
        /// thì hiện thẳng chuỗi thô <c>hiring_manager</c> ra màn hình. Giá trị lạ trả về nguyên văn
        /// thay vì rỗng, để dữ liệu lệch vẫn đọc được thay vì biến mất.
        /// </summary>
        public static string DisplayLabel(string? role) => NormalizeDbRole(role) switch
        {
            SuperAdmin => "Super Admin",
            HrAdmin => "HR Admin",
            Recruiter => "Recruiter",
            HiringManager => "Hiring Manager",
            _ => role ?? string.Empty
        };
    }
}
