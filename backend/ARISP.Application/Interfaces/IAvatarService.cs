using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    public class SdpMessage
    {
        public string Type { get; set; } = string.Empty;
        public string Sdp { get; set; } = string.Empty;
    }

    public class IceCandidateMessage
    {
        public string Candidate { get; set; } = string.Empty;
        public string SdpMid { get; set; } = string.Empty;
        public int SdpMLineIndex { get; set; }
    }

    public interface IAvatarService
    {
        Task<SdpMessage> StartSessionAsync(string voiceId, string style, CancellationToken ct = default);
        Task<bool> SubmitSdpAnswerAsync(string sessionId, SdpMessage sdpAnswer, CancellationToken ct = default);
        Task<bool> SendIceCandidateAsync(string sessionId, IceCandidateMessage candidate, CancellationToken ct = default);
        Task<bool> SpeakTextAsync(string sessionId, string text, CancellationToken ct = default);
        Task<bool> StopSessionAsync(string sessionId, CancellationToken ct = default);
    }
}
