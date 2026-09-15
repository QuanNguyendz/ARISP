using System;
using System.Collections.Generic;
using System.Linq;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Các LOẠI vòng phỏng vấn mà một tin có thể cấu hình.
    ///
    /// <b>Vì sao gom thành hằng số.</b> Ba giá trị này vốn là chuỗi trần rải khắp nơi, và mỗi chỗ tự
    /// phân loại theo cách riêng — trong đó có nhiều bản viết `type == "technical" ? … : …`, tức là
    /// một phép chọn HAI nhánh cho một tập BA giá trị, nên vòng trắc nghiệm bị in ra thành "Sơ loại"
    /// ở bốn màn khác nhau. Một danh sách có tên là chỗ để hỏi "còn giá trị nào nữa không".
    /// </summary>
    public static class InterviewRoundTypes
    {
        /// <summary>Bài thi trắc nghiệm trực tuyến — ứng viên làm tại nhà, KHÔNG có Hiring Manager.</summary>
        public const string OnlineTest = "online_test";

        /// <summary>Vòng sơ loại: hội thoại với AI, đánh giá cả nội dung lẫn năng lực ngôn ngữ.</summary>
        public const string Screening = "screening";

        /// <summary>Vòng chuyên môn: hội thoại với AI, đi sâu kỹ năng kỹ thuật.</summary>
        public const string Technical = "technical";

        /// <summary>Thứ tự này cũng là thứ tự gợi ý khi dựng tin: thi sàng lọc trước, chuyên môn sau.</summary>
        public static readonly string[] All = { OnlineTest, Screening, Technical };

        /// <summary>Trần số vòng của một tin — nhiều hơn thì phễu dài tới mức không ai đi hết.</summary>
        public const int MaxRounds = 5;

        private static readonly HashSet<string> Known =
            new(All, StringComparer.OrdinalIgnoreCase);

        /// <summary>Chuẩn hoá về đúng dạng lưu DB; <c>null</c> nếu không nhận ra.</summary>
        public static string? Normalize(string? value)
        {
            var v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return Known.Contains(v) ? v : null;
        }

        public static bool IsValid(string? value) => Normalize(value) != null;

        /// <summary>
        /// Vòng có cần Hiring Manager CÓ MẶT không.
        ///
        /// Đây là vị từ đứng sau các luật của ADR-067 (một ca một ứng viên · ca phải nằm trong khung
        /// HM rảnh · ca không chồng giờ · HM không dự hai buổi cùng lúc · phòng chờ đợi HM cho vào)
        /// và việc HM có phải khai lịch cho vòng đó không. Vòng trắc nghiệm trả <c>false</c>: bài thi
        /// trực tuyến không có ai ngồi cùng.
        /// </summary>
        public static bool NeedsHiringManager(string? roundType) =>
            !string.Equals(Normalize(roundType), OnlineTest, StringComparison.Ordinal);

        /// <summary>
        /// Lọc + chuẩn hoá một danh sách vòng người dùng gửi lên. Giữ nguyên THỨ TỰ (thứ tự chính là
        /// số vòng), bỏ giá trị lạ, bỏ trùng liền kề không có ý nghĩa gì nên giữ nguyên cả trùng.
        /// </summary>
        public static List<string> Sanitize(IEnumerable<string>? values) =>
            (values ?? Array.Empty<string>())
                .Select(Normalize)
                .Where(v => v != null)
                .Select(v => v!)
                .ToList();
    }
}
