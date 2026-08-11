using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.RejectAccountRequest;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Admin;

/// <summary>
/// Super Admin từ chối yêu cầu tạo tài khoản (<see cref="RejectAccountRequestCommandHandler"/>, test-plan B24):
/// bắt buộc lý do (kiểm trước lookup), và happy path đóng dấu rejected + audit + báo leader.
/// </summary>
public class RejectAccountRequestCommandHandlerTests
{
    private static RejectAccountRequestCommandHandler Handler(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => new(uow, notif);

    private static AccountRequest PendingRequest(Guid requestedBy) => new()
    {
        Email = "hr@example.io",
        FullName = "HR Person",
        Role = "recruiter",
        Status = "pending",
        RequestedByUserId = requestedBy,
    };

    [Fact]
    public async Task Blank_reason_is_rejected_before_lookup()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new RecordingNotificationService())
            .Handle(new RejectAccountRequestCommand(Guid.NewGuid(), "   ", Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Vui lòng nhập lý do từ chối", res.Error);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Unknown_request_is_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new RecordingNotificationService())
            .Handle(new RejectAccountRequestCommand(Guid.NewGuid(), "Không phù hợp", Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Pending_request_is_rejected_with_audit_and_leader_notification()
    {
        var leader = Guid.NewGuid();
        var req = PendingRequest(leader);
        var uow = new InMemoryUnitOfWork().Seed(req);
        var notif = new RecordingNotificationService();

        var res = await Handler(uow, notif)
            .Handle(new RejectAccountRequestCommand(req.Id, "  Hồ sơ chưa đạt  ", Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("rejected", req.Status);
        Assert.Equal("Hồ sơ chưa đạt", req.ReviewReason);   // trim
        var audit = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("account_request_rejected", audit.Action);
        Assert.Contains((leader, "ReceiveAccountRequestUpdate"), notif.UserEvents);
    }
}
