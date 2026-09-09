namespace ARI.Infrastructure.Media
{
    /// <summary>
    /// Cấu hình media stack phỏng vấn realtime (ADR-043/044). Bind từ section "Media".
    /// Avatar (HeyGen LiveAvatar) đã gỡ khỏi dự án ở ADR-067 — chỉ còn STT + TTS.
    /// </summary>
    public class MediaOptions
    {
        public DeepgramOptions Deepgram { get; set; } = new();
        public ElevenLabsOptions ElevenLabs { get; set; } = new();
    }

    public class DeepgramOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        // nova-2 hỗ trợ đầy đủ 36 ngôn ngữ gồm tiếng Việt + Anh (ổn định cho streaming đa ngôn ngữ).
        public string Model { get; set; } = "nova-2";
        /// <summary>
        /// TTL token ngắn hạn cấp cho FE (giây). Phải đủ dài để FE dựng xong SignalR + thiết bị
        /// rồi mới mở WebSocket STT (token chỉ dùng lúc handshake — phiên STT không bị cắt khi token hết hạn).
        /// </summary>
        public int TokenTtlSeconds { get; set; } = 300;
        public string ApiBaseUrl { get; set; } = "https://api.deepgram.com";
    }

    public class ElevenLabsOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ModelId { get; set; } = "eleven_flash_v2_5";
        public string DefaultVoiceId { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = "https://api.elevenlabs.io";
    }

}
