using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    public interface ITTSService
    {
        Task<Stream> TextToSpeechAsync(string text, string voiceId, CancellationToken ct = default);

        /// <summary>
        /// TTS trả base64 PCM 24kHz — định dạng HeyGen LiveAvatar repeatAudio() cần (ADR-044).
        /// Rỗng nếu chưa cấu hình ElevenLabs key/voice.
        /// </summary>
        Task<string> TextToSpeechBase64PcmAsync(string text, string voiceId, CancellationToken ct = default);
    }
}
