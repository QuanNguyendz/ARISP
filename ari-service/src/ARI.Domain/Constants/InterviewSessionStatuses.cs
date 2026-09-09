using System;
using System.Collections.Generic;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Vòng đời của <see cref="Entities.InterviewSession"/> — cột <c>interview_sessions.status</c>.
    ///
    /// <see cref="Waiting"/> là giá trị thêm ở ADR-067 và là điểm cần chú ý: buổi phỏng vấn THẬT nay
    /// bắt đầu ở phòng chờ chứ không vào thẳng <see cref="Active"/>. Điều kiện để phiên chạy được là
    /// <b>Hiring Manager đã vào phòng và bấm cho ứng viên vào</b>.
    ///
    /// Chốt chặn nằm ở đúng một chỗ: <c>GenerateAndSendNextQuestionAsync</c> chỉ chạy khi phiên
    /// <see cref="Active"/>. Nghĩa là ứng viên có gọi thẳng SignalR cũng không moi được câu hỏi nào ra
    /// trước khi được cho vào — không cần rải thêm câu <c>if</c> nào ở tầng giao diện.
    /// </summary>
    public static class InterviewSessionStatuses
    {
        /// <summary>Đã tạo nhưng chưa chạy (giá trị mặc định lịch sử).</summary>
        public const string Pending = "pending";

        /// <summary>
        /// Buổi THẬT: ứng viên đã nhập mã và đang ở phòng chờ, AI chưa hỏi câu nào.
        /// Rời khỏi trạng thái này chỉ bằng thao tác cho vào của Hiring Manager (hoặc quản trị viên
        /// dự phòng khi tin chưa gán HM).
        /// </summary>
        public const string Waiting = "waiting";

        /// <summary>Đang phỏng vấn — AI được phép sinh câu hỏi.</summary>
        public const string Active = "active";

        public const string Completed = "completed";
        public const string Aborted = "aborted";
        public const string Error = "error";

        public static readonly string[] All = { Pending, Waiting, Active, Completed, Aborted, Error };

        /// <summary>Phiên đã đóng — không nhận thêm câu hỏi/câu trả lời nào nữa.</summary>
        public static readonly string[] Closed = { Completed, Aborted, Error };

        private static readonly HashSet<string> ClosedSet = new(Closed, StringComparer.OrdinalIgnoreCase);

        public static bool Is(string? actual, string expected) =>
            !string.IsNullOrWhiteSpace(actual)
            && string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        public static bool IsClosed(string? status) =>
            !string.IsNullOrWhiteSpace(status) && ClosedSet.Contains(status.Trim());
    }
}
