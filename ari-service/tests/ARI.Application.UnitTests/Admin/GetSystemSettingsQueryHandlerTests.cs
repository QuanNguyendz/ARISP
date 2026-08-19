using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Queries.GetSystemSettings;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Danh sách cấu hình hệ thống (<see cref="GetSystemSettingsQueryHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "GetSystemSettings" (UTCID01–05): rỗng, map DTO, sắp theo Key tăng dần, Description nullable, và lỗi repository.
/// </summary>
public class GetSystemSettingsQueryHandlerTests
{
    private static SystemSetting Setting(string key, string value = "v", string? description = null)
        => new() { Key = key, Value = value, Description = description };

    // UTCID01 — không có setting → []
    [Fact]
    public async Task UTCID01_Empty_when_none()
    {
        var res = await new GetSystemSettingsQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetSystemSettingsQuery(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    // UTCID02 — 1 setting → map DTO đúng giá trị
    [Fact]
    public async Task UTCID02_Maps_single_setting()
    {
        var uow = new InMemoryUnitOfWork().Seed(Setting("AllowedDomains", "example.com", "OAuth domains"));

        var item = Assert.Single((await new GetSystemSettingsQueryHandler(uow)
            .Handle(new GetSystemSettingsQuery(), CancellationToken.None)).Value);

        Assert.Equal("AllowedDomains", item.Key);
        Assert.Equal("example.com", item.Value);
        Assert.Equal("OAuth domains", item.Description);
    }

    // UTCID03 — nhiều setting không thứ tự → sắp theo Key tăng dần
    [Fact]
    public async Task UTCID03_Ordered_by_key_ascending()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            Setting("WebhookUrl"), Setting("AllowedDomains"), Setting("CompanyName"));

        var res = await new GetSystemSettingsQueryHandler(uow)
            .Handle(new GetSystemSettingsQuery(), CancellationToken.None);

        Assert.Equal(new[] { "AllowedDomains", "CompanyName", "WebhookUrl" }, res.Value.Select(s => s.Key));
    }

    // UTCID04 — Description=null → map null đúng
    [Fact]
    public async Task UTCID04_Null_description_mapped()
    {
        var uow = new InMemoryUnitOfWork().Seed(Setting("WebhookUrl", "https://example.com", description: null));

        var item = Assert.Single((await new GetSystemSettingsQueryHandler(uow)
            .Handle(new GetSystemSettingsQuery(), CancellationToken.None)).Value);

        Assert.Equal("WebhookUrl", item.Key);
        Assert.Equal("https://example.com", item.Value);
        Assert.Null(item.Description);
    }

    // UTCID05 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID05_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<SystemSetting>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => new GetSystemSettingsQueryHandler(uow).Handle(new GetSystemSettingsQuery(), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
