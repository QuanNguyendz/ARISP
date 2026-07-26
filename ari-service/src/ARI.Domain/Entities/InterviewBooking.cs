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
        /// <summary>Thời điểm ứng viên xác nhận/từ chối lịch (null nếu chưa phản hồi).</summary>
        public DateTimeOffset? RespondedAt { get; set; }

        public bool Reminder24hSent { get; set; } = false;
        public bool Reminder1hSent { get; set; } = false;
        public Guid? RescheduledFromId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
