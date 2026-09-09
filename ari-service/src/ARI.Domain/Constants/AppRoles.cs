using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Giá trị vai trò trong claim <c>role</c> của JWT. Đây KHÔNG phải giá trị lưu trong DB —
    /// cột <c>users.role</c> dùng snake_case chữ thường, xem <see cref="RoleNames"/>.
    /// Đổi bất kỳ chuỗi nào ở đây là vô hiệu hoá mọi token đã phát hành.
    /// </summary>
    public static class AppRoles
    {
        // Người trong công ty (trong class Users)
        public const string SuperAdmin = "Super_admin";
        public const string HrAdmin = "Hr_admin";
        public const string Recruiter = "Recruiter";

        /// <summary>
        /// Trưởng bộ phận có nhu cầu tuyển — duyệt shortlist, ký duyệt JD và CHỐT kết quả
        /// phỏng vấn của AI (ADR-061). Phạm vi dữ liệu theo đội tuyển dụng của từng tin.
        /// </summary>
        public const string HiringManager = "Hiring_manager";

        // Người bên ngoài công ty (trong class Candidates)
        public const string Candidate = "Candidate";

        /// <summary>
        /// Không phải người dùng — là "danh tính của MỘT phiên phỏng vấn thật tại Kiosk" (ADR-052).
        /// Token mang role này chỉ thao tác được đúng phiên ghi trong claim <c>session_id</c>,
        /// hết hạn theo buổi phỏng vấn; máy Kiosk không cần ai đăng nhập.
        /// </summary>
        public const string KioskSession = "Kiosk_session";
    }
}
