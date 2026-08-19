using System;
using System.Linq;
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

/// <summary>
/// Lịch sử thao tác (<see cref="GetAuditLogsQueryHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "GetAuditLogs" (UTCID01–12): clamp page/pageSize, lọc action + entity (chuẩn hoá), phân trang,
/// sắp CreatedAt giảm dần, resolve tên actor (fullname → email → "Hệ thống"), và lỗi repository (audit / user).
/// </summary>
public class GetAuditLogsQueryHandlerTests
{
    private static AuditLog Log(string action = "x", Guid? actor = null, string? entityType = "User", DateTimeOffset? at = null)
        => new() { Action = action, ActorUserId = actor, EntityType = entityType, CreatedAt = at ?? DateTimeOffset.UtcNow };

    private static GetAuditLogsQueryHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    // UTCID01 — không có log, page/pageSize hợp lệ → trang rỗng
    [Fact]
    public async Task UTCID01_Empty_default_page()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetAuditLogsQuery(null, null, 1, 20), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value.TotalCount);
        Assert.Equal(1, res.Value.Page);
        Assert.Equal(20, res.Value.PageSize);
        Assert.Equal(0, res.Value.TotalPages);
        Assert.Empty(res.Value.Items);
    }

    // UTCID02 — Page=0, PageSize=0 → clamp về Page=1, PageSize=20
    [Fact]
    public async Task UTCID02_Page_and_size_clamped_to_defaults()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetAuditLogsQuery(null, null, 0, 0), CancellationToken.None);

        Assert.Equal(1, res.Value.Page);
        Assert.Equal(20, res.Value.PageSize);
    }

    // UTCID03 — PageSize=101 → clamp về 100
    [Fact]
    public async Task UTCID03_Page_size_clamped_to_max_100()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetAuditLogsQuery(null, null, 1, 101), CancellationToken.None);

        Assert.Equal(100, res.Value.PageSize);
    }

    // UTCID04 — lọc theo Action (chuẩn hoá trim+lower, case-insensitive)
    [Fact]
    public async Task UTCID04_Filters_by_action()
    {
        var uow = new InMemoryUnitOfWork().Seed(Log(action: "user_deleted"), Log(action: "user_created"));

        var res = await Handler(uow).Handle(new GetAuditLogsQuery(" USER_DELETED ", null, 1, 20), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal("user_deleted", res.Value.Items[0].Action);
    }

    // UTCID05 — lọc theo EntityType (chuẩn hoá)
    [Fact]
    public async Task UTCID05_Filters_by_entity_type()
    {
        var uow = new InMemoryUnitOfWork().Seed(Log(entityType: "User"), Log(entityType: "Job"));

        var res = await Handler(uow).Handle(new GetAuditLogsQuery(null, " USER ", 1, 20), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal("User", res.Value.Items[0].EntityType);
    }

    // UTCID06 — lọc theo cả Action + EntityType
    [Fact]
    public async Task UTCID06_Filters_by_action_and_entity()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            Log(action: "user_deleted", entityType: "User"),
            Log(action: "user_created", entityType: "User"),
            Log(action: "user_deleted", entityType: "Job"));

        var res = await Handler(uow).Handle(new GetAuditLogsQuery(" USER_DELETED ", " USER ", 1, 20), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal("user_deleted", res.Value.Items[0].Action);
        Assert.Equal("User", res.Value.Items[0].EntityType);
    }

    // UTCID07 — 5 log unordered, Page=2/PageSize=2 → lấy log mới thứ 3 & 4, TotalPages=3
    [Fact]
    public async Task UTCID07_Pagination_second_page()
    {
        var now = DateTimeOffset.UtcNow;
        var l0 = Log(at: now);               // mới nhất
        var l1 = Log(at: now.AddMinutes(-1));
        var l2 = Log(at: now.AddMinutes(-2));
        var l3 = Log(at: now.AddMinutes(-3));
        var l4 = Log(at: now.AddMinutes(-4));
        var uow = new InMemoryUnitOfWork().Seed(l3, l0, l4, l1, l2); // xáo trộn

        var res = await Handler(uow).Handle(new GetAuditLogsQuery(null, null, 2, 2), CancellationToken.None);

        Assert.Equal(5, res.Value.TotalCount);
        Assert.Equal(2, res.Value.Page);
        Assert.Equal(2, res.Value.PageSize);
        Assert.Equal(3, res.Value.TotalPages);
        Assert.Equal(new[] { l2.Id, l3.Id }, res.Value.Items.Select(i => i.Id));
    }

    // UTCID08 — actor có FullName → ActorName = FullName
    [Fact]
    public async Task UTCID08_Actor_name_from_full_name()
    {
        var actor = AuthData.Staff(fullName: "Admin User");
        var uow = new InMemoryUnitOfWork().Seed(actor).Seed(Log(actor: actor.Id));

        var res = await Handler(uow).Handle(new GetAuditLogsQuery(null, null, 1, 20), CancellationToken.None);

        Assert.Equal("Admin User", Assert.Single(res.Value.Items).ActorName);
    }

    // UTCID09 — actor FullName=null → ActorName fallback về Email
    [Fact]
    public async Task UTCID09_Actor_name_falls_back_to_email()
    {
        var actor = AuthData.Staff(email: "admin@example.com", fullName: null);
        var uow = new InMemoryUnitOfWork().Seed(actor).Seed(Log(actor: actor.Id));

        var res = await Handler(uow).Handle(new GetAuditLogsQuery(null, null, 1, 20), CancellationToken.None);

        Assert.Equal("admin@example.com", Assert.Single(res.Value.Items).ActorName);
    }

    // UTCID10 — actor null hoặc trỏ tới user không tồn tại → ActorName = "Hệ thống"
    [Fact]
    public async Task UTCID10_System_actor_name_when_missing()
    {
        var uow = new InMemoryUnitOfWork().Seed(Log(actor: null), Log(actor: Guid.NewGuid()));

        var res = await Handler(uow).Handle(new GetAuditLogsQuery(null, null, 1, 20), CancellationToken.None);

        Assert.Equal(2, res.Value.TotalCount);
        Assert.All(res.Value.Items, i => Assert.Equal("Hệ thống", i.ActorName));
    }

    // UTCID11 — audit repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID11_Audit_repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<AuditLog>("Audit DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetAuditLogsQuery(null, null, 1, 20), CancellationToken.None));

        Assert.Equal("Audit DB Error", ex.Message);
    }

    // UTCID12 — có log kèm actor nhưng user repository ném lỗi khi resolve tên → thoát ra ngoài
    [Fact]
    public async Task UTCID12_User_repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().Seed(Log(actor: Guid.NewGuid())).FailRepo<User>("User DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetAuditLogsQuery(null, null, 1, 20), CancellationToken.None));

        Assert.Equal("User DB Error", ex.Message);
    }
}

/// <summary>
/// Danh sách staff (<see cref="GetUsersQueryHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "GetUsersQuery" (UTCID01–13): clamp page/pageSize, lọc search/role/active (chuẩn hoá), phân trang,
/// sắp CreatedAt giảm dần, map DTO, và lỗi repository.
/// </summary>
public class GetUsersQueryHandlerTests
{
    private static User U(string email, string role = "recruiter", bool active = true, string? fullName = "User", DateTimeOffset? createdAt = null)
        => new() { Email = email, Role = role, IsActive = active, FullName = fullName, CreatedAt = createdAt ?? DateTimeOffset.UtcNow };

    private static GetUsersQueryHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    // UTCID01 — không có user → trang rỗng, mặc định PageSize=10
    [Fact]
    public async Task UTCID01_Empty_default_page()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetUsersQuery(null, null, null, 1, 10), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value.TotalCount);
        Assert.Equal(1, res.Value.Page);
        Assert.Equal(10, res.Value.PageSize);
        Assert.Equal(0, res.Value.TotalPages);
        Assert.Empty(res.Value.Items);
    }

    // UTCID02 — Page=0, PageSize=0 → clamp Page=1, PageSize=10
    [Fact]
    public async Task UTCID02_Page_and_size_clamped_to_defaults()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetUsersQuery(null, null, null, 0, 0), CancellationToken.None);

        Assert.Equal(1, res.Value.Page);
        Assert.Equal(10, res.Value.PageSize);
    }

    // UTCID03 — PageSize=101 → clamp 100
    [Fact]
    public async Task UTCID03_Page_size_clamped_to_max_100()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetUsersQuery(null, null, null, 1, 101), CancellationToken.None);

        Assert.Equal(100, res.Value.PageSize);
    }

    // UTCID04 — Search=" ", Role=" " (whitespace) → không loại ai, sắp mới nhất trước
    [Fact]
    public async Task UTCID04_Whitespace_filters_exclude_none()
    {
        var now = DateTimeOffset.UtcNow;
        var newest = U("new@example.com", createdAt: now);
        var oldest = U("old@example.com", createdAt: now.AddMinutes(-1));
        var uow = new InMemoryUnitOfWork().Seed(oldest, newest);

        var res = await Handler(uow).Handle(new GetUsersQuery(" ", " ", null, 1, 10), CancellationToken.None);

        Assert.Equal(2, res.Value.TotalCount);
        Assert.Equal(new[] { "new@example.com", "old@example.com" }, res.Value.Items.Select(i => i.Email));
    }

    // UTCID05 — Search=" ADMIN " khớp FullName
    [Fact]
    public async Task UTCID05_Search_by_full_name()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            U("a@example.com", fullName: "Admin User"),
            U("r@example.com", fullName: "Recruiter User"));

        var res = await Handler(uow).Handle(new GetUsersQuery(" ADMIN ", null, null, 1, 10), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal("Admin User", res.Value.Items[0].FullName);
    }

    // UTCID06 — Search=" STAFF@EXAMPLE.COM " khớp Email
    [Fact]
    public async Task UTCID06_Search_by_email()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            U("admin@example.com", fullName: "Admin"),
            U("staff@example.com", fullName: "Staff"));

        var res = await Handler(uow).Handle(new GetUsersQuery(" STAFF@EXAMPLE.COM ", null, null, 1, 10), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal("staff@example.com", res.Value.Items[0].Email);
    }

    // UTCID07 — Role=" HR_ADMIN " (chuẩn hoá) khớp role
    [Fact]
    public async Task UTCID07_Filter_by_role()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            U("h@example.com", role: "hr_admin"),
            U("r@example.com", role: "recruiter"));

        var res = await Handler(uow).Handle(new GetUsersQuery(null, " HR_ADMIN ", null, 1, 10), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        Assert.Equal("hr_admin", res.Value.Items[0].Role);
    }

    // UTCID08 — IsActive=true → chỉ user active
    [Fact]
    public async Task UTCID08_Filter_active_true()
    {
        var uow = new InMemoryUnitOfWork().Seed(U("a@example.com", active: true), U("i@example.com", active: false));

        var res = await Handler(uow).Handle(new GetUsersQuery(null, null, true, 1, 10), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        Assert.True(res.Value.Items[0].IsActive);
    }

    // UTCID09 — IsActive=false → chỉ user bị khóa
    [Fact]
    public async Task UTCID09_Filter_active_false()
    {
        var uow = new InMemoryUnitOfWork().Seed(U("a@example.com", active: true), U("i@example.com", active: false));

        var res = await Handler(uow).Handle(new GetUsersQuery(null, null, false, 1, 10), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        Assert.False(res.Value.Items[0].IsActive);
    }

    // UTCID10 — kết hợp Search + Role + IsActive → chỉ 1 user khớp hết
    [Fact]
    public async Task UTCID10_Combined_filters()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            U("admin@example.com", role: "hr_admin", active: true, fullName: "Admin One"),
            U("admin2@example.com", role: "recruiter", active: true, fullName: "Admin Two"),
            U("admin3@example.com", role: "hr_admin", active: false, fullName: "Admin Three"));

        var res = await Handler(uow).Handle(new GetUsersQuery("admin", "hr_admin", true, 1, 10), CancellationToken.None);

        Assert.Equal(1, res.Value.TotalCount);
        var item = res.Value.Items[0];
        Assert.Equal("admin@example.com", item.Email);
        Assert.Equal("hr_admin", item.Role);
        Assert.True(item.IsActive);
    }

    // UTCID11 — 5 user unordered, Page=2/PageSize=2 → user mới thứ 3 & 4, TotalPages=3
    [Fact]
    public async Task UTCID11_Pagination_second_page()
    {
        var now = DateTimeOffset.UtcNow;
        var u0 = U("u0@example.com", createdAt: now);
        var u1 = U("u1@example.com", createdAt: now.AddMinutes(-1));
        var u2 = U("u2@example.com", createdAt: now.AddMinutes(-2));
        var u3 = U("u3@example.com", createdAt: now.AddMinutes(-3));
        var u4 = U("u4@example.com", createdAt: now.AddMinutes(-4));
        var uow = new InMemoryUnitOfWork().Seed(u3, u0, u4, u1, u2);

        var res = await Handler(uow).Handle(new GetUsersQuery(null, null, null, 2, 2), CancellationToken.None);

        Assert.Equal(5, res.Value.TotalCount);
        Assert.Equal(3, res.Value.TotalPages);
        Assert.Equal(new[] { u2.Id, u3.Id }, res.Value.Items.Select(i => i.Id));
    }

    // UTCID12 — Search không khớp ai → trang rỗng
    [Fact]
    public async Task UTCID12_Search_no_match()
    {
        var uow = new InMemoryUnitOfWork().Seed(U("a@example.com", fullName: "Admin"));

        var res = await Handler(uow).Handle(new GetUsersQuery("not-found", null, null, 1, 10), CancellationToken.None);

        Assert.Equal(0, res.Value.TotalCount);
        Assert.Empty(res.Value.Items);
    }

    // UTCID13 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID13_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetUsersQuery(null, null, null, 1, 10), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}

/// <summary>
/// Tài khoản chờ duyệt (<see cref="GetPendingUsersQueryHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "GetPendingUsers" (UTCID01–06): chỉ user IsActive=false, map DTO (kể cả FullName null),
/// giữ nguyên thứ tự repository, rỗng, và lỗi repository.
/// </summary>
public class GetPendingUsersQueryHandlerTests
{
    private static User U(string email, bool active, string role = "recruiter", string? fullName = "User")
        => new() { Email = email, Role = role, IsActive = active, FullName = fullName };

    private static GetPendingUsersQueryHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    // UTCID01 — không có user inactive → []
    [Fact]
    public async Task UTCID01_Empty_when_none_inactive()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new GetPendingUsersQuery(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    // UTCID02 — 1 user inactive → 1 DTO đúng giá trị
    [Fact]
    public async Task UTCID02_Single_inactive_user()
    {
        var uow = new InMemoryUnitOfWork().Seed(U("locked@example.com", active: false, fullName: "Locked User"));

        var item = Assert.Single((await Handler(uow).Handle(new GetPendingUsersQuery(), CancellationToken.None)).Value);

        Assert.Equal("locked@example.com", item.Email);
        Assert.Equal("recruiter", item.Role);
        Assert.Equal("Locked User", item.FullName);
    }

    // UTCID03 — 1 active + 1 inactive → chỉ inactive
    [Fact]
    public async Task UTCID03_Only_inactive_returned()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            U("active@example.com", active: true, fullName: "Active User"),
            U("inactive@example.com", active: false, fullName: "Inactive User"));

        var item = Assert.Single((await Handler(uow).Handle(new GetPendingUsersQuery(), CancellationToken.None)).Value);

        Assert.Equal("inactive@example.com", item.Email);
        Assert.Equal("Inactive User", item.FullName);
    }

    // UTCID04 — repository trả theo thứ tự UserB, UserA → giữ nguyên thứ tự
    [Fact]
    public async Task UTCID04_Preserves_repository_order()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            U("b@example.com", active: false),
            U("a@example.com", active: false));

        var res = await Handler(uow).Handle(new GetPendingUsersQuery(), CancellationToken.None);

        Assert.Equal(new[] { "b@example.com", "a@example.com" }, res.Value.Select(u => u.Email));
    }

    // UTCID05 — user inactive có FullName=null → map null
    [Fact]
    public async Task UTCID05_Null_full_name_mapped()
    {
        var uow = new InMemoryUnitOfWork().Seed(U("locked@example.com", active: false, fullName: null));

        var item = Assert.Single((await Handler(uow).Handle(new GetPendingUsersQuery(), CancellationToken.None)).Value);

        Assert.Equal("locked@example.com", item.Email);
        Assert.Null(item.FullName);
    }

    // UTCID06 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID06_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<User>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetPendingUsersQuery(), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}

/// <summary>
/// Thống kê admin (<see cref="GetAdminStatsQueryHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "GetAdminStats" (UTCID01–10). Ghi chú: phần "Confirm" của tab này trong report bị dán nhầm giá trị của
/// tab "GetAccountRequests"; bộ test bám theo phần "Condition" đúng của tab + hành vi thật của handler
/// (đếm user theo role/active, ứng viên, yêu cầu chờ duyệt; chuẩn hoá role; lỗi từng repository).
/// </summary>
public class GetAdminStatsQueryHandlerTests
{
    private static User U(string role, bool active = true)
        => new() { Email = Guid.NewGuid().ToString("N") + "@example.com", Role = role, IsActive = active, FullName = "U" };

    private static GetAdminStatsQueryHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    // UTCID01 — không có dữ liệu → tất cả bằng 0
    [Fact]
    public async Task UTCID01_All_zero_when_empty()
    {
        var s = (await Handler(new InMemoryUnitOfWork()).Handle(new GetAdminStatsQuery(), CancellationToken.None)).Value;

        Assert.Equal(0, s.TotalUsers);
        Assert.Equal(0, s.ActiveUsers);
        Assert.Equal(0, s.LockedUsers);
        Assert.Equal(0, s.SuperAdmins);
        Assert.Equal(0, s.HrAdmins);
        Assert.Equal(0, s.Recruiters);
        Assert.Equal(0, s.Candidates);
        Assert.Equal(0, s.PendingRequests);
    }

    // UTCID02 — 1 super_admin active, 1 hr_admin active, 1 recruiter bị khóa; 5 ứng viên; 2 pending
    [Fact]
    public async Task UTCID02_Mixed_counts()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(U("super_admin", active: true), U("hr_admin", active: true), U("recruiter", active: false))
            .Seed(AuthData.Candidate("c1@example.com"), AuthData.Candidate("c2@example.com"), AuthData.Candidate("c3@example.com"),
                  AuthData.Candidate("c4@example.com"), AuthData.Candidate("c5@example.com"))
            .Seed(new AccountRequest { Status = "pending", Email = "p1@example.com" },
                  new AccountRequest { Status = "pending", Email = "p2@example.com" });

        var s = (await Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None)).Value;

        Assert.Equal(3, s.TotalUsers);
        Assert.Equal(2, s.ActiveUsers);
        Assert.Equal(1, s.LockedUsers);
        Assert.Equal(1, s.SuperAdmins);
        Assert.Equal(1, s.HrAdmins);
        Assert.Equal(1, s.Recruiters);
        Assert.Equal(5, s.Candidates);
        Assert.Equal(2, s.PendingRequests);
    }

    // UTCID03 — role có khoảng trắng/hoa/thường + null + "unknown" → đếm theo role đã chuẩn hoá
    [Fact]
    public async Task UTCID03_Role_normalization()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            U(" SUPER_ADMIN "), U("Hr_Admin"), U(" RECRUITER "), U(null!), U("unknown"));

        var s = (await Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None)).Value;

        Assert.Equal(5, s.TotalUsers);
        Assert.Equal(1, s.SuperAdmins);
        Assert.Equal(1, s.HrAdmins);
        Assert.Equal(1, s.Recruiters);
    }

    // UTCID04 — 3 user active → ActiveUsers=3, LockedUsers=0
    [Fact]
    public async Task UTCID04_All_active()
    {
        var uow = new InMemoryUnitOfWork().Seed(U("recruiter"), U("recruiter"), U("hr_admin"));

        var s = (await Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None)).Value;

        Assert.Equal(3, s.TotalUsers);
        Assert.Equal(3, s.ActiveUsers);
        Assert.Equal(0, s.LockedUsers);
    }

    // UTCID05 — 3 user bị khóa → LockedUsers=3, ActiveUsers=0
    [Fact]
    public async Task UTCID05_All_locked()
    {
        var uow = new InMemoryUnitOfWork().Seed(U("recruiter", active: false), U("recruiter", active: false), U("hr_admin", active: false));

        var s = (await Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None)).Value;

        Assert.Equal(0, s.ActiveUsers);
        Assert.Equal(3, s.LockedUsers);
    }

    // UTCID06 — không user/ứng viên; pending=3
    [Fact]
    public async Task UTCID06_Only_pending_requests()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            new AccountRequest { Status = "pending", Email = "p1@example.com" },
            new AccountRequest { Status = "pending", Email = "p2@example.com" },
            new AccountRequest { Status = "pending", Email = "p3@example.com" });

        var s = (await Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None)).Value;

        Assert.Equal(0, s.TotalUsers);
        Assert.Equal(0, s.Candidates);
        Assert.Equal(3, s.PendingRequests);
    }

    // UTCID07 — không user/pending; ứng viên=7
    [Fact]
    public async Task UTCID07_Only_candidates()
    {
        var uow = new InMemoryUnitOfWork();
        for (var i = 0; i < 7; i++) uow.Seed(AuthData.Candidate($"c{i}@example.com"));

        var s = (await Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None)).Value;

        Assert.Equal(0, s.TotalUsers);
        Assert.Equal(7, s.Candidates);
        Assert.Equal(0, s.PendingRequests);
    }

    // UTCID08 — user repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID08_User_repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<User>("User DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None));

        Assert.Equal("User DB Error", ex.Message);
    }

    // UTCID09 — candidate repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID09_Candidate_repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<CandidateAccount>("Candidate DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None));

        Assert.Equal("Candidate DB Error", ex.Message);
    }

    // UTCID10 — account-request repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID10_Request_repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<AccountRequest>("Request DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow).Handle(new GetAdminStatsQuery(), CancellationToken.None));

        Assert.Equal("Request DB Error", ex.Message);
    }
}
