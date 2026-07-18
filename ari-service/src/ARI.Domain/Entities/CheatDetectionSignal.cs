using System;

namespace ARISP.Domain.Entities
{
    public class CheatDetectionSignal
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public string SignalType { get; set; } = string.Empty; // eye_tracking | response_timing | speech_pattern | tab_switch | focus_loss
        public string Payload { get; set; } = "{}"; // JSON payload
        public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
