using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.JobBoard;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>Cài đặt portal (<see cref="GetPortalSettingsQueryHandler"/> / <see cref="UpdatePortalSettingsCommandHandler"/>): IDOR + chuẩn hóa ngôn ngữ.</summary>
public class PortalSettingsQueryTests
{
    private static CandidateAccount Account(Guid id, string? settingsJson = null)
        => new() { Id = id, Email = "cand@example.io", FullName = "Nguyen Van A", SettingsJson = settingsJson };

    [Fact]
    public async Task Get_unknown_candidate_is_unauthorized()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new GetPortalSettingsQueryHandler(uow)
            .Handle(new GetPortalSettingsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("Không tìm thấy tài khoản", res.Error);
    }

    [Fact]
    public async Task Get_returns_saved_settings()
    {
        var id = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(Account(id, "{\"language\":\"en\"}"));

        var res = await new GetPortalSettingsQueryHandler(uow)
            .Handle(new GetPortalSettingsQuery(id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("en", res.Value.Language);
    }

    [Fact]
    public async Task Get_defaults_when_no_settings_stored()
    {
        var id = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(Account(id, settingsJson: null));

        var res = await new GetPortalSettingsQueryHandler(uow)
            .Handle(new GetPortalSettingsQuery(id), CancellationToken.None);

        Assert.Equal("vi", res.Value.Language);   // default DTO
    }

    [Fact]
    public async Task Update_unknown_candidate_is_unauthorized()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new UpdatePortalSettingsCommandHandler(uow)
            .Handle(new UpdatePortalSettingsCommand(Guid.NewGuid(), new CandidateSettingsDto()), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Update_normalizes_unknown_language_to_vi_and_persists()
    {
        var id = Guid.NewGuid();
        var acc = Account(id);
        var uow = new InMemoryUnitOfWork().Seed(acc);

        var res = await new UpdatePortalSettingsCommandHandler(uow)
            .Handle(new UpdatePortalSettingsCommand(id, new CandidateSettingsDto { Language = "fr" }), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("vi", res.Value.Language);            // ngôn ngữ lạ → vi
        Assert.False(string.IsNullOrEmpty(acc.SettingsJson));
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Update_keeps_english_language()
    {
        var id = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(Account(id));

        var res = await new UpdatePortalSettingsCommandHandler(uow)
            .Handle(new UpdatePortalSettingsCommand(id, new CandidateSettingsDto { Language = "en" }), CancellationToken.None);

        Assert.Equal("en", res.Value.Language);
    }
}

/// <summary>Xuất dữ liệu cá nhân (<see cref="ExportMyDataQueryHandler"/>): file JSON gồm hồ sơ + đơn ứng tuyển.</summary>
public class ExportMyDataQueryHandlerTests
{
    [Fact]
    public async Task Unknown_candidate_is_unauthorized()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new ExportMyDataQueryHandler(uow)
            .Handle(new ExportMyDataQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(res.IsSuccess);
    }

    [Fact]
    public async Task Exports_profile_and_applications_as_json()
    {
        var id = Guid.NewGuid();
        var acc = new CandidateAccount { Id = id, Email = "me@example.io", FullName = "Nguyen Van A" };
        var job = JobBoardData.PublicJob(title: "Backend Developer");
        var app = new ARI.Domain.Entities.Application
        {
            CandidateAccountId = id,
            JobPostingId = job.Id,
            CandidateName = "Nguyen Van A",
            CandidateEmail = "me@example.io",
            Status = "cv_submitted",
        };
        var uow = new InMemoryUnitOfWork().Seed(acc).Seed(job).Seed(app);

        var res = await new ExportMyDataQueryHandler(uow)
            .Handle(new ExportMyDataQuery(id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("application/json", res.Value.ContentType);
        Assert.StartsWith("arisp-data-", res.Value.FileName);
        Assert.EndsWith(".json", res.Value.FileName);
        var json = Encoding.UTF8.GetString(res.Value.Bytes);
        Assert.Contains("me@example.io", json);
        Assert.Contains("Backend Developer", json);   // job title được resolve vào đơn
    }
}

/// <summary>Đăng xuất mọi thiết bị (<see cref="LogoutAllDevicesCommandHandler"/>): thu hồi mọi refresh token còn hiệu lực của đúng ứng viên.</summary>
public class LogoutAllDevicesCommandHandlerTests
{
    private static CandidateRefreshToken Token(Guid cand, DateTimeOffset? revokedAt = null)
        => new() { CandidateAccountId = cand, TokenHash = "h", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7), RevokedAt = revokedAt };

    [Fact]
    public async Task Revokes_all_active_tokens_of_candidate_and_returns_count()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var active1 = Token(me);
        var active2 = Token(me);
        var alreadyRevoked = Token(me, revokedAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        var otherActive = Token(other);
        var uow = new InMemoryUnitOfWork().Seed(active1, active2, alreadyRevoked, otherActive);

        var res = await new LogoutAllDevicesCommandHandler(uow)
            .Handle(new LogoutAllDevicesCommand(me), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value);
        Assert.NotNull(active1.RevokedAt);
        Assert.NotNull(active2.RevokedAt);
        Assert.Null(otherActive.RevokedAt);         // không đụng người khác
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task No_active_tokens_returns_zero_without_saving()
    {
        var me = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(Token(me, revokedAt: DateTimeOffset.UtcNow));

        var res = await new LogoutAllDevicesCommandHandler(uow)
            .Handle(new LogoutAllDevicesCommand(me), CancellationToken.None);

        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }
}
