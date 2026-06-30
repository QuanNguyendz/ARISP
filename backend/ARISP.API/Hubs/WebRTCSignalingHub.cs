using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ARISP.Application.Interfaces;

namespace ARISP.API.Hubs
{
    [Authorize]
    public class WebRTCSignalingHub : Hub
    {
        private readonly IAvatarService _avatarService;

        public WebRTCSignalingHub(IAvatarService avatarService)
        {
            _avatarService = avatarService;
        }

        public async Task SubmitSdpAnswer(string sessionId, SdpMessage answer)
        {
            // Forward the SDP answer to HeyGen Streaming API via AvatarService
            var result = await _avatarService.SubmitSdpAnswerAsync(sessionId, answer);
            await Clients.Caller.SendAsync("ReceiveSdpConfirmation", result);
        }

        public async Task SendIceCandidate(string sessionId, IceCandidateMessage candidate)
        {
            // Send client's ICE candidate to HeyGen
            var result = await _avatarService.SendIceCandidateAsync(sessionId, candidate);
            await Clients.Caller.SendAsync("ReceiveIceConfirmation", result);
        }
    }
}
