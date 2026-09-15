using System;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Nguồn của một bài trắc nghiệm đã nộp (<see cref="Entities.OnlineTestSubmission.SubmittedBy"/>).
    /// </summary>
    public static class OnlineTestSubmittedBy
    {
        /// <summary>
        /// Ứng viên nộp — bấm nộp, hết đồng hồ làm bài, hoặc rời trang giữa chừng (trình duyệt của
        /// chính họ gửi bài đi).
        /// </summary>
        public const string Candidate = "candidate";

        /// <summary>
        /// Hệ thống tự nộp khi bài HẾT HẠN: ứng viên đã được xếp giờ nhưng không vào làm trong khung
        /// giờ đó. Bài nộp trống, chấm như mọi bài khác (0 điểm).
        ///
        /// Đây là thứ thay cho đường "không tham dự → đánh trượt + trả chỗ" của vòng phỏng vấn: ở
        /// vòng trắc nghiệm, không vào làm bài vẫn là CÓ kết quả, và việc loại hay giữ ứng viên thuộc
        /// về Recruiter chứ không thuộc về một tác vụ nền.
        /// </summary>
        public const string System = "system";

        public static bool IsSystem(string? value) =>
            string.Equals(value, System, StringComparison.OrdinalIgnoreCase);
    }
}
