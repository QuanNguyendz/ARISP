using System;
using ARI.Domain.Entities;

namespace ARI.Application.DTOs
{
    /// <summary>Khung giờ phỏng vấn (slot) trả về cho client.</summary>
    public class AvailabilitySlotResponse
    {
        public Guid Id { get; set; }
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public int Capacity { get; set; }
        public int BookedCount { get; set; }
        /// <summary>Còn chỗ trống để ứng viên đặt không.</summary>
        public bool IsAvailable => BookedCount < Capacity;

        /// <summary>
        /// Ứng viên đang giữ chỗ ở ca này. Rỗng khi chưa ai đặt.
        ///
        /// Vì sao trả kèm chứ không để giao diện tự tra: màn cấu hình lịch trước đây chỉ hiện
        /// "Đã đặt 0/1" — một con số không nói được ca đó đang giữ chỗ cho AI, và người vận hành
        /// phải sang màn khác mới biết có được đụng vào ca này không.
        /// </summary>
        public List<SlotBookingBriefDto> Bookings { get; set; } = new();

        public static AvailabilitySlotResponse FromEntity(AvailabilitySlot s) => new()
        {
            Id = s.Id,
            JobPostingId = s.JobPostingId,
            RoundNumber = s.RoundNumber,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Timezone = s.Timezone,
            Capacity = s.Capacity,
            BookedCount = s.BookedCount,
        };
    }

    /// <summary>Ứng viên đang giữ một chỗ trong ca — vừa đủ để nhận ra người đó trên màn lịch.</summary>
    public class SlotBookingBriefDto
    {
        public Guid BookingId { get; set; }
        public Guid ApplicationId { get; set; }
        public string? CandidateName { get; set; }
        public string? CandidateEmail { get; set; }
        /// <summary>pending | confirmed | declined — ứng viên đã xác nhận giờ hẹn chưa (ADR-048).</summary>
        public string ConfirmationStatus { get; set; } = "pending";
    }

    /// <summary>Tạo một khung giờ phỏng vấn cho job + vòng.</summary>
    public class CreateSlotRequest
    {
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public int Capacity { get; set; } = 1;
    }

    public class UpdateSlotCapacityRequest
    {
        public int Capacity { get; set; }
    }

    /// <summary>
    /// Một mục lịch phỏng vấn của ứng viên (kèm thông tin booking để xác nhận/từ chối) — ADR-048.
    /// </summary>
    public class CandidateScheduleItemDto
    {
        public Guid BookingId { get; set; }
        public Guid ApplicationId { get; set; }
        public string? JobTitle { get; set; }
        public int RoundNumber { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        /// <summary>pending | confirmed | declined — phản hồi của ứng viên với lịch được gán.</summary>
        public string ConfirmationStatus { get; set; } = "pending";
        /// <summary>Lý do đã từ chối (khi ConfirmationStatus = declined).</summary>
        public string? DeclineReason { get; set; }
    }

    /// <summary>Body ứng viên từ chối lịch (bận) kèm lý do để nhân sự xếp lại.</summary>
    public class DeclineScheduleRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
