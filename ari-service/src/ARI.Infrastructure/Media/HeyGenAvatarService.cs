using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;

namespace ARISP.Infrastructure.Media
{
    /// <summary>
    /// HeyGen LiveAvatar (ADR-044) — thay Streaming Avatar API cũ (đã sunset 410). Mô hình client-SDK:
    /// BE mint session token chế độ LITE (giữ API key), FE chạy @heygen/liveavatar-web-sdk, avatar
    /// lip-sync audio ElevenLabs do ta cấp qua repeatAudio (giữ não RAG/GPT-4o của ta).
    /// Các phương thức SDP cũ giữ để tương thích interface nhưng không dùng.
    /// </summary>
    public class HeyGenAvatarService : IAvatarService
    {
        private readonly HttpClient _http;
        private readonly HeyGenOptions _options;

        public HeyGenAvatarService(HttpClient http, MediaOptions media)
        {
            _http = http;
            _options = media.HeyGen;
        }

        public async Task<AvatarStreamingToken?> CreateStreamingTokenAsync(string? avatarId, string? voiceId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                return null;

            var effectiveAvatar = string.IsNullOrWhiteSpace(avatarId) ? _options.DefaultAvatarId : avatarId;
            if (string.IsNullOrWhiteSpace(effectiveAvatar))
                return null; // LiveAvatar bắt buộc avatar_id → chưa cấu hình thì để FE fallback browser TTS

            // LiveAvatar LITE: POST /v1/sessions/token { mode, avatar_id, is_sandbox } → data.session_token
            var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/v1/sessions/token";
            var payload = new { mode = "LITE", avatar_id = effectiveAvatar, is_sandbox = _options.IsSandbox };
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-API-KEY", _options.ApiKey);

            var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"LiveAvatar create token lỗi {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var token = doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("session_token", out var t)
                ? t.GetString()
                : null;
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("LiveAvatar token: thiếu data.session_token trong response.");

            return new AvatarStreamingToken(
                token,
                _options.ServerUrl,
                effectiveAvatar,
                string.IsNullOrWhiteSpace(voiceId) ? _options.DefaultVoiceId : voiceId);
        }

        // --- Luồng client-SDK không dùng SDP server-side; giữ no-op để tương thích interface ---
        public Task<SdpMessage> StartSessionAsync(string voiceId, string style, CancellationToken ct = default)
            => Task.FromResult(new SdpMessage { Type = "none", Sdp = string.Empty });

        public Task<bool> SubmitSdpAnswerAsync(string sessionId, SdpMessage sdpAnswer, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> SendIceCandidateAsync(string sessionId, IceCandidateMessage candidate, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> SpeakTextAsync(string sessionId, string text, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> StopSessionAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
