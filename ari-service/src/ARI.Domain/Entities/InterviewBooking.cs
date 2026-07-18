using System;

namespace ARISP.Domain.Entities
{
    public class InterviewBooking
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public Guid AvailabilitySlotId { get; set; }
        public int RoundNumber { get; set; } = 1;
        public string? InterviewLink { get; set; }
        public string Status { get; set; } = "scheduled"; // scheduled | completed | cancelled | rescheduled
        public bool Reminder24hSent { get; set; } = false;
        public bool Reminder1hSent { get; set; } = false;
        public Guid? RescheduledFromId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
