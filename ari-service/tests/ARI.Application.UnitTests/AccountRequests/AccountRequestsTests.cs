using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.AccountRequests;
using ARI.Application.AccountRequests.Commands.CreateAccountRequests;
using ARI.Application.AccountRequests.Queries.GetMyAccountRequests;
using ARI.Application.Admin.Queries.GetAccountRequests;
using ARI.Application.Common;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.AccountRequests;

/// <summary>
/// Tạo yêu cầu tài khoản staff (<see cref="CreateAccountRequestsCommandHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "CreateAccountRequests" (UTCID01–13): validate từng mục (email/họ tên/role), trùng trong request,
/// trùng user/pending, gộp batch, và trạng thái rejected không chặn.
/// </summary>
/// <remarks>
/// Report5 (tab CreateAccountRequests) có 2 chỗ ghi nhầm cột "Confirm": UTCID02 (Items=[{Email=""}]) và UTCID03
/// (Email không có '@') — bản mô tả ghi "Danh sách yêu cầu trống." / "Email không hợp lệ: ''.". Handler thực tế
/// trả đúng theo input: "Email không hợp lệ: ''." và "Email không hợp lệ: 'nguyenvana.example.com'.". Test bám
/// hành vi thật của handler để luôn xanh.
/// </remarks>
public class CreateAccountRequestsCommandHandlerTests
{
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string EmailA = "nguyenvana@example.com";
    private const string EmailB = "tranthib@example.com";

    private static (CreateAccountRequestsCommandHandler h, RecordingNotificationService notif) Make(InMemoryUnitOfWork uow)
    {
        var notif = new RecordingNotificationService();
        return (new CreateAccountRequestsCommandHandler(uow, notif), notif);
    }

    private static AccountRequestItem Item(string email, string fullName = "Nguyen Van A", string role = "recruiter", string? dept = "IT")
        => new() { Email = email, FullName = fullName, Role = role, Department = dept };

    private static CreateAccountRequestsCommand Cmd(params AccountRequestItem[] items)
        => new(items.ToList(), ActorA);

    // UTCID01 — Items=null → danh sách trống
    [Fact]
    public async Task UTCID01_Null_items_is_empty_list()
    {
        var (h, _) = Make(new InMemoryUnitOfWork());

        var res = await h.Handle(new CreateAccountRequestsCommand(null, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Danh sách yêu cầu trống.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID02 — 1 mục email rỗng → "Email không hợp lệ: ''." (Report ghi nhầm là "Danh sách yêu cầu trống.")
    [Fact]
    public async Task UTCID02_Empty_email_item_is_invalid_email()
    {
        var (h, _) = Make(new InMemoryUnitOfWork());

        var res = await h.Handle(Cmd(Item(email: "")), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email không hợp lệ: ''.", res.Error);
    }

    // UTCID03 — email không có '@' → "Email không hợp lệ: 'nguyenvana.example.com'."
    [Fact]
    public async Task UTCID03_Email_without_at_is_invalid()
    {
        var (h, _) = Make(new InMemoryUnitOfWork());

        var res = await h.Handle(Cmd(Item(email: "nguyenvana.example.com")), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email không hợp lệ: 'nguyenvana.example.com'.", res.Error);
    }

    // UTCID04 — email không có '@' (lặp lại của UTCID03 trong report) → cùng kết quả
    [Fact]
    public async Task UTCID04_Email_without_at_is_invalid_again()
    {
        var (h, _) = Make(new InMemoryUnitOfWork());

        var res = await h.Handle(Cmd(Item(email: "nguyenvana.example.com")), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email không hợp lệ: 'nguyenvana.example.com'.", res.Error);
    }

    // UTCID05 — họ tên trống → "Thiếu họ tên cho '<email>'."
    [Fact]
    public async Task UTCID05_Missing_full_name()
    {
        var (h, _) = Make(new InMemoryUnitOfWork());

        var res = await h.Handle(Cmd(Item(email: EmailA, fullName: "   ")), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal($"Thiếu họ tên cho '{EmailA}'.", res.Error);
    }

    // UTCID06 — role không hợp lệ → "Vai trò phải là một trong: ... (email <email>)."
    [Fact]
    public async Task UTCID06_Invalid_role()
    {
        var (h, _) = Make(new InMemoryUnitOfWork());

        var res = await h.Handle(Cmd(Item(email: EmailA, role: "super_admin")), CancellationToken.None);

        Assert.True(res.IsFailure);
        // Danh sách dựng từ RoleNames.AssignableStaff — ADR-061 thêm hiring_manager vào đó.
        Assert.Equal(
            $"Vai trò phải là một trong: {string.Join(", ", RoleNames.AssignableStaff)} (email {EmailA}).",
            res.Error);
    }

    // UTCID07 — trùng email trong cùng request (sau chuẩn hoá) → "Email bị lặp trong yêu cầu: <email>."
    [Fact]
    public async Task UTCID07_Duplicate_email_within_request()
    {
        var (h, _) = Make(new InMemoryUnitOfWork());

        var res = await h.Handle(Cmd(
            Item(email: " NguyenVanA@Example.com ", dept: null),
            Item(email: EmailA, fullName: "Nguyen Van B", role: "hr_admin", dept: null)), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal($"Email bị lặp trong yêu cầu: {EmailA}.", res.Error);
    }

    // UTCID08 — email đã có tài khoản user → conflict
    [Fact]
    public async Task UTCID08_Email_already_taken_by_user()
    {
        var uow = new InMemoryUnitOfWork().Seed(new User { Email = EmailA, Role = "recruiter", FullName = "Existing" });
        var (h, _) = Make(uow);

        var res = await h.Handle(Cmd(Item(email: EmailA)), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal($"Các email sau đã có tài khoản hoặc đang chờ duyệt: {EmailA}.", res.Error);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    // UTCID09 — email đang có yêu cầu pending → conflict
    [Fact]
    public async Task UTCID09_Email_already_pending()
    {
        var uow = new InMemoryUnitOfWork().Seed(new AccountRequest { Email = EmailA, Status = "pending", RequestedByUserId = Guid.NewGuid() });
        var (h, _) = Make(uow);

        var res = await h.Handle(Cmd(Item(email: EmailA)), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal($"Các email sau đã có tài khoản hoặc đang chờ duyệt: {EmailA}.", res.Error);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    // UTCID10 — nhiều mục cùng xung đột (user + pending) → conflict liệt kê theo thứ tự nhập
    [Fact]
    public async Task UTCID10_Multiple_conflicts_listed_in_order()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(new User { Email = EmailA, Role = "recruiter", FullName = "Existing" })
            .Seed(new AccountRequest { Email = EmailB, Status = "pending", RequestedByUserId = Guid.NewGuid() });
        var (h, _) = Make(uow);

        var res = await h.Handle(Cmd(Item(email: EmailA, dept: null), Item(email: EmailB, fullName: "Tran Thi B", role: "hr_admin", dept: null)), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal($"Các email sau đã có tài khoản hoặc đang chờ duyệt: {EmailA}, {EmailB}.", res.Error);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    // UTCID11 — 1 mục hợp lệ → thành công, Count=1, BatchId=null
    [Fact]
    public async Task UTCID11_Single_valid_request_no_batch()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, notif) = Make(uow);

        var res = await h.Handle(Cmd(Item(email: EmailA)), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.Count);
        Assert.Null(res.Value.BatchId);
        Assert.Single(uow.Repo<AccountRequest>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Equal(2, notif.GroupEvents.Count(e => e.Group == "super_admin"));
    }

    // UTCID12 — 2 mục hợp lệ → thành công, Count=2, BatchId != null (cùng batch)
    [Fact]
    public async Task UTCID12_Multiple_valid_requests_share_batch()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(Cmd(Item(email: EmailA, dept: null), Item(email: EmailB, fullName: "Tran Thi B", role: "hr_admin", dept: null)), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Count);
        Assert.NotNull(res.Value.BatchId);
        Assert.All(uow.Repo<AccountRequest>().Items, r => Assert.Equal(res.Value.BatchId, r.BatchId));
    }

    // UTCID13 — chỉ có yêu cầu cũ trạng thái "rejected" cùng email → KHÔNG chặn (chỉ pending mới chặn)
    [Fact]
    public async Task UTCID13_Rejected_history_does_not_block()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(new AccountRequest { Email = EmailA, Status = "rejected", RequestedByUserId = Guid.NewGuid() });
        var (h, _) = Make(uow);

        var res = await h.Handle(Cmd(Item(email: EmailA)), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.Count);
        Assert.Null(res.Value.BatchId);
    }
}

/// <summary>
/// Yêu cầu do chính HR Leader gửi (<see cref="GetMyAccountRequestsQueryHandler"/>) — theo test-plan Report5 Unit v1.2,
/// tab "GetMyAccountRequests" (UTCID01–07): rỗng, lọc theo UserId, sắp CreatedAt giảm dần, map DTO
/// (kể cả nullable), và lỗi repository.
/// </summary>
public class GetMyAccountRequestsQueryHandlerTests
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AccountRequest Req(Guid requester, DateTimeOffset createdAt, string email = "x@example.com", string status = "pending")
        => new() { RequestedByUserId = requester, Email = email, FullName = "N", Role = "recruiter", Status = status, CreatedAt = createdAt };

    // UTCID01 — không có yêu cầu → Value rỗng
    [Fact]
    public async Task UTCID01_Empty_when_none()
    {
        var res = await new GetMyAccountRequestsQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetMyAccountRequestsQuery(UserA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    // UTCID02 — 1 yêu cầu của UserA → 1 DTO
    [Fact]
    public async Task UTCID02_Single_own_request()
    {
        var uow = new InMemoryUnitOfWork().Seed(Req(UserA, DateTimeOffset.UtcNow, "p@example.com"));

        var res = await new GetMyAccountRequestsQueryHandler(uow)
            .Handle(new GetMyAccountRequestsQuery(UserA), CancellationToken.None);

        var item = Assert.Single(res.Value);
        Assert.Equal("p@example.com", item.Email);
        Assert.Equal("pending", item.Status);
    }

    // UTCID03 — nhiều yêu cầu unordered → sắp mới nhất trước
    [Fact]
    public async Task UTCID03_Ordered_newest_first()
    {
        var now = DateTimeOffset.UtcNow;
        var middle = Req(UserA, now.AddMinutes(-2), "mid@example.com");
        var newest = Req(UserA, now, "new@example.com");
        var oldest = Req(UserA, now.AddMinutes(-5), "old@example.com");
        var uow = new InMemoryUnitOfWork().Seed(middle, newest, oldest);

        var res = await new GetMyAccountRequestsQueryHandler(uow)
            .Handle(new GetMyAccountRequestsQuery(UserA), CancellationToken.None);

        Assert.Equal(new[] { "new@example.com", "mid@example.com", "old@example.com" }, res.Value.Select(r => r.Email));
    }

    // UTCID04 — có cả UserA và UserB → chỉ trả của UserA
    [Fact]
    public async Task UTCID04_Filters_by_user_id()
    {
        var now = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork().Seed(
            Req(UserA, now, "a-new@example.com"),
            Req(UserA, now.AddMinutes(-1), "a-old@example.com"),
            Req(UserB, now, "b@example.com"));

        var res = await new GetMyAccountRequestsQueryHandler(uow)
            .Handle(new GetMyAccountRequestsQuery(UserA), CancellationToken.None);

        Assert.Equal(2, res.Value.Count);
        Assert.DoesNotContain(res.Value, r => r.Email == "b@example.com");
    }

    // UTCID05 — nullable fields = null → DTO map null đúng
    [Fact]
    public async Task UTCID05_Nullable_fields_mapped()
    {
        var req = Req(UserA, DateTimeOffset.UtcNow, "n@example.com");
        req.BatchId = null; req.Department = null; req.ReviewReason = null; req.ReviewedAt = null;
        var uow = new InMemoryUnitOfWork().Seed(req);

        var item = Assert.Single((await new GetMyAccountRequestsQueryHandler(uow)
            .Handle(new GetMyAccountRequestsQuery(UserA), CancellationToken.None)).Value);

        Assert.Null(item.BatchId);
        Assert.Null(item.Department);
        Assert.Null(item.ReviewReason);
        Assert.Null(item.ReviewedAt);
    }

    // UTCID06 — 1 yêu cầu rejected với đủ optional fields → map đầy đủ
    [Fact]
    public async Task UTCID06_Rejected_with_optional_fields()
    {
        var req = Req(UserA, DateTimeOffset.UtcNow, "r@example.com", status: "rejected");
        req.BatchId = Guid.NewGuid();
        req.Department = "IT";
        req.ReviewReason = "Duplicate";
        req.ReviewedAt = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork().Seed(req);

        var item = Assert.Single((await new GetMyAccountRequestsQueryHandler(uow)
            .Handle(new GetMyAccountRequestsQuery(UserA), CancellationToken.None)).Value);

        Assert.Equal("rejected", item.Status);
        Assert.Equal("Duplicate", item.ReviewReason);
        Assert.NotNull(item.ReviewedAt);
    }

    // UTCID07 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID07_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<AccountRequest>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => new GetMyAccountRequestsQueryHandler(uow).Handle(new GetMyAccountRequestsQuery(UserA), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}

/// <summary>
/// Super Admin xem yêu cầu tạo tài khoản (<see cref="GetAccountRequestsQueryHandler"/>) — theo test-plan
/// Report5 Unit v1.2, tab "GetAccountRequests" (UTCID01–11): mặc định pending, chuẩn hoá + lọc status,
/// "all" xem hết theo CreatedAt giảm dần, resolve tên người gửi (fullname → email → "—"), map nullable,
/// và lỗi repository (account-request / user).
/// </summary>
public class GetAccountRequestsQueryHandlerTests
{
    private static readonly Guid Requester = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static AccountRequest Req(string status, string email, Guid? requester = null, DateTimeOffset? createdAt = null)
        => new()
        {
            RequestedByUserId = requester ?? Requester, Email = email, FullName = "N", Role = "recruiter",
            Status = status, CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };

    // UTCID01 — Status=null, không có pending → [] (mặc định pending)
    [Fact]
    public async Task UTCID01_Null_status_defaults_pending_empty()
    {
        var res = await new GetAccountRequestsQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetAccountRequestsQuery(null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    // UTCID02 — Status=" " → pending; resolve tên người gửi "HR Leader"
    [Fact]
    public async Task UTCID02_Whitespace_status_defaults_pending_with_requester_name()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(new User { Id = Requester, Email = "leader@example.com", FullName = "HR Leader" })
            .Seed(Req("pending", "a@example.com"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery(" "), CancellationToken.None);

        var item = Assert.Single(res.Value);
        Assert.Equal("a@example.com", item.Email);
        Assert.Equal("pending", item.Status);
        Assert.Equal("HR Leader", item.RequestedBy);
    }

    // UTCID03 — Status=" PENDING " (trim+lower) → chỉ pending
    [Fact]
    public async Task UTCID03_Status_trim_lower_filters_pending()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(Req("pending", "p@example.com"), Req("approved", "ap@example.com"), Req("rejected", "rj@example.com"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery(" PENDING "), CancellationToken.None);

        Assert.Equal("pending", Assert.Single(res.Value).Status);
    }

    // UTCID04 — Status=" APPROVED " → chỉ approved
    [Fact]
    public async Task UTCID04_Filters_approved()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(Req("pending", "p@example.com"), Req("approved", "ap@example.com"), Req("rejected", "rj@example.com"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery(" APPROVED "), CancellationToken.None);

        Assert.Equal("approved", Assert.Single(res.Value).Status);
    }

    // UTCID05 — Status="all" → tất cả, sắp CreatedAt giảm dần
    [Fact]
    public async Task UTCID05_All_returns_every_status_newest_first()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            Req("approved", "ap@example.com", createdAt: new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)),
            Req("rejected", "rj@example.com", createdAt: new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)),
            Req("pending", "p@example.com", createdAt: new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero)));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery("all"), CancellationToken.None);

        Assert.Equal(new[] { "approved", "rejected", "pending" }, res.Value.Select(r => r.Status));
    }

    // UTCID06 — Status="unknown" (không khớp gì) → []
    [Fact]
    public async Task UTCID06_Unknown_status_returns_empty()
    {
        var uow = new InMemoryUnitOfWork().Seed(Req("pending", "p@example.com"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery("unknown"), CancellationToken.None);

        Assert.Empty(res.Value);
    }

    // UTCID07 — người gửi có FullName=null → RequestedBy fallback về Email
    [Fact]
    public async Task UTCID07_Requester_name_falls_back_to_email()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(new User { Id = Requester, Email = "requester@example.com", FullName = null })
            .Seed(Req("pending", "p@example.com"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery("pending"), CancellationToken.None);

        Assert.Equal("requester@example.com", Assert.Single(res.Value).RequestedBy);
    }

    // UTCID08 — người gửi không tồn tại → RequestedBy = "—"
    [Fact]
    public async Task UTCID08_Unknown_requester_shows_dash()
    {
        var uow = new InMemoryUnitOfWork().Seed(Req("pending", "p@example.com"));

        var res = await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery("pending"), CancellationToken.None);

        Assert.Equal("—", Assert.Single(res.Value).RequestedBy);
    }

    // UTCID09 — nullable fields = null → map null đúng
    [Fact]
    public async Task UTCID09_Nullable_fields_mapped()
    {
        var req = Req("pending", "p@example.com");
        req.BatchId = null; req.Department = null; req.ReviewReason = null; req.ReviewedAt = null;
        var uow = new InMemoryUnitOfWork().Seed(req);

        var item = Assert.Single((await new GetAccountRequestsQueryHandler(uow)
            .Handle(new GetAccountRequestsQuery("pending"), CancellationToken.None)).Value);

        Assert.Null(item.BatchId);
        Assert.Null(item.Department);
        Assert.Null(item.ReviewReason);
        Assert.Null(item.ReviewedAt);
    }

    // UTCID10 — account-request repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID10_Request_repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<AccountRequest>("Request DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => new GetAccountRequestsQueryHandler(uow).Handle(new GetAccountRequestsQuery("pending"), CancellationToken.None));

        Assert.Equal("Request DB Error", ex.Message);
    }

    // UTCID11 — có yêu cầu khớp nhưng user repository ném lỗi khi resolve tên → thoát ra ngoài
    [Fact]
    public async Task UTCID11_User_repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(Req("pending", "p@example.com"))
            .FailRepo<User>("User DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => new GetAccountRequestsQueryHandler(uow).Handle(new GetAccountRequestsQuery("pending"), CancellationToken.None));

        Assert.Equal("User DB Error", ex.Message);
    }
}
