using System;
using System.Linq;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Mức độ ưu tiên của phiếu yêu cầu tuyển dụng (ADR-065).
    ///
    /// Không phải nhãn trang trí: hàng chờ duyệt của HR Leader xếp theo thứ tự này, nên cần một
    /// **thứ hạng số** để đẩy được xuống SQL — sắp bằng chuỗi thì `high` nằm sau `low` theo bảng chữ cái.
    /// </summary>
    public static class RecruitmentPriority
    {
        public const string High = "high";
        public const string Medium = "medium";
        public const string Low = "low";

        public static readonly string[] All = { High, Medium, Low };

        public static bool IsValid(string? value) =>
            value != null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

        public static string Normalize(string? value)
        {
            var v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return IsValid(v) ? v : Medium;
        }

        /// <summary>
        /// Thứ hạng để sắp xếp (nhỏ hơn = lên trước). Dùng trong biểu thức LINQ nên phải là phép so
        /// sánh đơn giản, không tra Dictionary — EF không dịch được Dictionary sang SQL.
        /// </summary>
        public static int Rank(string? value) => Normalize(value) switch
        {
            High => 0,
            Medium => 1,
            _ => 2,
        };
    }
}
