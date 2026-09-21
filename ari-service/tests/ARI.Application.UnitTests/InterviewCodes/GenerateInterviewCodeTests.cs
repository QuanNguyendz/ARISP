using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.InterviewCodes;

/// <summary>
/// Cấp Interview Code cho buổi phỏng vấn thật (UC-44, <c>InterviewCodeService.GenerateCode/GenerateBatch</c>):
/// chỉ cấp khi ứng viên đã đặt lịch buổi thật của vòng (ADR-015/016), mã 6 ký tự alphanumeric,
/// ghi audit + đẩy thông báo, nâng screening→interview; batch bỏ qua hồ sơ chưa đủ điều kiện.
/// </summary>
public class GenerateInterviewCodeTests
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // bỏ I O 0 1

    private static (InterviewCodeData_Ctx ctx, ARI.Application.Services.InterviewCodeService svc) Build()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();
        var token = new FakeTokenService();
        return (new InterviewCodeData_Ctx(uow, notif, token), InterviewCodeData.Service(uow, notif, token));
    }

    [Fact]
    public async Task Generate_application_not_found_fails()
    {
        var (_, svc) = Build();

        var res = await svc.GenerateCodeAsync(Guid.NewGuid(), 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Hồ sơ ứng tuyển không tồn tại", res.Error);
    }

    [Fact]
    public async Task Generate_job_not_found_fails()
    {
        var (ctx, svc) = Build();
        var app = InterviewCodeData.App(Guid.NewGuid()); // job không seed
        ctx.Uow.Seed(app);

        var res = await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Tin tuyển dụng liên kết không tồn tại", res.Error);
    }

    [Fact]
    public async Task Generate_without_scheduled_booking_is_rejected()
    {
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app); // KHÔNG có booking scheduled

        var res = await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("chưa đặt lịch", res.Error);
        Assert.Empty(ctx.Uow.Repo<ARI.Domain.Entities.InterviewCode>().Items);
    }

    [Fact]
    public async Task Generate_with_booking_creates_6char_code_and_audit()
    {
        var (ctx, svc) = Build();
        var hrId = Guid.NewGuid();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app).Seed(InterviewCodeData.Booking(app.Id, round: 1));

        var res = await svc.GenerateCodeAsync(app.Id, 1, hrId, CancellationToken.None);

        Assert.True(res.IsSuccess);
        var code = res.Value.Code;
        Assert.Equal(6, code.Length);
        Assert.All(code, c => Assert.Contains(c, CodeAlphabet)); // alphanumeric, bỏ ký tự dễ nhầm
        Assert.True(res.Value.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Single(ctx.Uow.Repo<ARI.Domain.Entities.InterviewCode>().Items);
        var audit = Assert.Single(ctx.Uow.Repo<AuditLog>().Items);
        Assert.Equal("interview_code_generated", audit.Action);
        Assert.Equal(hrId, audit.ActorUserId);
    }

    [Fact]
    public async Task Generate_uses_round_config_ttl()
    {
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app)
            .Seed(InterviewCodeData.Booking(app.Id, round: 1))
            .Seed(InterviewCodeData.RoundConfig(job.Id, round: 1, ttlHours: 5));

        var res = await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.Value.ExpiresAt > DateTimeOffset.UtcNow.AddHours(4)); // TTL 5h theo round config
    }

    [Fact]
    public async Task Generate_promotes_screening_to_interview()
    {
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id, status: "screening");
        ctx.Uow.Seed(job).Seed(app).Seed(InterviewCodeData.Booking(app.Id, round: 1));

        await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("interview", app.Status); // có mã = chắc chắn vào phỏng vấn thật
    }

    [Fact]
    public async Task Generate_notifies_candidate_when_account_present()
    {
        var (ctx, svc) = Build();
        var accountId = Guid.NewGuid();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id, accountId: accountId);
        ctx.Uow.Seed(job).Seed(app).Seed(InterviewCodeData.Booking(app.Id, round: 1));

        await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.Contains(ctx.Notif.UserEvents, e => e.UserId == accountId && e.EventType == "ReceiveUserNotification");
    }

    [Fact]
    public async Task Generate_without_round_uses_the_highest_scheduled_booking()
    {
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app)
            .Seed(InterviewCodeData.CompletedSession(app.Id, round: 1)) // đã xong vòng 1
            .Seed(InterviewCodeData.Booking(app.Id, round: 1))          // lịch vòng 1 vẫn "scheduled"
            .Seed(InterviewCodeData.Booking(app.Id, round: 2));         // đã xếp lịch vòng 2

        var res = await svc.GenerateCodeAsync(app.Id, roundNumber: null, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.RoundNumber);
    }

    [Fact]
    public async Task Generate_without_round_after_passing_an_online_test_round_issues_for_the_next_round()
    {
        // Lỗi thật trên deploy: vòng 1 trắc nghiệm KHÔNG sinh phiên phỏng vấn nào, nên phép đoán cũ
        // max(phiên đã xong) + 1 ra vòng 1 → "Vòng 1 là bài trắc nghiệm", dù Recruiter đang cấp cho vòng 2.
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test" })
            .Seed(InterviewCodeData.RoundConfig(job.Id, round: 2))
            .Seed(InterviewCodeData.Booking(app.Id, round: 1))
            .Seed(InterviewCodeData.Booking(app.Id, round: 2));

        var res = await svc.GenerateCodeAsync(app.Id, roundNumber: null, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsSuccess, res.IsFailure ? res.Error : null);
        Assert.Equal(2, res.Value.RoundNumber);
    }

    [Fact]
    public async Task Generate_without_round_and_without_any_booking_is_rejected()
    {
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app)
            .Seed(InterviewCodeData.Booking(app.Id, round: 1, status: "declined")); // đã trả chỗ

        var res = await svc.GenerateCodeAsync(app.Id, roundNumber: null, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("chưa có lịch", res.Error);
        Assert.Empty(ctx.Uow.Repo<ARI.Domain.Entities.InterviewCode>().Items);
    }

    // ---------- Luật cấp mã (dùng chung mọi đường cấp) ----------

    [Fact]
    public async Task Generate_for_online_test_round_is_rejected()
    {
        // Bài trắc nghiệm làm trong Portal (ADR-049). Có mã thì nhập tại Kiosk sẽ mở một phiên PHỎNG
        // VẤN AI cho một vòng vốn là bài thi.
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app)
            .Seed(InterviewCodeData.Booking(app.Id, round: 1))
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = "online_test" });

        var res = await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("trắc nghiệm", res.Error);
        Assert.Empty(ctx.Uow.Repo<ARI.Domain.Entities.InterviewCode>().Items);
    }

    [Theory]
    [InlineData("waiting", "đang ở phòng")]
    [InlineData("active", "đang ở phòng")]
    [InlineData("completed", "phỏng vấn xong")]
    public async Task Generate_after_candidate_entered_the_room_is_rejected(string sessionStatus, string reason)
    {
        // Nhập mã luôn mở phiên MỚI — mã thứ hai là phòng chờ thứ hai cho cùng một người.
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app)
            .Seed(InterviewCodeData.Booking(app.Id, round: 1))
            .Seed(new InterviewSession { ApplicationId = app.Id, RoundNumber = 1, SessionType = "real", Status = sessionStatus });

        var res = await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains(reason, res.Error);
        Assert.Empty(ctx.Uow.Repo<ARI.Domain.Entities.InterviewCode>().Items);
    }

    [Fact]
    public async Task Generate_after_an_aborted_session_is_allowed()
    {
        // Phiên hỏng giữa chừng không được khoá ứng viên ngoài phòng — cấp mã mới để vào lại.
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app)
            .Seed(InterviewCodeData.Booking(app.Id, round: 1))
            .Seed(new InterviewSession { ApplicationId = app.Id, RoundNumber = 1, SessionType = "real", Status = "aborted" });

        var res = await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Generating_again_expires_the_previous_unused_code()
    {
        // MỘT mã sống cho mỗi (hồ sơ, vòng): để hai mã cùng hiệu lực thì ứng viên nhập được cả hai và
        // sinh hai phiên phỏng vấn.
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var app = InterviewCodeData.App(job.Id);
        ctx.Uow.Seed(job).Seed(app).Seed(InterviewCodeData.Booking(app.Id, round: 1));

        var first = (await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None)).Value;
        var second = (await svc.GenerateCodeAsync(app.Id, 1, Guid.NewGuid(), CancellationToken.None)).Value;

        Assert.True(first.ExpiresAt <= DateTimeOffset.UtcNow);
        Assert.True(second.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.NotEqual(first.Code, second.Code);

        // Mã cũ nhập vào Kiosk bị từ chối là "hết hạn" — không mở được phiên thứ hai.
        var validate = await svc.ValidateCodeAsync(first.Code, CancellationToken.None);
        Assert.False(validate.Value.Valid);
        Assert.Equal("expired", validate.Value.Reason);
    }

    // ---------- GenerateBatchAsync ----------

    [Fact]
    public async Task Batch_empty_list_fails()
    {
        var (_, svc) = Build();

        var res = await svc.GenerateBatchAsync(new(), 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không được để trống", res.Error);
    }

    [Fact]
    public async Task Batch_returns_only_eligible_codes()
    {
        var (ctx, svc) = Build();
        var job = InterviewCodeData.Job();
        var eligible = InterviewCodeData.App(job.Id, name: "Có lịch");
        var notEligible = InterviewCodeData.App(job.Id, name: "Chưa lịch");
        ctx.Uow.Seed(job).Seed(eligible, notEligible)
            .Seed(InterviewCodeData.Booking(eligible.Id, round: 1)); // chỉ 1 người có booking

        var res = await svc.GenerateBatchAsync(
            new() { eligible.Id, notEligible.Id }, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var code = Assert.Single(res.Value);
        Assert.Equal(eligible.Id, code.ApplicationId); // hồ sơ chưa đủ điều kiện bị bỏ qua
    }

    [Fact]
    public async Task Batch_null_list_fails()
    {
        var (_, svc) = Build();

        var res = await svc.GenerateBatchAsync(null!, 1, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không được để trống", res.Error);
    }

    [Fact]
    public async Task Batch_two_eligible_round2_apps_get_distinct_codes_and_two_audits()
    {
        var (ctx, svc) = Build();
        var hrId = Guid.NewGuid();
        var job = InterviewCodeData.Job();
        var app1 = InterviewCodeData.App(job.Id, name: "Ứng viên 1");
        var app2 = InterviewCodeData.App(job.Id, name: "Ứng viên 2");
        ctx.Uow.Seed(job).Seed(app1, app2)
            .Seed(InterviewCodeData.Booking(app1.Id, round: 2))   // cả 2 đã đặt lịch vòng 2
            .Seed(InterviewCodeData.Booking(app2.Id, round: 2));

        var res = await svc.GenerateBatchAsync(new() { app1.Id, app2.Id }, roundNumber: 2, hrId, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Count);
        Assert.All(res.Value, c => Assert.Equal(2, c.RoundNumber));            // đúng vòng 2
        Assert.NotEqual(res.Value[0].Code, res.Value[1].Code);                 // mã khác nhau
        Assert.Equal(2, ctx.Uow.Repo<AuditLog>().Items.Count(a => a.Action == "interview_code_generated"));
    }
}

/// <summary>Gói 3 fake dùng chung cho các test cấp/validate mã (giữ chữ ký test gọn).</summary>
internal sealed record InterviewCodeData_Ctx(InMemoryUnitOfWork Uow, RecordingNotificationService Notif, FakeTokenService Token);
