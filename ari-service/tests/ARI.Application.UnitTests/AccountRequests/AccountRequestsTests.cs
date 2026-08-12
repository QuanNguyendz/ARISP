using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.AccountRequests;
using ARI.Application.AccountRequests.Commands.CreateAccountRequests;
using ARI.Application.AccountRequests.Queries.GetMyAccountRequests;
using ARI.Application.Admin.Queries.GetAccountRequests;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.AccountRequests;

/// <summary>
/// Tạo yêu cầu tài khoản staff (<see cref="CreateAccountRequestsCommandHandler"/>, ADR-041): validate từng mục,
/// chặn trùng/đã tồn tại, gộp batch khi nhiều mục, ghi audit + báo Super Admin.
/// </summary>
public class CreateAccountRequestsCommandHandlerTests
{
    private static AccountRequestItem Item(string email = "new@x.io", string fullName = "Nguoi Moi", string role = "recruiter")
        => new() { Email = email, FullName = fullName, Role = role };

    private static (CreateAccountRequestsCommandHandler h, RecordingNotificationService notif) Make(InMemoryUnitOfWork uow)
    {
        var notif = new RecordingNotificationService();
        return (new CreateAccountRequestsCommandHandler(uow, notif), notif);
    }

    [Fact]
    public async Task Empty_list_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(new CreateAccountRequestsCommand(new List<AccountRequestItem>(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("Danh sách yêu cầu trống", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Invalid_email_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(new CreateAccountRequestsCommand(new List<AccountRequestItem> { Item(email: "not-an-email") }, Guid.NewGuid()), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("Email không hợp lệ", res.Error);
        Assert.Empty(uow.Repo<AccountRequest>().Items);
    }

    [Fact]
    public async Task Invalid_role_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(new CreateAccountRequestsCommand(new List<AccountRequestItem> { Item(role: "super_admin") }, Guid.NewGuid()), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("Vai trò phải là", res.Error);
    }

    [Fact]
    public async Task Duplicate_email_within_request_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(new CreateAccountRequestsCommand(
            new List<AccountRequestItem> { Item(email: "dup@x.io"), Item(email: "DUP@x.io") }, Guid.NewGuid()), CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Contains("bị lặp", res.Error);
    }

    [Fact]
    public async Task Email_already_taken_or_pending_conflicts()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(AuthData.Staff(email: "taken@x.io"))
            .Seed(new AccountRequest { Email = "waiting@x.io", Status = "pending", RequestedByUserId = Guid.NewGuid() });
        var (h, _) = Make(uow);

        var takenByUser = await h.Handle(new CreateAccountRequestsCommand(new List<AccountRequestItem> { Item(email: "taken@x.io") }, Guid.NewGuid()), CancellationToken.None);
        var takenByPending = await h.Handle(new CreateAccountRequestsCommand(new List<AccountRequestItem> { Item(email: "waiting@x.io") }, Guid.NewGuid()), CancellationToken.None);

        Assert.False(takenByUser.IsSuccess);
        Assert.Contains("đã có tài khoản hoặc đang chờ duyệt", takenByUser.Error);
        Assert.False(takenByPending.IsSuccess);
    }

    [Fact]
    public async Task Single_valid_request_persists_normalized_audits_and_notifies()
    {
        var actor = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork();
        var (h, notif) = Make(uow);

        var res = await h.Handle(new CreateAccountRequestsCommand(
            new List<AccountRequestItem> { new() { Email = "  HR@X.IO ", FullName = " Nguyen Van A ", Role = "HR_Admin" } }, actor), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.Count);
        Assert.Null(res.Value.BatchId);                          // 1 mục → không batch
        var saved = Assert.Single(uow.Repo<AccountRequest>().Items);
        Assert.Equal("hr@x.io", saved.Email);                    // trim + lower
        Assert.Equal("Nguyen Van A", saved.FullName);            // trim
        Assert.Equal("hr_admin", saved.Role);                    // lower
        Assert.Equal("pending", saved.Status);
        Assert.Equal(actor, saved.RequestedByUserId);
        Assert.Single(uow.Repo<AuditLog>().Items, a => a.Action == "account_request_created");
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Equal(2, notif.GroupEvents.Count(e => e.Group == "super_admin"));  // 2 sự kiện báo SA
    }

    [Fact]
    public async Task Multiple_valid_requests_share_a_batch_id()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(new CreateAccountRequestsCommand(
            new List<AccountRequestItem> { Item(email: "a@x.io"), Item(email: "b@x.io") }, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Count);
        Assert.NotNull(res.Value.BatchId);
        var reqs = uow.Repo<AccountRequest>().Items;
        Assert.Equal(2, reqs.Count);
        Assert.All(reqs, r => Assert.Equal(res.Value.BatchId, r.BatchId));  // cùng batch
    }
}

/// <summary>Yêu cầu do chính HR Leader gửi (<see cref="GetMyAccountRequestsQueryHandler"/>): chỉ của mình, mới nhất trước.</summary>
public class GetMyAccountRequestsQueryHandlerTests
{
    private static AccountRequest Req(Guid requester, DateTimeOffset createdAt, string email = "x@x.io")
        => new() { RequestedByUserId = requester, Email = email, FullName = "N", Role = "recruiter", Status = "pending", CreatedAt = createdAt };

    [Fact]
    public async Task Returns_only_own_requests_newest_first()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var older = Req(me, now.AddMinutes(-2), "old@x.io");
        var newer = Req(me, now.AddMinutes(-1), "new@x.io");
        var uow = new InMemoryUnitOfWork().Seed(older, newer, Req(other, now, "other@x.io"));

        var res = await new GetMyAccountRequestsQueryHandler(uow)
            .Handle(new GetMyAccountRequestsQuery(me), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Count);
        Assert.Equal("new@x.io", res.Value[0].Email);   // mới nhất trước
        Assert.Equal("old@x.io", res.Value[1].Email);
    }

    [Fact]
    public async Task Empty_when_none()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new GetMyAccountRequestsQueryHandler(uow)
            .Handle(new GetMyAccountRequestsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(res.Value);
    }
}

/// <summary>Super Admin xem yêu cầu (<see cref="GetAccountRequestsQueryHandler"/>): mặc định pending, "all" xem hết, resolve tên người gửi.</summary>
public class GetAccountRequestsQueryHandlerTests
{
    private static AccountRequest Req(Guid requester, string status, string email)
        => new() { RequestedByUserId = requester, Email = email, FullName = "N", Role = "recruiter", Status = status, CreatedAt = DateTimeOffset.UtcNow };

    [Fact]
    public async Task Defaults_to_pending_and_resolves_requester_name()
    {
        var requester = AuthData.Staff(email: "leader@x.io", fullName: "HR Leader");
        var uow = new InMemoryUnitOfWork()
            .Seed(requester)
            .Seed(Req(requester.Id, "pending", "p@x.io"), Req(requester.Id, "approved", "a@x.io"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery(null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var item = Assert.Single(res.Value);              // chỉ pending
        Assert.Equal("p@x.io", item.Email);
        Assert.Equal("HR Leader", item.RequestedBy);
    }

    [Fact]
    public async Task All_returns_every_status()
    {
        var requester = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(Req(requester, "pending", "p@x.io"), Req(requester, "approved", "a@x.io"), Req(requester, "rejected", "r@x.io"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery("all"), CancellationToken.None);

        Assert.Equal(3, res.Value.Count);
        Assert.All(res.Value, i => Assert.Equal("—", i.RequestedBy));  // không seed User → "—"
    }

    [Fact]
    public async Task Filters_by_explicit_status()
    {
        var requester = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(Req(requester, "pending", "p@x.io"), Req(requester, "approved", "a@x.io"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery("approved"), CancellationToken.None);

        Assert.Equal("a@x.io", Assert.Single(res.Value).Email);
    }
}
