using System;

namespace ARI.Domain.Entities
{
    public class InterviewBooking
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public Guid AvailabilitySlotId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string? InterviewLink { get; set; }
        public string Status { get; set; } = "scheduled"; // scheduled | completed | cancelled | rescheduled | declined

        /// <summary>Phản hồi của ứng viên với lịch được nhân sự gán: pending | confirmed | declined.</summary>
        public string ConfirmationStatus { get; set; } = "pending"; // pending | confirmed | declined
        /// <summary>Lý do ứng viên từ chối/bận (bắt buộc khi ConfirmationStatus = declined).</summary>
        public string? DeclineReason { get; set; }

        /// <summary>
        /// Ai đã đóng lịch này: candidate (tự báo bận) | system (quá hạn xác nhận) | staff (loại hồ sơ).
        /// Null = lịch chưa bị đóng. Xem <see cref="Constants.BookingDeclinedBy"/>.
        ///
        /// Tồn tại vì Status + ConfirmationStatus KHÔNG phân biệt được ba trường hợp trên — cả ba
        /// đều ghi "declined". Trước khi có cột này giao diện phải đoán qua nội dung DeclineReason.
        /// </summary>
        public string? DeclinedBy { get; set; }
        /// <summary>Thời điểm ứng viên xác nhận/từ chối lịch (null nếu chưa phản hồi).</summary>
        public DateTimeOffset? RespondedAt { get; set; }

        /// <summary>Thời điểm ứng viên tự ẩn (xoá khỏi danh sách) một lịch đã bị huỷ/từ chối.
        /// CHỈ ảnh hưởng hiển thị phía ứng viên — nhân sự vẫn thấy booking để xếp lại
        /// (Status giữ nguyên "declined"/"cancelled"). Null = chưa ẩn.</summary>
        public DateTimeOffset? CandidateDismissedAt { get; set; }

        public bool Reminder24hSent { get; set; } = false;
        public bool Reminder1hSent { get; set; } = false;
        public Guid? RescheduledFromId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
