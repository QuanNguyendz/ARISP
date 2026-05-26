using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    public interface ITTSService
    {
        Task<Stream> TextToSpeechAsync(string text, string voiceId, CancellationToken ct = default);
    }
}
