using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    public interface ITTSService
    {
        Task<Stream> TextToSpeechAsync(string text, string voiceId, CancellationToken ct = default);

        /// <summary>
        /// TTS trả base64 PCM 24kHz — FE phát thẳng qua WebAudio (ADR-067 gỡ avatar).
        /// Rỗng nếu chưa cấu hình ElevenLabs key/voice.
        /// </summary>
        Task<string> TextToSpeechBase64PcmAsync(string text, string voiceId, CancellationToken ct = default);
    }
}
