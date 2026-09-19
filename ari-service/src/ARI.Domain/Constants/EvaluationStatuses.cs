using System;
using System.Collections.Generic;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Việc sinh báo cáo đánh giá của một phiên đang ở đâu — cột <c>interview_sessions.evaluation_status</c>
    /// (ADR-073).
    ///
    /// Trước đây không có cột này: báo cáo được sinh NGAY trong lệnh đóng phiên, và mọi lý do không sinh
    /// được (tin chưa có bộ tiêu chí, AI lỗi, model không trả điểm) chỉ để lại một dòng log. Giao diện suy
    /// "phiên xong mà chưa có báo cáo" thành "AI đang chấm" và quay mãi — không ai biết phải làm gì.
    /// Mỗi giá trị ở đây là một câu trả lời cho câu hỏi "vì sao chưa có báo cáo, và ai phải làm gì".
    /// </summary>
    public static class EvaluationStatuses
    {
        /// <summary>Phiên đã đóng, đang chờ hàng đợi chấm nhận việc.</summary>
        public const string Pending = "pending";

        /// <summary>Đang gọi AI chấm. Kẹt quá lâu (tiến trình chết giữa chừng) thì lượt quét đưa lại vào hàng.</summary>
        public const string Processing = "processing";

        /// <summary>Đã có báo cáo.</summary>
        public const string Done = "done";

        /// <summary>
        /// Tin chưa có bộ tiêu chí chấm phỏng vấn — việc của Hiring Manager. Khai xong là phiên tự được chấm,
        /// không ai phải bấm gì thêm.
        /// </summary>
        public const string BlockedNoRubric = "blocked_no_rubric";

        /// <summary>
        /// Buổi THỬ mà ứng viên không trả lời câu nào — không có gì để nhận xét. Buổi thật không dừng ở đây:
        /// nó được ghi một báo cáo hệ thống để Hiring Manager vẫn chốt được kết quả.
        /// </summary>
        public const string NoAnswers = "no_answers";

        /// <summary>AI lỗi hoặc model không trả điểm tiêu chí nào — được thử lại tự động tới <see cref="MaxAttempts"/> lượt.</summary>
        public const string Failed = "failed";

        /// <summary>Số lượt gọi AI tối đa cho một phiên trước khi dừng hẳn và chờ người bấm "Chấm lại".</summary>
        public const int MaxAttempts = 3;

        public static readonly string[] All = { Pending, Processing, Done, BlockedNoRubric, NoAnswers, Failed };

        private static readonly HashSet<string> AllSet = new(All, StringComparer.OrdinalIgnoreCase);

        public static bool IsKnown(string? status) =>
            !string.IsNullOrWhiteSpace(status) && AllSet.Contains(status.Trim());

        public static bool Is(string? actual, string expected) =>
            !string.IsNullOrWhiteSpace(actual)
            && string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }
}
