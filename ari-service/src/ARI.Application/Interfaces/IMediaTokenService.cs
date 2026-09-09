using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    /// <summary>Token ngắn hạn để FE kết nối trực tiếp Deepgram live STT (BE giữ API key thật).</summary>
    public record DeepgramToken(string AccessToken, int ExpiresInSeconds, string Model);

    public interface IDeepgramTokenService
    {
        /// <summary>Mint ephemeral token (TTL ngắn) cho phiên STT của ứng viên. Null nếu chưa cấu hình key.</summary>
        Task<DeepgramToken?> CreateTemporaryTokenAsync(CancellationToken ct = default);
    }
}
