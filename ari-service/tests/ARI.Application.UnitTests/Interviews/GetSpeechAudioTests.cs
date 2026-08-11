using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Tổng hợp giọng nói câu hỏi (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>GetSpeechAudioAsync</c>, test-plan B25): text rỗng → short-circuit Success(''); IDOR; TTS lỗi →
/// Success('') để FE fallback browser TTS (không sập phòng). Factory 2-arg dùng TTS all-throwing.
/// </summary>
public class GetSpeechAudioTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService());

    private static InterviewSession Session(Guid appId) =>
        new() { ApplicationId = appId, RoundNumber = 1, SessionType = "real", Status = "active" };

    [Fact]
    public async Task Whitespace_text_short_circuits_to_empty_success()
    {
        var res = await Svc(new InMemoryUnitOfWork())
            .GetSpeechAudioAsync(Guid.NewGuid(), "   ", Guid.NewGuid(), null, false, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(string.Empty, res.Value);   // không gọi TTS, không cần session
    }

    [Fact]
    public async Task Missing_session_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork())
            .GetSpeechAudioAsync(Guid.NewGuid(), "Xin chào", Guid.NewGuid(), null, false, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy phiên phỏng vấn", res.Error);
    }

    [Fact]
    public async Task Non_owner_without_kiosk_is_forbidden()
    {
        var owner = Guid.NewGuid();
        var app = SchedulingData.Application(Guid.NewGuid(), owner, email: "a@example.io");
        var session = Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session);

        var res = await Svc(uow).GetSpeechAudioAsync(session.Id, "Xin chào", Guid.NewGuid(), "b@example.io", false, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Bạn không có quyền truy cập", res.Error);
    }

    [Fact]
    public async Task Tts_failure_falls_back_to_empty_success_for_owner()
    {
        var owner = Guid.NewGuid();
        var app = SchedulingData.Application(Guid.NewGuid(), owner);
        var session = Session(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session);

        // TTS all-throwing → catch → Success('') để FE fallback.
        var res = await Svc(uow).GetSpeechAudioAsync(session.Id, "Xin chào", owner, null, false, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(string.Empty, res.Value);
    }
}
