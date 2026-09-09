using System;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Khung giờ Hiring Manager có mặt được cho một VÒNG của một tin (ADR-067).
    ///
    /// <b>Vì sao là bảng riêng chứ không dùng lại <see cref="AvailabilitySlot"/>.</b> Hai thứ trả lời
    /// hai câu hỏi khác nhau: <c>availability_slots</c> là <i>ca phỏng vấn</i> — có sức chứa, có người
    /// đặt, ứng viên được gán vào; còn bảng này là <i>lời khai rảnh</i> của HM — không ai "đặt" vào nó,
    /// nó chỉ giới hạn ca nào được phép dùng. Nhét chung một bảng thì cột <c>booked_count</c> mất nghĩa
    /// với một nửa số dòng, và mọi truy vấn đếm chỗ phải nhớ lọc thêm một cờ — quên một chỗ là đếm sai.
    ///
    /// <b>Vì sao gắn theo VÒNG.</b> HM dự buổi thật của từng vòng, mà các vòng cách nhau nhiều ngày;
    /// một danh sách rảnh dùng chung cho mọi vòng sẽ hoặc quá chặt (vòng 3 phải nằm trong lịch khai
    /// từ vòng 1) hoặc vô nghĩa.
    ///
    /// Không xoá mềm: đây là dữ liệu vận hành ngắn hạn, khai sai thì xoá và khai lại.
    /// </summary>
    public class HiringManagerAvailability
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid JobPostingId { get; set; }

        /// <summary>Vòng phỏng vấn mà khung giờ này áp dụng.</summary>
        public int RoundNumber { get; set; } = 1;

        /// <summary>
        /// Người khai. Giữ lại vì tin có thể đổi Hiring Manager giữa chừng — lúc đó phải phân biệt
        /// được khung giờ nào là của người đang phụ trách, khung nào là di sản của người trước.
        /// </summary>
        public Guid HiringManagerUserId { get; set; }

        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }

        /// <summary>Ghi chú tuỳ chọn của HM ("chỉ họp online", "sau 15h thì gấp")—hiển thị cho Recruiter.</summary>
        public string? Note { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
