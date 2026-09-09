using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Infrastructure.Services
{
    public class MockSTTProvider : ISTTProvider
    {
        public Task<string> SpeechToTextAsync(Stream audioStream, string languageCode = "vi", CancellationToken ct = default)
        {
            // Simple mock response
            return Task.FromResult("Tôi là ứng viên Backend Developer với 3 năm kinh nghiệm lập trình C# ASP.NET Core.");
        }
    }

    public class MockTTSService : ITTSService
    {
        public Task<Stream> TextToSpeechAsync(string text, string voiceId, CancellationToken ct = default)
        {
            // Return an empty stream simulating speech audio
            var memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            writer.Write("[Speech Audio Content]");
            writer.Flush();
            memoryStream.Position = 0;
            return Task.FromResult<Stream>(memoryStream);
        }

        // Chưa cấu hình ElevenLabs → trả rỗng để FE fallback browser TTS.
        public Task<string> TextToSpeechBase64PcmAsync(string text, string voiceId, CancellationToken ct = default)
            => Task.FromResult(string.Empty);
    }

    /// <summary>Chưa cấu hình Deepgram key → trả null để FE fallback (Web Speech API hoặc nhập tay).</summary>
    public class MockDeepgramTokenService : IDeepgramTokenService
    {
        public Task<DeepgramToken?> CreateTemporaryTokenAsync(CancellationToken ct = default)
        {
            return Task.FromResult<DeepgramToken?>(null);
        }
    }

    public class MockNotificationService : INotificationService
    {
        private readonly IEmailService _emailService;

        public MockNotificationService(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string content, CancellationToken ct = default)
        {
            Console.WriteLine($"[EMAIL] Sending actual email to: {toEmail} | Subject: {subject}");
            try
            {
                await _emailService.SendEmailAsync(toEmail, subject, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to send email via SMTP: {ex.Message}");
            }
        }

        public async Task<string?> SendThreadedEmailAsync(
            string toEmail, string subject, string content, string? inReplyToMessageId = null,
            CancellationToken ct = default)
        {
            Console.WriteLine($"[EMAIL] Sending threaded email to: {toEmail} | Subject: {subject}");
            try
            {
                return await _emailService.SendThreadedEmailAsync(toEmail, subject, content, inReplyToMessageId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to send email via SMTP: {ex.Message}");
                return null;
            }
        }

        public Task SendSlackNotificationAsync(string message, CancellationToken ct = default)
        {
            Console.WriteLine($"[SLACK] Msg: {message}");
            return Task.CompletedTask;
        }

        public Task SendTeamsNotificationAsync(string message, CancellationToken ct = default)
        {
            Console.WriteLine($"[TEAMS] Msg: {message}");
            return Task.CompletedTask;
        }

        public Task PublishInterviewSessionEventAsync(Guid sessionId, string eventType, object payload, CancellationToken ct = default)
        {
            // Event publication simulation (usually via SignalR IHubContext in API layer)
            Console.WriteLine($"[SignalR EVENT] Session: {sessionId} | Event: {eventType}");
            return Task.CompletedTask;
        }

        public Task PublishUserEventAsync(Guid userId, string eventType, object payload, CancellationToken ct = default)
        {
            Console.WriteLine($"[SignalR USER EVENT] User: {userId} | Event: {eventType}");
            return Task.CompletedTask;
        }

        public Task PublishGroupEventAsync(string groupName, string eventType, object payload, CancellationToken ct = default)
        {
            Console.WriteLine($"[SignalR GROUP EVENT] Group: {groupName} | Event: {eventType}");
            return Task.CompletedTask;
        }

        public Task PublishAllEventAsync(string eventType, object payload, CancellationToken ct = default)
        {
            Console.WriteLine($"[SignalR GLOBAL EVENT] Event: {eventType}");
            return Task.CompletedTask;
        }
    }
}
