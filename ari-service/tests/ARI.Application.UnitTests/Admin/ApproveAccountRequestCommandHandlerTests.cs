using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.ApproveAccountRequest;
using ARI.Application.Common;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Super Admin duyệt yêu cầu tạo tài khoản HR (<see cref="ApproveAccountRequestCommandHandler"/>) — theo test-plan
/// Report5 Unit v1.2, tab "ApproveAccountRequest" (UTCID01–09): tồn tại yêu cầu, trạng thái yêu cầu,
/// xung đột email (kể cả sau chuẩn hoá), actor nullable, happy path tạo user + audit + email + báo leader,
/// biên Guid.Empty, và lỗi repository.
/// </summary>
public class ApproveAccountRequestCommandHandlerTests
{
    private static readonly Guid RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Leader = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static ApproveAccountRequestCommandHandler Handler(
        InMemoryUnitOfWork uow, RecordingEmailService email, RecordingNotificationService notif)
        => new(uow, new FakePasswordHasher(), email, notif, AuthData.EmptyConfig());

    private static AccountRequest Request(string status = "pending", string email = "staff@example.com")
        => new()
        {
            Id = RequestId, Email = email, FullName = "Staff User",
            Role = "recruiter", Department = "IT", Status = status, RequestedByUserId = Leader,
        };

    private static User ExistingUser(string email)
        => new() { Email = email, Role = "recruiter", FullName = "Existing", IsActive = true };

    // UTCID01 — không có yêu cầu với Id → not_found
    [Fact]
    public async Task UTCID01_Request_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(RequestId, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy yêu cầu.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID02 — yêu cầu đã approved → "Yêu cầu này đã được xử lý."
    [Fact]
    public async Task UTCID02_Approved_request_already_processed()
    {
        var uow = new InMemoryUnitOfWork().Seed(Request(status: "approved"));

        var res = await Handler(uow, new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(RequestId, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Yêu cầu này đã được xử lý.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID03 — yêu cầu đã rejected → "Yêu cầu này đã được xử lý."
    [Fact]
    public async Task UTCID03_Rejected_request_already_processed()
    {
        var uow = new InMemoryUnitOfWork().Seed(Request(status: "rejected"));

        var res = await Handler(uow, new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(RequestId, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Yêu cầu này đã được xử lý.", res.Error);
    }

    // UTCID04 — email trùng user đã có → conflict
    [Fact]
    public async Task UTCID04_Existing_email_is_conflict()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(Request(email: "staff@example.com"))
            .Seed(ExistingUser("staff@example.com"));

        var res = await Handler(uow, new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(RequestId, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email này đã có tài khoản. Hãy từ chối yêu cầu.", res.Error);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    // UTCID05 — email yêu cầu " STAFF@EXAMPLE.COM " sau chuẩn hoá vẫn trùng user đã có → conflict
    [Fact]
    public async Task UTCID05_Existing_email_conflict_after_normalization()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(Request(email: " STAFF@EXAMPLE.COM "))
            .Seed(ExistingUser("staff@example.com"));

        var res = await Handler(uow, new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(RequestId, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
    }

    // UTCID06 — pending, chưa có user → tạo user + duyệt + audit + email + báo leader
    [Fact]
    public async Task UTCID06_Pending_request_is_approved_and_creates_user()
    {
        var req = Request(email: "staff@example.com");
        var uow = new InMemoryUnitOfWork().Seed(req);
        var email = new RecordingEmailService();
        var notif = new RecordingNotificationService();

        var res = await Handler(uow, email, notif)
            .Handle(new ApproveAccountRequestCommand(RequestId, ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var user = Assert.Single(uow.Repo<User>().Items);
        Assert.Equal("staff@example.com", user.Email);
        Assert.Equal("recruiter", user.Role);
        Assert.True(user.IsActive);
        Assert.Equal("approved", req.Status);
        Assert.Equal(ActorA, req.ReviewedByUserId);
        Assert.Equal(user.Id, req.CreatedUserId);
        Assert.Equal("account_request_approved", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Single(email.Sent);
        Assert.Contains((Leader, "ReceiveAccountRequestUpdate"), notif.UserEvents);
    }

    // UTCID07 — ActorId=null → vẫn duyệt thành công
    [Fact]
    public async Task UTCID07_Null_actor_still_approves()
    {
        var req = Request(email: "staff@example.com");
        var uow = new InMemoryUnitOfWork().Seed(req);

        var res = await Handler(uow, new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(RequestId, null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("approved", req.Status);
        Assert.Null(req.ReviewedByUserId);
    }

    // UTCID08 — Id=Guid.Empty (biên) → not_found
    [Fact]
    public async Task UTCID08_Empty_id_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(Guid.Empty, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy yêu cầu.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID09 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID09_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<AccountRequest>("DB Error");

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(uow, new RecordingEmailService(), new RecordingNotificationService())
                .Handle(new ApproveAccountRequestCommand(RequestId, ActorA), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
