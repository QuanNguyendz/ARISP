using System;
using System.Linq;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Đơn vị tiền của mọi con số lương trong hệ thống — chỉ <c>VND</c> và <c>USD</c>.
    ///
    /// Trước đây phiếu yêu cầu, trình soạn JD, đề xuất lương của HM và thư mời đều là ô gõ tự do, nên
    /// cùng một đơn vị có thể lưu thành "VND", "vnd", "VNĐ"… Mà dữ liệu lương đi nối tiếp qua các bước
    /// (phiếu → JD → tin, đề xuất → thư mời), còn bộ lọc lương trên Job Board so khớp đúng chuỗi
    /// <c>"USD"</c> để quy đổi — một chữ "usd" viết thường là tin đó bị lọc sai mà không ai hay.
    ///
    /// Giá trị lưu DB luôn viết HOA. Ô trống vẫn hợp lệ: nơi gọi tự quyết định mặc định (thường là
    /// <see cref="Default"/>), giữ đúng hành vi cũ "không gửi đơn vị = VND".
    /// </summary>
    public static class SalaryCurrencies
    {
        public const string Vnd = "VND";
        public const string Usd = "USD";
        public const string Default = Vnd;

        public static readonly string[] All = { Vnd, Usd };

        public const string InvalidMessage = "Đơn vị tiền chỉ được chọn VND hoặc USD.";

        /// <summary>Trống hoặc là một trong <see cref="All"/> (không phân biệt hoa thường).</summary>
        public static bool IsAllowed(string? value) =>
            string.IsNullOrWhiteSpace(value)
            || All.Contains(value.Trim().ToUpperInvariant(), StringComparer.Ordinal);

        /// <summary>Chuẩn hoá về dạng viết hoa; trống hoặc không nhận ra thì về <see cref="Default"/>.</summary>
        public static string Normalize(string? value)
        {
            var v = (value ?? string.Empty).Trim().ToUpperInvariant();
            return All.Contains(v, StringComparer.Ordinal) ? v : Default;
        }
    }
}
