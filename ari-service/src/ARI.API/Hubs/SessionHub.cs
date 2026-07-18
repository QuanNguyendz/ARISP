using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ARI.Application.Hubs;
using ARI.Application.Services;

namespace ARI.API.Hubs
{
    [Authorize]
    public class SessionHub : Hub<ISessionClient>
    {
        private readonly InterviewService _interviewService;

        public SessionHub(InterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        public async Task JoinSession(string sessionIdStr)
        {
            if (Guid.TryParse(sessionIdStr, out var sessionId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString());
                await Clients.Caller.ReceiveSessionStatus("joined");
            }
        }

        public async Task StartInterview(string sessionIdStr)
        {
            if (Guid.TryParse(sessionIdStr, out var sessionId))
            {
                // Trigger the first question generation
                await _interviewService.GenerateAndSendNextQuestionAsync(sessionId);
            }
        }

        public async Task SubmitAnswerText(string sessionIdStr, string questionIdStr, string transcript, int responseTimeMs)
        {
            if (Guid.TryParse(sessionIdStr, out var sessionId) && Guid.TryParse(questionIdStr, out var questionId))
            {
                // Lưu answer nhanh (không LLM) → sinh & gửi câu hỏi kế NGAY (critical path latency),
                // phân tích adaptive difficulty chạy sau khi ứng viên đã nhận câu hỏi mới.
                await _interviewService.SaveAnswerAsync(sessionId, questionId, transcript, responseTimeMs);

                await _interviewService.GenerateAndSendNextQuestionAsync(sessionId);

                await _interviewService.AnalyzeAnswerAndAdaptAsync(sessionId, questionId, transcript);
            }
        }

        public async Task ReportCheatSignal(string sessionIdStr, string signalType, string payloadJson)
        {
            if (Guid.TryParse(sessionIdStr, out var sessionId))
            {
                // Log cheat signal or alert HR via clients
                await Clients.Group(sessionId.ToString()).ReceiveCheatAlert($"Suspicious action detected: {signalType}");
            }
        }
    }
}
