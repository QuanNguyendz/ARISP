using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

internal static class PortalSettingsData
{
    public static readonly Guid CandidateA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static CandidateAccount Account(string? settingsJson = null)
        => new() { Id = CandidateA, Email = "candidate@example.com", FullName = "Candidate User", SettingsJson = settingsJson };
}

/// <summary>
/// Đọc cài đặt portal (<see cref="GetPortalSettingsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetPortalSettings" (UTCID01–08): không có tài khoản → unauthorized; JSON hợp lệ → map; null/rỗng/khoảng trắng/
/// JSON hỏng/"null" → default; và lỗi repo.
/// </summary>
public class GetPortalSettingsQueryHandlerTests
{
    private static Task<Result<CandidateSettingsDto>> Run(InMemoryUnitOfWork uow)
        => new GetPortalSettingsQueryHandler(uow).Handle(new GetPortalSettingsQuery(PortalSettingsData.CandidateA), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Run(new InMemoryUnitOfWork());
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tài khoản ứng viên.", res.Error);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Customized_settings()
    {
        var json = "{\"language\":\"en\",\"allowHrViewProfile\":false,\"allowRecording\":false,\"marketingEmail\":true}";
        var res = await Run(new InMemoryUnitOfWork().Seed(PortalSettingsData.Account(json)));
        Assert.True(res.IsSuccess);
        Assert.Equal("en", res.Value.Language);
        Assert.False(res.Value.AllowHrViewProfile);
        Assert.False(res.Value.AllowRecording);
        Assert.True(res.Value.MarketingEmail);
    }

    [Theory]
    [InlineData(null)]           // UTCID03
    [InlineData("")]             // UTCID04
    [InlineData("   ")]          // UTCID05
    [InlineData("{not json}")]   // UTCID06
    [InlineData("null")]         // UTCID07
    public async Task UTCID03_to_07_default_settings(string? settingsJson)
    {
        var res = await Run(new InMemoryUnitOfWork().Seed(PortalSettingsData.Account(settingsJson)));
        Assert.True(res.IsSuccess);
        Assert.Equal("vi", res.Value.Language);              // default DTO
        Assert.True(res.Value.AllowHrViewProfile);
    }

    [Fact]
    public async Task UTCID08_Repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Candidate DB Error", ex.Message);
    }
}

/// <summary>
/// Cập nhật cài đặt portal (<see cref="UpdatePortalSettingsCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "UpdatePortalSettings" (UTCID01–09): unauthorized; Settings null → default vi; chuẩn hoá ngôn ngữ (fr→vi);
/// lưu tuỳ chỉnh; và lỗi phụ thuộc (lookup/update/save).
/// </summary>
public class UpdatePortalSettingsCommandHandlerTests
{
    private static Task<Result<CandidateSettingsDto>> Run(InMemoryUnitOfWork uow, CandidateSettingsDto? settings)
        => new UpdatePortalSettingsCommandHandler(uow).Handle(new UpdatePortalSettingsCommand(PortalSettingsData.CandidateA, settings), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Run(new InMemoryUnitOfWork(), new CandidateSettingsDto());
        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Null_settings_defaults_vi()
    {
        var acc = PortalSettingsData.Account();
        var uow = new InMemoryUnitOfWork().Seed(acc);
        var res = await Run(uow, null);
        Assert.True(res.IsSuccess);
        Assert.Equal("vi", res.Value.Language);
        Assert.False(string.IsNullOrEmpty(acc.SettingsJson));
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID03_Language_en()
    {
        var res = await Run(new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()), new CandidateSettingsDto { Language = "en" });
        Assert.Equal("en", res.Value.Language);
    }

    [Fact]
    public async Task UTCID04_Language_vi()
    {
        var res = await Run(new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()), new CandidateSettingsDto { Language = "vi" });
        Assert.Equal("vi", res.Value.Language);
    }

    [Fact]
    public async Task UTCID05_Unsupported_language_coerced_vi()
    {
        var res = await Run(new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()), new CandidateSettingsDto { Language = "fr" });
        Assert.Equal("vi", res.Value.Language);
    }

    [Fact]
    public async Task UTCID06_Customized_preferences()
    {
        var acc = PortalSettingsData.Account();
        var settings = new CandidateSettingsDto
        {
            Language = "en",
            InterviewInvite = new NotificationChannelPref { Email = false, Push = true },
            AllowRecording = false,
            MarketingEmail = true,
        };
        var res = await Run(new InMemoryUnitOfWork().Seed(acc), settings);
        Assert.True(res.IsSuccess);
        Assert.Equal("en", res.Value.Language);
        Assert.False(res.Value.InterviewInvite.Email);
        Assert.True(res.Value.InterviewInvite.Push);
        Assert.False(res.Value.AllowRecording);
        Assert.True(res.Value.MarketingEmail);
        Assert.Contains("marketingEmail", acc.SettingsJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UTCID07_Lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, new CandidateSettingsDto()));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID08_Update_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()).FailUpdateFor<CandidateAccount>("Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, new CandidateSettingsDto()));
        Assert.Equal("Update Error", ex.Message);
    }

    [Fact]
    public async Task UTCID09_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, new CandidateSettingsDto()));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Xuất dữ liệu cá nhân (<see cref="ExportMyDataQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "ExportMyData" (UTCID01–09): unauthorized; JSON file (application/json, tên arisp-data-yyyyMMdd.json);
/// apps sắp CreatedAt desc + JobTitle map (null khi thiếu job); JSON hồ sơ hỏng → mảng rỗng; null fields OK; lỗi repo.
/// </summary>
public class ExportMyDataQueryHandlerTests
{
    private static Task<Result<ExportFileDto>> Run(InMemoryUnitOfWork uow)
        => new ExportMyDataQueryHandler(uow).Handle(new ExportMyDataQuery(PortalSettingsData.CandidateA), CancellationToken.None);

    private static ARI.Domain.Entities.Application App(Guid job, DateTimeOffset createdAt, string status = "cv_submitted")
        => new() { CandidateAccountId = PortalSettingsData.CandidateA, JobPostingId = job, CandidateName = "N", CandidateEmail = "c@x.io", Status = status, CreatedAt = createdAt };

    private static JsonElement Json(ExportFileDto file) => JsonDocument.Parse(file.Bytes).RootElement;

    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Run(new InMemoryUnitOfWork());
        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_No_applications()
    {
        var res = await Run(new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()));
        Assert.True(res.IsSuccess);
        Assert.Equal("application/json", res.Value.ContentType);
        Assert.StartsWith("arisp-data-", res.Value.FileName);
        Assert.Equal(0, Json(res.Value).GetProperty("applications").GetArrayLength());
    }

    [Fact]
    public async Task UTCID03_Applications_ordered_with_job_title()
    {
        var now = DateTimeOffset.UtcNow;
        var jobA = Guid.NewGuid(); var jobB = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(PortalSettingsData.Account())
            .Seed(new JobPosting { Id = jobA, Title = "Older Job", CreatedByUserId = Guid.NewGuid() },
                  new JobPosting { Id = jobB, Title = "Newer Job", CreatedByUserId = Guid.NewGuid() })
            .Seed(App(jobA, now.AddDays(-1)), App(jobB, now));

        var res = await Run(uow);

        var apps = Json(res.Value).GetProperty("applications");
        Assert.Equal(2, apps.GetArrayLength());
        Assert.Equal("Newer Job", apps[0].GetProperty("JobTitle").GetString());   // mới nhất trước
    }

    [Fact]
    public async Task UTCID04_Missing_job_title_null()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()).Seed(App(Guid.NewGuid(), DateTimeOffset.UtcNow));
        var res = await Run(uow);
        var app = Json(res.Value).GetProperty("applications")[0];
        Assert.Equal(JsonValueKind.Null, app.GetProperty("JobTitle").ValueKind);
    }

    [Fact]
    public async Task UTCID05_Invalid_profile_json_empty_collections()
    {
        var acc = PortalSettingsData.Account();
        acc.SkillsJson = "{bad"; acc.ExperienceJson = "{bad"; acc.EducationJson = "{bad";
        var res = await Run(new InMemoryUnitOfWork().Seed(acc));
        var profile = Json(res.Value).GetProperty("profile");
        Assert.Equal(0, profile.GetProperty("Skills").GetArrayLength());
        Assert.Equal(0, profile.GetProperty("Experience").GetArrayLength());
        Assert.Equal(0, profile.GetProperty("Education").GetArrayLength());
    }

    [Fact]
    public async Task UTCID06_Null_optional_fields_ok()
    {
        var res = await Run(new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()));   // optional fields null
        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task UTCID07_Candidate_repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID08_Application_repo_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()).FailFindFor<ARI.Domain.Entities.Application>("Application DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Application DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID09_Job_repo_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(PortalSettingsData.Account()).Seed(App(Guid.NewGuid(), DateTimeOffset.UtcNow)).FailFindFor<JobPosting>("Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Job DB Error", ex.Message);
    }
}

/// <summary>
/// Đăng xuất mọi thiết bị (<see cref="LogoutAllDevicesCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "LogoutAllDevices" (UTCID01–07): revoke mọi refresh token active của ứng viên, trả số lượng; và lỗi phụ thuộc.
/// </summary>
public class LogoutAllDevicesCommandHandlerTests
{
    private static Task<Result<int>> Run(InMemoryUnitOfWork uow)
        => new LogoutAllDevicesCommandHandler(uow).Handle(new LogoutAllDevicesCommand(PortalSettingsData.CandidateA), CancellationToken.None);

    private static CandidateRefreshToken Token(DateTimeOffset? revokedAt = null)
        => new() { CandidateAccountId = PortalSettingsData.CandidateA, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10), RevokedAt = revokedAt };

    [Fact]
    public async Task UTCID01_No_tokens()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow);
        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID02_One_token_revoked()
    {
        var token = Token();
        var uow = new InMemoryUnitOfWork().Seed(token);
        var res = await Run(uow);
        Assert.Equal(1, res.Value);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID03_Three_tokens_revoked()
    {
        var uow = new InMemoryUnitOfWork().Seed(Token(), Token(), Token());
        var res = await Run(uow);
        Assert.Equal(3, res.Value);
    }

    [Fact]
    public async Task UTCID04_Only_revoked_tokens()
    {
        var uow = new InMemoryUnitOfWork().Seed(Token(revokedAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        var res = await Run(uow);
        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID05_Find_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateRefreshToken>("Token DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Token DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID06_Update_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Token()).FailUpdateFor<CandidateRefreshToken>("Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Update Error", ex.Message);
    }

    [Fact]
    public async Task UTCID07_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Token()).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Save Error", ex.Message);
    }
}
