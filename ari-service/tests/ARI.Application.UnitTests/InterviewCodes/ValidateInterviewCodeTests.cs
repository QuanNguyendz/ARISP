using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.InterviewCodes;

/// <summary>
/// Xác thực Interview Code tại Kiosk (UC-44/65, <c>InterviewCodeService.ValidateCodeAsync</c>, ADR-052):
/// mã sai/đã dùng/hết hạn → Valid=false kèm reason; mã hợp lệ → tạo phiên thật, đánh dấu đã dùng,
/// mint token Kiosk đúng phiên, ghi audit; StartSession lỗi → hoàn tác trạng thái đã-dùng.
/// </summary>
public class ValidateInterviewCodeTests
{
    private static (InMemoryUnitOfWork uow, RecordingNotificationService notif, FakeTokenService token, ARI.Application.Services.InterviewCodeService svc) Build()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();
        var token = new FakeTokenService();
        return (uow, notif, token, InterviewCodeData.Service(uow, notif, token));
    }

    [Fact]
    public async Task Empty_code_fails()
    {
        var (_, _, _, svc) = Build();

        var res = await svc.ValidateCodeAsync("   ", CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không được để trống", res.Error);
    }

    [Fact]
    public async Task Unknown_code_returns_not_found_reason()
    {
        var (_, _, _, svc) = Build();

        var res = await svc.ValidateCodeAsync("ZZZ999", CancellationToken.None);

        Assert.True(res.IsSuccess);       // mã sai vẫn trả 200 kèm reason để Kiosk hướng dẫn
        Assert.False(res.Value.Valid);
        Assert.Equal("not_found", res.Value.Reason);
    }

    [Fact]
    public async Task Used_code_returns_used_reason()
    {
        var (uow, _, _, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        uow.Seed(job).Seed(app).Seed(InterviewCodeData.Code(app.Id, "ABC234", usedAt: DateTimeOffset.UtcNow.AddMinutes(-5)));

        var res = await svc.ValidateCodeAsync("ABC234", CancellationToken.None);

        Assert.False(res.Value.Valid);
        Assert.Equal("used", res.Value.Reason);
    }

    [Fact]
    public async Task Expired_code_returns_expired_reason()
    {
        var (uow, _, _, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        uow.Seed(job).Seed(app).Seed(InterviewCodeData.Code(app.Id, "ABC234", expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));

        var res = await svc.ValidateCodeAsync("ABC234", CancellationToken.None);

        Assert.False(res.Value.Valid);
        Assert.Equal("expired", res.Value.Reason);
    }

    [Fact]
    public async Task Valid_code_starts_session_marks_used_mints_token_and_audits()
    {
        var (uow, _, token, svc) = Build();
        var job = InterviewCodeData.Job(title: "Backend Developer", language: "en");
        var app = InterviewCodeData.App(job.Id, name: "Nguyen Van A");
        var code = InterviewCodeData.Code(app.Id, "ABC234", round: 1);
        uow.Seed(job).Seed(app).Seed(code);

        var res = await svc.ValidateCodeAsync("ABC234", CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.Valid);
        Assert.NotNull(res.Value.SessionId);
        Assert.Equal("kiosk-jwt-token", res.Value.Token);
        Assert.Equal("Nguyen Van A", res.Value.CandidateName);
        Assert.Equal("Backend Developer", res.Value.JobTitle);
        Assert.Equal(1, res.Value.RoundNumber);
        Assert.Equal("en", res.Value.Language);

        Assert.NotNull(code.UsedAt);                                   // mã one-time-use bị đánh dấu đã dùng
        var session = Assert.Single(uow.Repo<InterviewSession>().Items);
        Assert.Equal("real", session.SessionType);                    // phiên thật được tạo
        Assert.Equal(res.Value.SessionId, session.Id);
        Assert.Equal(session.Id, token.LastSessionId);                // token mint đúng phiên
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "interview_code_used");
    }

    [Fact]
    public async Task Code_match_is_case_insensitive()
    {
        var (uow, _, _, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        uow.Seed(job).Seed(app).Seed(InterviewCodeData.Code(app.Id, "ABC234"));

        var res = await svc.ValidateCodeAsync("abc234", CancellationToken.None); // nhập thường

        Assert.True(res.Value.Valid);
    }

    [Fact]
    public async Task Start_session_failure_rolls_back_used_flag()
    {
        var (uow, _, token, svc) = Build();
        // Job KHÔNG seed → StartSessionAsync trả "Job posting not found." → phải hoàn tác UsedAt.
        var app = InterviewCodeData.App(Guid.NewGuid());
        var code = InterviewCodeData.Code(app.Id, "ABC234");
        uow.Seed(app).Seed(code);

        var res = await svc.ValidateCodeAsync("ABC234", CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Null(code.UsedAt);              // đánh dấu đã dùng được hoàn tác
        Assert.Equal(0, token.MintCount);      // không mint token khi phiên không tạo được
        Assert.Empty(uow.Repo<InterviewSession>().Items);
    }
}
