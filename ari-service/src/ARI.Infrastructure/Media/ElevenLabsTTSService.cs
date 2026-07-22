using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Infrastructure.Media
{
    /// <summary>
    /// TTS qua ElevenLabs Flash v2.5 (ADR-005/043). Trả MP3 stream cho luồng không-avatar
    /// (fallback / phỏng vấn thật không HeyGen). Với avatar HeyGen, giọng ElevenLabs được
    /// route qua HeyGen voice_id (xem HeyGenAvatarService) nên service này không nằm trên
    /// critical path của avatar live.
    /// </summary>
    public class ElevenLabsTTSService : ITTSService
    {
        private readonly HttpClient _http;
        private readonly ElevenLabsOptions _options;

        public ElevenLabsTTSService(HttpClient http, MediaOptions media)
        {
            _http = http;
            _options = media.ElevenLabs;
        }

        public async Task<Stream> TextToSpeechAsync(string text, string voiceId, CancellationToken ct = default)
        {
            var voice = string.IsNullOrWhiteSpace(voiceId) ? _options.DefaultVoiceId : voiceId;
            if (string.IsNullOrWhiteSpace(voice))
                throw new InvalidOperationException("ElevenLabs voiceId chưa được cấu hình (Media:ElevenLabs:DefaultVoiceId).");

            var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/v1/text-to-speech/{voice}/stream?output_format=mp3_44100_128";
            var payload = new
            {
                text,
                model_id = _options.ModelId,
                voice_settings = new { stability = 0.5, similarity_boost = 0.75 }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("xi-api-key", _options.ApiKey);

            var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"ElevenLabs TTS lỗi {(int)resp.StatusCode}: {body}");
            }

            // Đọc toàn bộ vào MemoryStream để caller tự do dispose response.
            var ms = new MemoryStream();
            await resp.Content.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ms;
        }

        public async Task<string> TextToSpeechBase64PcmAsync(string text, string voiceId, CancellationToken ct = default)
        {
            var voice = string.IsNullOrWhiteSpace(voiceId) ? _options.DefaultVoiceId : voiceId;
            if (string.IsNullOrWhiteSpace(voice))
                throw new InvalidOperationException("ElevenLabs voiceId chưa được cấu hình (Media:ElevenLabs:DefaultVoiceId).");

            // PCM 24kHz raw (định dạng repeatAudio của LiveAvatar cần) qua endpoint thường —
            // nhanh hơn with-timestamps (không phải chờ tính alignment) + optimize_streaming_latency.
            var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/v1/text-to-speech/{voice}?output_format=pcm_24000&optimize_streaming_latency=3";
            var payload = new { text, model_id = _options.ModelId };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("xi-api-key", _options.ApiKey);

            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"ElevenLabs TTS (pcm) lỗi {(int)resp.StatusCode}: {body}");
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            return Convert.ToBase64String(bytes);
        }
    }
}
