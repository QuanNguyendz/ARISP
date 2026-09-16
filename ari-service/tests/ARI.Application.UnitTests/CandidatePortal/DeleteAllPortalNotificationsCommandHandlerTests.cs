using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Xoá TẤT CẢ thông báo portal của ứng viên (<see cref="DeleteAllPortalNotificationsCommandHandler"/>): chỉ chạm
/// thông báo của chính ứng viên, trả số đã xoá, không có gì để xoá thì không save.
/// </summary>
public class DeleteAllPortalNotificationsCommandHandlerTests
{
    private static Task<ARI.Application.Common.Result<int>> Run(InMemoryUnitOfWork uow)
        => new DeleteAllPortalNotificationsCommandHandler(uow)
            .Handle(new DeleteAllPortalNotificationsCommand(PortalNotifData.CandidateA), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Nothing_to_delete_no_save()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Run(uow);

        Assert.Equal(0, res.Value);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID02_Deletes_only_own_notifications()
    {
        var mine1 = PortalNotifData.Notif(PortalNotifData.CandidateA, "a");
        var mine2 = PortalNotifData.Notif(PortalNotifData.CandidateA, "b");
        var other = PortalNotifData.Notif(Guid.NewGuid(), "c");
        var uow = new InMemoryUnitOfWork().Seed(mine1, mine2, other);

        var res = await Run(uow);

        Assert.Equal(2, res.Value);
        Assert.Single(uow.Repo<Notification>().Items);            // của người khác còn nguyên
        Assert.Contains(uow.Repo<Notification>().Items, n => n.Id == other.Id);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID03_Find_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<Notification>("Notification DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Notification DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID04_Save_error_propagates()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(PortalNotifData.Notif(PortalNotifData.CandidateA, "a"))
            .FailSaveOn(1, "Save Error");

        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Save Error", ex.Message);
    }
}
