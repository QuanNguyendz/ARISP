using System;
using ARISP.Domain.Entities;

namespace ARISP.Application.DTOs
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
}
