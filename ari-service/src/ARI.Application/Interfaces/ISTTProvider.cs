using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    public interface ISTTProvider
    {
        Task<string> SpeechToTextAsync(Stream audioStream, string languageCode = "vi", CancellationToken ct = default);
    }
}
