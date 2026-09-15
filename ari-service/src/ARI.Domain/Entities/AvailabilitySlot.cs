using System;

namespace ARI.Domain.Entities
{
    public class AvailabilitySlot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

        /// <summary>
        /// Số ứng viên tối đa của ca. <b><c>null</c> = KHÔNG giới hạn.</b>
        ///
        /// Vì sao cần "không giới hạn" chứ không phải một con số rất lớn: vòng TRẮC NGHIỆM là bài
        /// thi trực tuyến, ai cũng có thể làm trong cùng một khung giờ — không có ghế nào để đếm.
        /// Một con số lớn giả (999) sẽ phải giải thích ở mọi chỗ hiển thị ("Đã đặt 3/999") và vẫn
        /// sai bản chất.
        ///
        /// Vì sao vẫn giữ được cột số: Recruiter <b>có thể</b> đặt giới hạn cho vòng trắc nghiệm
        /// (mở đợt thi nhỏ). Còn vòng hội thoại thì luôn bằng 1 — Hiring Manager ngồi cùng AI nên
        /// một ca không phục vụ được hai người (ADR-067).
        ///
        /// <c>booked_count</c> vẫn tăng/giảm bình thường khi không giới hạn: nó là con số "bao nhiêu
        /// người đã đăng ký", vai trò khoá tương tranh chỉ có ý nghĩa khi có trần (ADR-058).
        /// </summary>
        public int? Capacity { get; set; } = 1;

        public int BookedCount { get; set; } = 0;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
