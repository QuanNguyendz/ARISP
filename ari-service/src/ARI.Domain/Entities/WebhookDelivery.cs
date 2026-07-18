using System;

namespace ARISP.Domain.Entities
{
    public class WebhookDelivery
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = "{}"; // JSON payload
        public int? ResponseStatus { get; set; }
        public string? ResponseBody { get; set; }
        public int AttemptCount { get; set; } = 1;
        public DateTimeOffset? DeliveredAt { get; set; }
        public DateTimeOffset? NextRetryAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
