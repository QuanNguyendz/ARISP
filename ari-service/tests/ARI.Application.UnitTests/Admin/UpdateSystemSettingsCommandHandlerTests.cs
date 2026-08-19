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
/// Cập nhật cấu hình hệ thống (<see cref="UpdateSystemSettingsCommandHandler"/>).
/// </summary>
/// <remarks>
/// Tab "UpdateSystemSettings" trong Report5 Unit v1.2 bị ghi trùng nội dung ma trận của "RejectAccountRequest"
/// (lỗi copy trong file report). Bộ test dưới đây bám theo mô tả ở tab "Functions" của chính report
/// ("empty input, blank keys, existing/new settings, multiple items, nullable actor, success, repository errors")
/// và hành vi thật của handler.
/// </remarks>
public class UpdateSystemSettingsCommandHandlerTests
{
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static UpdateSystemSettingsCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    private static UpdateSettingItem Item(string key, string? value = "v", string? description = null)
        => new() { Key = key, Value = value, Description = description };

    private static UpdateSystemSettingsCommand Cmd(Guid? actor, params UpdateSettingItem[] items)
        => new(items.ToList(), actor);

    // UTCID01 — Items=null → "Danh sách cài đặt trống."
    [Fact]
    public async Task UTCID01_Null_items_is_empty()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new UpdateSystemSettingsCommand(null, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Danh sách cài đặt trống.", res.Error);
    }

    // UTCID02 — Items=[] → "Danh sách cài đặt trống."
    [Fact]
    public async Task UTCID02_Empty_items_is_empty()
    {
        var res = await Handler(new InMemoryUnitOfWork())
            .Handle(new UpdateSystemSettingsCommand(new List<UpdateSettingItem>(), ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Danh sách cài đặt trống.", res.Error);
    }

    // UTCID03 — chỉ có item key trống → bỏ qua, không tạo setting, vẫn Success + audit
    [Fact]
    public async Task UTCID03_Blank_key_is_skipped()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(Cmd(ActorA, Item("   ")), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<SystemSetting>().Items);
        Assert.Equal("system_settings_updated", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID04 — key chưa tồn tại → tạo setting mới
    [Fact]
    public async Task UTCID04_New_setting_is_created()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(Cmd(ActorA, Item("CompanyName", "ARISP", "Company")), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var s = Assert.Single(uow.Repo<SystemSetting>().Items);
        Assert.Equal("CompanyName", s.Key);
        Assert.Equal("ARISP", s.Value);
        Assert.Equal("Company", s.Description);
    }

    // UTCID05 — key đã tồn tại → cập nhật Value + Description
    [Fact]
    public async Task UTCID05_Existing_setting_is_updated()
    {
        var existing = new SystemSetting { Key = "AllowedDomains", Value = "old.com", Description = "old" };
        var uow = new InMemoryUnitOfWork().Seed(existing);

        var res = await Handler(uow).Handle(Cmd(ActorA, Item("AllowedDomains", "new.com", "new")), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("new.com", existing.Value);
        Assert.Equal("new", existing.Description);
        Assert.Single(uow.Repo<SystemSetting>().Items);   // không tạo thêm dòng
    }

    // UTCID06 — nhiều item (1 mới + 1 đã có) → xử lý cả hai
    [Fact]
    public async Task UTCID06_Multiple_items_new_and_existing()
    {
        var existing = new SystemSetting { Key = "AllowedDomains", Value = "old.com" };
        var uow = new InMemoryUnitOfWork().Seed(existing);

        var res = await Handler(uow).Handle(Cmd(ActorA,
            Item("AllowedDomains", "new.com"), Item("WebhookUrl", "https://hook")), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("new.com", existing.Value);
        Assert.Equal(2, uow.Repo<SystemSetting>().Items.Count);
        Assert.Contains(uow.Repo<SystemSetting>().Items, s => s.Key == "WebhookUrl" && s.Value == "https://hook");
    }

    // UTCID07 — ActorId=null → vẫn thành công
    [Fact]
    public async Task UTCID07_Null_actor_still_succeeds()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(Cmd(null, Item("CompanyName", "ARISP")), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<SystemSetting>().Items);
    }

    // UTCID08 — Value=null → lưu thành chuỗi rỗng
    [Fact]
    public async Task UTCID08_Null_value_stored_as_empty_string()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(Cmd(ActorA, Item("CompanyName", value: null)), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(string.Empty, Assert.Single(uow.Repo<SystemSetting>().Items).Value);
    }

    // UTCID09 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID09_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<SystemSetting>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(Cmd(ActorA, Item("CompanyName", "ARISP")), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
