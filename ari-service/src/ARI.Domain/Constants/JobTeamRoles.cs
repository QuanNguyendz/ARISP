using System;
using System.Collections.Generic;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Vai trò của một người TRONG MỘT TIN cụ thể (<see cref="Entities.JobHiringTeamMember"/>).
    ///
    /// Khác với <see cref="RoleNames"/> — vai trò tài khoản, áp cho toàn hệ thống. Một người có
    /// vai trò tài khoản <c>hiring_manager</c> vẫn không thấy tin nào cho tới khi được gán vào
    /// đội tuyển dụng của tin đó.
    /// </summary>
    public static class JobTeamRoles
    {
        /// <summary>Trưởng bộ phận có nhu cầu tuyển — duyệt shortlist, ký JD, chốt kết quả.</summary>
        public const string HiringManager = "hiring_manager";

        /// <summary>
        /// Người tham gia phỏng vấn cùng. CHƯA dùng tới: buổi phỏng vấn do AI thực hiện (ADR-043),
        /// giá trị này để dành cho trường hợp doanh nghiệp muốn thêm người nghe/nhận xét.
        /// </summary>
        public const string Interviewer = "interviewer";

        /// <summary>Chỉ theo dõi tiến trình, không có quyền quyết định nào.</summary>
        public const string Observer = "observer";

        public static readonly string[] All = { HiringManager, Interviewer, Observer };

        private static readonly HashSet<string> AllSet = new(All, StringComparer.OrdinalIgnoreCase);

        /// <summary>Chuẩn hoá về dạng lưu DB; <c>null</c> nếu không nhận ra.</summary>
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim().ToLowerInvariant();
            return AllSet.Contains(normalized) ? normalized : null;
        }

        public static bool Is(string? actual, string expected) =>
            Normalize(actual) is { } normalized && string.Equals(normalized, expected, StringComparison.Ordinal);
    }
}
