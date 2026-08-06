using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARI.Domain.Constants
{
    public static class AppRoles
    {
        // Người trong công ty (trong class Users)
        public const string SuperAdmin = "Super_admin";
        public const string HrAdmin = "Hr_admin";
        public const string Recruiter = "Recruiter";

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
