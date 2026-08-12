using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Admin.Commands.UpdateSystemSettings;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Cập nhật cấu hình hệ thống (<see cref="UpdateSystemSettingsCommandHandler"/>, test-plan B24): danh sách
/// rỗng → lỗi; upsert — cập nhật key có sẵn + chèn key mới + audit 'system_settings_updated'.
/// </summary>
public class UpdateSystemSettingsCommandHandlerTests
{
    private static UpdateSystemSettingsCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    [Fact]
    public async Task Empty_items_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new UpdateSystemSettingsCommand(new List<UpdateSettingItem>(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Danh sách cài đặt trống", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Null_items_is_rejected()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new UpdateSystemSettingsCommand(null, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Danh sách cài đặt trống", res.Error);
    }

    [Fact]
    public async Task Existing_key_is_updated_new_key_is_inserted_with_audit()
    {
        var existing = new SystemSetting { Key = "allowed_domains", Value = "old.com" };
        var uow = new InMemoryUnitOfWork().Seed(existing);
        var items = new List<UpdateSettingItem>
        {
            new() { Key = "  allowed_domains  ", Value = "new.com" }, // trim khớp key cũ → update
            new() { Key = "webhook_url", Value = "https://hook.example.io", Description = "ATS" }, // key mới → insert
        };

        var res = await Handler(uow).Handle(new UpdateSystemSettingsCommand(items, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("new.com", existing.Value);                          // cập nhật tại chỗ
        var settings = uow.Repo<SystemSetting>().Items;
        Assert.Equal(2, settings.Count);
        var inserted = settings.Single(s => s.Key == "webhook_url");
        Assert.Equal("https://hook.example.io", inserted.Value);
        Assert.Equal("ATS", inserted.Description);
        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("system_settings_updated", audit.Action);
        Assert.Equal(1, uow.SaveChangesCount);
    }
}
