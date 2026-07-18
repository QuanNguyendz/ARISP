using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;

namespace ARISP.Infrastructure.Media
{
    /// <summary>
    /// Mint ephemeral token Deepgram (endpoint /v1/auth/grant) để FE kết nối live STT trực tiếp,
    /// không lộ API key gốc. Token TTL ngắn (mặc định 60s) — đủ để mở WebSocket STT.
    /// </summary>
    public class DeepgramTokenService : IDeepgramTokenService
    {
        private readonly HttpClient _http;
        private readonly DeepgramOptions _options;

        public DeepgramTokenService(HttpClient http, MediaOptions media)
        {
            _http = http;
            _options = media.Deepgram;
        }

        public async Task<DeepgramToken?> CreateTemporaryTokenAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                return null;

            var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/v1/auth/grant";
            var payload = new { ttl_seconds = _options.TokenTtlSeconds };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Token {_options.ApiKey}");

            var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Deepgram grant token lỗi {(int)resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var token = root.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            var expires = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var ei) ? ei : _options.TokenTtlSeconds;

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Deepgram grant token: thiếu access_token trong response.");

            return new DeepgramToken(token, expires, _options.Model);
        }
    }
}
