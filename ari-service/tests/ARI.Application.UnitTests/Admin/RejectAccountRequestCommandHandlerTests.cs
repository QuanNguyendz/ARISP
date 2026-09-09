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
/// Từ chối yêu cầu tạo tài khoản (<see cref="RejectAccountRequestCommandHandler"/>) — theo test-plan
/// Report5 Unit v1.2, tab "RejectAccountRequest" (UTCID01–08): bắt buộc lý do, tồn tại yêu cầu,
/// yêu cầu đã xử lý (approved/rejected), happy path từ chối + audit + báo leader, actor nullable, lỗi repository.
/// </summary>
public class RejectAccountRequestCommandHandlerTests
{
    private static readonly Guid RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Leader = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string Reason = "Duplicate request";

    private static (RejectAccountRequestCommandHandler h, RecordingNotificationService notif) Make(InMemoryUnitOfWork uow)
    {
        var notif = new RecordingNotificationService();
        return (new RejectAccountRequestCommandHandler(uow, notif), notif);
    }

    private static AccountRequest Request(string status)
        => new()
        {
            Id = RequestId, Email = "staff@example.com", FullName = "Staff User",
            Role = "recruiter", Status = status, RequestedByUserId = Leader,
        };

    // UTCID01 — Reason=null → bắt buộc lý do
    [Fact]
    public async Task UTCID01_Null_reason_is_required()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(new RejectAccountRequestCommand(RequestId, null, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Vui lòng nhập lý do từ chối.", res.Error);
    }

    // UTCID02 — Reason="   " → bắt buộc lý do
    [Fact]
    public async Task UTCID02_Whitespace_reason_is_required()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(new RejectAccountRequestCommand(RequestId, "   ", ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Vui lòng nhập lý do từ chối.", res.Error);
    }

    // UTCID03 — yêu cầu không tồn tại → not_found
    [Fact]
    public async Task UTCID03_Request_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var (h, _) = Make(uow);

        var res = await h.Handle(new RejectAccountRequestCommand(RequestId, Reason, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy yêu cầu.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID04 — yêu cầu đã approved → "Yêu cầu này đã được xử lý."
    [Fact]
    public async Task UTCID04_Approved_request_already_processed()
    {
        var uow = new InMemoryUnitOfWork().Seed(Request("approved"));
        var (h, _) = Make(uow);

        var res = await h.Handle(new RejectAccountRequestCommand(RequestId, Reason, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Yêu cầu này đã được xử lý.", res.Error);
        Assert.Null(res.ErrorCode);
    }

    // UTCID05 — yêu cầu đã rejected → "Yêu cầu này đã được xử lý."
    [Fact]
    public async Task UTCID05_Rejected_request_already_processed()
    {
        var uow = new InMemoryUnitOfWork().Seed(Request("rejected"));
        var (h, _) = Make(uow);

        var res = await h.Handle(new RejectAccountRequestCommand(RequestId, Reason, ActorA), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Yêu cầu này đã được xử lý.", res.Error);
    }

    // UTCID06 — yêu cầu pending → từ chối thành công + audit + báo leader
    [Fact]
    public async Task UTCID06_Pending_request_is_rejected_with_audit_and_notify()
    {
        var req = Request("pending");
        var uow = new InMemoryUnitOfWork().Seed(req);
        var (h, notif) = Make(uow);

        var res = await h.Handle(new RejectAccountRequestCommand(RequestId, Reason, ActorA), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("rejected", req.Status);
        Assert.Equal(Reason, req.ReviewReason);
        Assert.Equal(ActorA, req.ReviewedByUserId);
        Assert.Equal("account_request_rejected", Assert.Single(uow.Repo<AuditLog>().Items).Action);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Contains((Leader, "ReceiveAccountRequestUpdate"), notif.UserEvents);
    }

    // UTCID07 — ActorId=null → vẫn từ chối thành công
    [Fact]
    public async Task UTCID07_Null_actor_still_rejects()
    {
        var req = Request("pending");
        var uow = new InMemoryUnitOfWork().Seed(req);
        var (h, _) = Make(uow);

        var res = await h.Handle(new RejectAccountRequestCommand(RequestId, Reason, null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("rejected", req.Status);
        Assert.Null(req.ReviewedByUserId);
    }

    // UTCID08 — repository ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID08_Repository_error_propagates()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<AccountRequest>("DB Error");
        var (h, _) = Make(uow);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => h.Handle(new RejectAccountRequestCommand(RequestId, Reason, ActorA), CancellationToken.None));

        Assert.Equal("DB Error", ex.Message);
    }
}
