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
/// Super Admin duyệt yêu cầu tạo tài khoản HR (<see cref="ApproveAccountRequestCommandHandler"/>, test-plan B14):
/// guard (không tìm thấy / đã xử lý / email đã có user), và happy path tạo user + đóng dấu duyệt + audit +
/// email chào mừng + báo realtime cho leader yêu cầu.
/// </summary>
public class ApproveAccountRequestCommandHandlerTests
{
    private static ApproveAccountRequestCommandHandler Handler(
        InMemoryUnitOfWork uow, RecordingEmailService email, RecordingNotificationService notif)
        => new(uow, new FakePasswordHasher(), email, notif);

    private static AccountRequest Request(
        string status = "pending", string email = "hr@example.io", Guid? requestedBy = null) => new()
    {
        Email = email,
        FullName = "HR Person",
        Role = "recruiter",
        Department = "People",
        Status = status,
        RequestedByUserId = requestedBy ?? Guid.NewGuid(),
    };

    [Fact]
    public async Task Unknown_request_is_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Already_processed_request_is_rejected()
    {
        var req = Request(status: "approved");
        var uow = new InMemoryUnitOfWork().Seed(req);

        var res = await Handler(uow, new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(req.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã được xử lý", res.Error);
    }

    [Fact]
    public async Task Pending_request_with_existing_user_is_conflict_and_stays_pending()
    {
        var req = Request(email: "hr@example.io");
        var uow = new InMemoryUnitOfWork().Seed(req).Seed(AuthData.Staff(email: "hr@example.io"));

        var res = await Handler(uow, new RecordingEmailService(), new RecordingNotificationService())
            .Handle(new ApproveAccountRequestCommand(req.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Contains("đã có tài khoản", res.Error);
        Assert.Equal("pending", req.Status);   // không đổi
    }

    [Fact]
    public async Task Pending_request_is_approved_and_creates_user_with_notifications()
    {
        var actor = Guid.NewGuid();
        var leader = Guid.NewGuid();
        var req = Request(email: "  HR@X.io ", requestedBy: leader);
        var uow = new InMemoryUnitOfWork().Seed(req);
        var email = new RecordingEmailService();
        var notif = new RecordingNotificationService();

        var res = await Handler(uow, email, notif)
            .Handle(new ApproveAccountRequestCommand(req.Id, actor), CancellationToken.None);

        Assert.True(res.IsSuccess);

        var user = Assert.Single(uow.Repo<User>().Items);
        Assert.Equal("hr@x.io", user.Email);       // normalize
        Assert.Equal("recruiter", user.Role);
        Assert.Equal("HR Person", user.FullName);
        Assert.True(user.IsActive);

        // Yêu cầu được đóng dấu duyệt.
        Assert.Equal("approved", req.Status);
        Assert.Equal(actor, req.ReviewedByUserId);
        Assert.NotNull(req.ReviewedAt);
        Assert.Equal(user.Id, req.CreatedUserId);

        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("account_request_approved", audit.Action);

        Assert.Single(email.Sent);                                                       // email chào mừng
        Assert.Contains((leader, "ReceiveAccountRequestUpdate"), notif.UserEvents);       // báo leader
    }
}
