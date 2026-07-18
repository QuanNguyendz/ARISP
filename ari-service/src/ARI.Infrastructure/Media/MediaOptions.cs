namespace ARISP.Infrastructure.Media
{
    /// <summary>Cấu hình media stack phỏng vấn realtime (ADR-043/044). Bind từ section "Media".</summary>
    public class MediaOptions
    {
        public DeepgramOptions Deepgram { get; set; } = new();
        public ElevenLabsOptions ElevenLabs { get; set; } = new();
        public HeyGenOptions HeyGen { get; set; } = new();
    }

    public class DeepgramOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        // nova-2 hỗ trợ đầy đủ 36 ngôn ngữ gồm tiếng Việt + Anh (ổn định cho streaming đa ngôn ngữ).
        public string Model { get; set; } = "nova-2";
        /// <summary>
        /// TTL token ngắn hạn cấp cho FE (giây). Phải đủ dài để FE khởi tạo avatar/SignalR song song
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

    /// <summary>
    /// HeyGen LiveAvatar (thay Streaming Avatar API cũ đã sunset 410 — ADR-044). Dùng chế độ
    /// LITE: BE mint session token, FE chạy @heygen/liveavatar-web-sdk; avatar lip-sync audio
    /// ElevenLabs do ta cấp (repeatAudio). Giữ key path "Media:HeyGen:*" để không phải nhập lại.
    /// </summary>
    public class HeyGenOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        /// <summary>LiveAvatar API base (mint token + SDK apiUrl).</summary>
        public string ApiBaseUrl { get; set; } = "https://api.liveavatar.com";
        /// <summary>URL SDK FE kết nối (= ApiBaseUrl với LiveAvatar). Trả về cho FE.</summary>
        public string ServerUrl { get; set; } = "https://api.liveavatar.com";
        /// <summary>Avatar id của LiveAvatar (UUID, vd dd73ea75-...). KHÁC avatar id Streaming cũ.</summary>
        public string DefaultAvatarId { get; set; } = string.Empty;
        /// <summary>Voice ElevenLabs dùng cho TTS (lip-sync). Trùng Media:ElevenLabs:DefaultVoiceId.</summary>
        public string DefaultVoiceId { get; set; } = string.Empty;
        /// <summary>Sandbox mode LiveAvatar — mặc định false (production, chất lượng thật, tính phí).</summary>
        public bool IsSandbox { get; set; } = false;
    }
}
