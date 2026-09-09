using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.PracticeInterview;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Cấp cấu hình media cho phiên (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>GetMediaConfigAsync</c>, test-plan B19): guard phiên/hồ sơ, IDOR (kiosk bypass), token media
/// best-effort (provider lỗi → null, không sập phòng), trần thời lượng theo loại phiên (real/practice đều 20').
/// </summary>
public class GetMediaConfigTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService()); // providers all-throwing → nuốt lỗi

    private static InterviewSession Session(Guid appId, string type) =>
        new() { ApplicationId = appId, RoundNumber = 1, SessionType = type, InterviewLanguage = "vi", Status = "active" };

    [Fact]
    public async Task Missing_session_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork())
            .GetMediaConfigAsync(Guid.NewGuid(), Guid.NewGuid(), null, kioskAuthorized: false, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy phiên phỏng vấn", res.Error);
    }

    [Fact]
    public async Task Missing_application_fails()
    {
        var session = Session(Guid.NewGuid(), "real"); // ApplicationId trỏ hồ sơ không seed
        var uow = new InMemoryUnitOfWork().Seed(session);

        var res = await Svc(uow).GetMediaConfigAsync(session.Id, Guid.NewGuid(), null, false, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy hồ sơ ứng tuyển", res.Error);
    }

    [Fact]
    public async Task Non_owner_without_kiosk_is_forbidden()
    {
        var owner = Guid.NewGuid();
        var app = SchedulingData.Application(Guid.NewGuid(), owner, email: "a@example.io");
        var session = Session(app.Id, "real");
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session);

        var res = await Svc(uow).GetMediaConfigAsync(
            session.Id, Guid.NewGuid(), "b@example.io", kioskAuthorized: false, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Bạn không có quyền truy cập", res.Error);
    }

    [Fact]
    public async Task Kiosk_real_session_succeeds_with_default_cap_and_null_providers()
    {
        var app = SchedulingData.Application(Guid.NewGuid(), Guid.NewGuid());
        var session = Session(app.Id, "real");
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session);

        // Kiosk bypass IDOR; provider ném lỗi → Deepgram null (nuốt lỗi, không sập phòng).
        var res = await Svc(uow).GetMediaConfigAsync(session.Id, null, null, kioskAuthorized: true, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("real", res.Value.SessionType);
        Assert.Equal(1200, res.Value.MaxDurationSeconds);   // 20' × 60 — trần buổi phỏng vấn thật
        Assert.Null(res.Value.Deepgram);
    }

    [Fact]
    public async Task Practice_session_has_20min_cap()
    {
        var owner = Guid.NewGuid();
        var app = SchedulingData.Application(Guid.NewGuid(), owner);
        var session = Session(app.Id, "practice");
        var uow = new InMemoryUnitOfWork().Seed(app).Seed(session);

        var res = await Svc(uow).GetMediaConfigAsync(session.Id, owner, null, kioskAuthorized: false, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1200, res.Value.MaxDurationSeconds);   // 20' × 60
    }
}
