using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Dev.RegradeSession;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.Dev;

/// <summary>
/// Chấm lại phiên phỏng vấn (DEV-ONLY, <see cref="RegradeSessionCommandHandler"/>, ADR-053) — wrapper MỎNG:
/// forward <c>SessionId</c> + ngôn ngữ tuỳ chọn xuống <c>IInterviewService.RegenerateEvaluationAsync</c> và trả
/// nguyên kết quả (success/failure), propagate exception.
/// </summary>
public class RegradeSessionCommandHandlerTests
{
    private static readonly Guid SessionId = Guid.NewGuid();

    [Fact]
    public async Task UTCID01_Forwards_session_and_language()
    {
        var svc = new FakeInterviewService { RegradeResult = Result.Success(true) };

        var res = await new RegradeSessionCommandHandler(svc)
            .Handle(new RegradeSessionCommand(SessionId, "vi"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal((SessionId, "vi"), svc.LastRegrade);
    }

    [Fact]
    public async Task UTCID02_Null_language_forwarded()
    {
        var svc = new FakeInterviewService();

        await new RegradeSessionCommandHandler(svc).Handle(new RegradeSessionCommand(SessionId), CancellationToken.None);

        Assert.Equal((SessionId, (string?)null), svc.LastRegrade);
    }

    [Fact]
    public async Task UTCID03_Failure_passthrough()
    {
        var svc = new FakeInterviewService { RegradeResult = Result<bool>.Failure("Phiên chưa kết thúc.") };

        var res = await new RegradeSessionCommandHandler(svc)
            .Handle(new RegradeSessionCommand(SessionId), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Phiên chưa kết thúc.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Service_throws_propagates()
    {
        var svc = new FakeInterviewService { RegradeThrows = new Exception("Regrade Error") };

        var ex = await Assert.ThrowsAsync<Exception>(() => new RegradeSessionCommandHandler(svc)
            .Handle(new RegradeSessionCommand(SessionId), CancellationToken.None));
        Assert.Equal("Regrade Error", ex.Message);
    }
}
