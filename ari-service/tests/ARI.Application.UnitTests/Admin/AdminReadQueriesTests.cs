using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Queries.GetAdminStats;
using ARI.Application.Admin.Queries.GetAuditLogs;
using ARI.Application.Admin.Queries.GetPendingUsers;
using ARI.Application.Admin.Queries.GetUsers;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>Lịch sử thao tác (<see cref="GetAuditLogsQueryHandler"/>, test-plan B29): lọc Action + sort mới nhất trước + clamp PageSize 100 + actor null → 'Hệ thống'.</summary>
public class GetAuditLogsQueryHandlerTests
{
    private static AuditLog Log(string action, Guid? actor, DateTimeOffset at)
        => new() { Action = action, ActorUserId = actor, EntityType = "User", CreatedAt = at };

    [Fact]
    public async Task Filters_by_action_sorts_desc_and_resolves_actor_name()
    {
        var now = DateTimeOffset.UtcNow;
        var actor = AuthData.Staff(fullName: "Admin One");
        var older = Log("user_created", actor.Id, now.AddMinutes(-2));
        var newer = Log("user_created", null, now.AddMinutes(-1)); // actor null → "Hệ thống"
        var other = Log("user_deleted", null, now);
        var uow = new InMemoryUnitOfWork().Seed(actor).Seed(older, newer, other);

        var res = await new GetAuditLogsQueryHandler(uow)
            .Handle(new GetAuditLogsQuery("USER_CREATED", null, 1, 20), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.TotalCount);                 // chỉ user_created (case-insensitive)
        Assert.Equal(newer.Id, res.Value.Items[0].Id);         // mới nhất trước
        Assert.Equal("Hệ thống", res.Value.Items[0].ActorName);
        Assert.Equal("Admin One", res.Value.Items[1].ActorName);
    }

    [Fact]
    public async Task Page_size_is_clamped_to_100()
    {
        var uow = new InMemoryUnitOfWork().Seed(Log("x", null, DateTimeOffset.UtcNow));

        var res = await new GetAuditLogsQueryHandler(uow).Handle(new GetAuditLogsQuery(null, null, 1, 500), CancellationToken.None);

        Assert.Equal(100, res.Value.PageSize);
    }
}

/// <summary>Danh sách staff (<see cref="GetUsersQueryHandler"/>, test-plan B29): lọc role + active, paged mới nhất trước, item mang LockReason.</summary>
public class GetUsersQueryHandlerTests
{
    [Fact]
    public async Task Filters_by_role_and_active_state()
    {
        var activeRecruiter = AuthData.Staff(email: "r1@x.io", role: "recruiter", active: true);
        var inactiveRecruiter = AuthData.Staff(email: "r2@x.io", role: "recruiter", active: false);
        var hrAdmin = AuthData.Staff(email: "h@x.io", role: "hr_admin", active: true);
        var uow = new InMemoryUnitOfWork().Seed(activeRecruiter, inactiveRecruiter, hrAdmin);

        var res = await new GetUsersQueryHandler(uow)
            .Handle(new GetUsersQuery(null, "recruiter", true, 1, 10), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal(activeRecruiter.Id, res.Value.Items[0].Id);
    }

    [Fact]
    public async Task Item_carries_lock_reason()
    {
        var locked = AuthData.Staff(email: "r@x.io", role: "recruiter", active: false);
        locked.LockReason = "Vi phạm";
        var uow = new InMemoryUnitOfWork().Seed(locked);

        var res = await new GetUsersQueryHandler(uow)
            .Handle(new GetUsersQuery(null, "recruiter", false, 1, 10), CancellationToken.None);

        Assert.Equal("Vi phạm", Assert.Single(res.Value.Items).LockReason);
    }
}

/// <summary>Tài khoản chờ duyệt (<see cref="GetPendingUsersQueryHandler"/>, test-plan B29): chỉ user IsActive=false.</summary>
public class GetPendingUsersQueryHandlerTests
{
    [Fact]
    public async Task Returns_only_inactive_users()
    {
        var pending = AuthData.Staff(email: "p@x.io", active: false);
        var active = AuthData.Staff(email: "a@x.io", active: true);
        var uow = new InMemoryUnitOfWork().Seed(pending, active);

        var res = await new GetPendingUsersQueryHandler(uow).Handle(new GetPendingUsersQuery(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(pending.Id, Assert.Single(res.Value).Id);
    }
}

/// <summary>Thống kê admin (<see cref="GetAdminStatsQueryHandler"/>, test-plan B29): đếm user theo role/active + candidate + yêu cầu chờ duyệt.</summary>
public class GetAdminStatsQueryHandlerTests
{
    [Fact]
    public async Task Counts_users_candidates_and_pending_requests()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(AuthData.Staff(email: "sa@x.io", role: "super_admin", active: true),
                  AuthData.Staff(email: "hr@x.io", role: "hr_admin", active: true),
                  AuthData.Staff(email: "r1@x.io", role: "recruiter", active: true),
                  AuthData.Staff(email: "r2@x.io", role: "recruiter", active: false))
            .Seed(AuthData.Candidate(email: "c1@x.io"), AuthData.Candidate(email: "c2@x.io"))
            .Seed(new AccountRequest { Status = "pending", Email = "p@x.io" },
                  new AccountRequest { Status = "approved", Email = "q@x.io" });

        var res = await new GetAdminStatsQueryHandler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None);

        var s = res.Value;
        Assert.Equal(4, s.TotalUsers);
        Assert.Equal(3, s.ActiveUsers);
        Assert.Equal(1, s.LockedUsers);
        Assert.Equal(1, s.SuperAdmins);
        Assert.Equal(1, s.HrAdmins);
        Assert.Equal(2, s.Recruiters);
        Assert.Equal(2, s.Candidates);
        Assert.Equal(1, s.PendingRequests);  // chỉ đếm pending
    }
}
