using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Queries.GetSystemSettings;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>Cấu hình hệ thống (<see cref="GetSystemSettingsQueryHandler"/>): trả toàn bộ setting sắp theo Key.</summary>
public class GetSystemSettingsQueryHandlerTests
{
    [Fact]
    public async Task Returns_settings_ordered_by_key()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            new SystemSetting { Key = "z_key", Value = "3", Description = "Z" },
            new SystemSetting { Key = "a_key", Value = "1", Description = "A" },
            new SystemSetting { Key = "m_key", Value = "2", Description = "M" });

        var res = await new GetSystemSettingsQueryHandler(uow)
            .Handle(new GetSystemSettingsQuery(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(3, res.Value.Count);
        Assert.Equal("a_key", res.Value[0].Key);   // sắp tăng theo Key
        Assert.Equal("m_key", res.Value[1].Key);
        Assert.Equal("z_key", res.Value[2].Key);
        Assert.Equal("1", res.Value[0].Value);
    }

    [Fact]
    public async Task Empty_returns_empty_list()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new GetSystemSettingsQueryHandler(uow)
            .Handle(new GetSystemSettingsQuery(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }
}
