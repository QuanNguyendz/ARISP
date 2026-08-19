using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Commands;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Wrapper CQRS đổi trạng thái hồ sơ (<see cref="UpdateApplicationStatusCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "UpdateApplication" (UTCID01–05): forward Id/Status tới service và trả nguyên kết quả (success/failure),
/// tôn trọng cancellation, propagate exception. (Logic nghiệp vụ nằm ở service — test riêng.)
/// </summary>
public class UpdateApplicationStatusCommandHandlerTests
{
    private static readonly Guid AppId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    private static UpdateApplicationStatusCommandHandler Handler(StubApplicationService svc) => new(svc);

    // UTCID01 — service trả app đã cập nhật → Success, forward đúng Id/Status
    [Fact]
    public async Task UTCID01_Forwards_and_returns_updated()
    {
        var svc = new StubApplicationService { UpdateResult = Result.Success(new ApplicationResponse { Id = AppId, Status = "interview" }) };

        var res = await Handler(svc).Handle(new UpdateApplicationStatusCommand(AppId, "interview"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("interview", res.Value.Status);
        Assert.Equal(AppId, svc.LastUpdateId);
        Assert.Equal("interview", svc.LastUpdateStatus);
    }

    // UTCID02 — service trả not-found failure → trả nguyên
    [Fact]
    public async Task UTCID02_Not_found_failure_passthrough()
    {
        var svc = new StubApplicationService { UpdateResult = Result.Failure<ApplicationResponse>("Application not found.") };

        var res = await Handler(svc).Handle(new UpdateApplicationStatusCommand(AppId, "interview"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Application not found.", res.Error);
    }

    // UTCID03 — Status rỗng; service trả validation failure → trả nguyên
    [Fact]
    public async Task UTCID03_Validation_failure_passthrough()
    {
        var svc = new StubApplicationService { UpdateResult = Result.Failure<ApplicationResponse>("Status cannot be empty.") };

        var res = await Handler(svc).Handle(new UpdateApplicationStatusCommand(AppId, ""), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Status cannot be empty.", res.Error);
        Assert.Equal("", svc.LastUpdateStatus);
    }

    // UTCID04 — token đã huỷ → OperationCanceledException
    [Fact]
    public async Task UTCID04_Cancelled_token_throws()
    {
        var svc = new StubApplicationService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Handler(svc).Handle(new UpdateApplicationStatusCommand(AppId, "interview"), cts.Token));
    }

    // UTCID05 — service ném lỗi → propagate
    [Fact]
    public async Task UTCID05_Service_throws()
    {
        var svc = new StubApplicationService { UpdateThrows = new Exception("Service Error") };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => Handler(svc).Handle(new UpdateApplicationStatusCommand(AppId, "interview"), CancellationToken.None));
        Assert.Equal("Service Error", ex.Message);
    }
}
