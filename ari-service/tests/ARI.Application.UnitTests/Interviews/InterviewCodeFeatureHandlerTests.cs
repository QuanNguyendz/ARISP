using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interviews;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Cấp Interview Code — wrapper CQRS (<see cref="GenerateInterviewCodeCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GenerateInterviewCode" (UTCID01–04): forward đúng tham số, propagate success/failure/exception.
/// </summary>
public class GenerateInterviewCodeCommandHandlerTests
{
    private static readonly Guid AppA = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid HrA = Guid.Parse("41000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task UTCID01_Forwards_success()
    {
        var svc = new FakeInterviewCodeService();
        var expected = Result.Success(new InterviewCode { Code = "ABC123" });
        svc.GenerateResult = expected;

        var res = await new GenerateInterviewCodeCommandHandler(svc).Handle(new GenerateInterviewCodeCommand(AppA, 2, HrA), CancellationToken.None);

        Assert.Same(expected, res);
        Assert.Equal((AppA, (int?)2, HrA), svc.LastGenerate);
    }

    [Fact]
    public async Task UTCID02_Null_round_forwards()
    {
        var svc = new FakeInterviewCodeService();
        var res = await new GenerateInterviewCodeCommandHandler(svc).Handle(new GenerateInterviewCodeCommand(AppA, null, HrA), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal((AppA, (int?)null, HrA), svc.LastGenerate);
    }

    [Fact]
    public async Task UTCID03_Failure_forwarded()
    {
        var svc = new FakeInterviewCodeService { GenerateResult = Result.Failure<InterviewCode>("Generate code failed.") };
        var res = await new GenerateInterviewCodeCommandHandler(svc).Handle(new GenerateInterviewCodeCommand(AppA, 2, HrA), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Generate code failed.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Exception_propagates()
    {
        var svc = new FakeInterviewCodeService { GenerateThrows = new Exception("Interview Code Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new GenerateInterviewCodeCommandHandler(svc).Handle(new GenerateInterviewCodeCommand(AppA, 2, HrA), CancellationToken.None));
        Assert.Equal("Interview Code Error", ex.Message);
    }
}

/// <summary>
/// Cấp Interview Code hàng loạt (<see cref="GenerateInterviewCodeBatchCommandHandler"/>) — Report5 Unit v1.2,
/// tab "GenerateInterviewCodeBatch" (UTCID01–04).
/// </summary>
public class GenerateInterviewCodeBatchCommandHandlerTests
{
    private static readonly Guid AppA = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid AppB = Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly Guid HrA = Guid.Parse("41000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task UTCID01_Forwards_list()
    {
        var svc = new FakeInterviewCodeService();
        var expected = Result.Success(new List<InterviewCode> { new(), new() });
        svc.BatchResult = expected;
        var ids = new List<Guid> { AppA, AppB };

        var res = await new GenerateInterviewCodeBatchCommandHandler(svc).Handle(new GenerateInterviewCodeBatchCommand(ids, 1, HrA), CancellationToken.None);

        Assert.Same(expected, res);
        Assert.Equal((ids, (int?)1, HrA), svc.LastBatch);
    }

    [Fact]
    public async Task UTCID02_Empty_list_forwards()
    {
        var svc = new FakeInterviewCodeService();
        var res = await new GenerateInterviewCodeBatchCommandHandler(svc).Handle(new GenerateInterviewCodeBatchCommand(new List<Guid>(), null, HrA), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.NotNull(svc.LastBatch);
    }

    [Fact]
    public async Task UTCID03_Failure_forwarded()
    {
        var svc = new FakeInterviewCodeService { BatchResult = Result.Failure<List<InterviewCode>>("Batch generation failed.") };
        var res = await new GenerateInterviewCodeBatchCommandHandler(svc).Handle(new GenerateInterviewCodeBatchCommand(new List<Guid> { AppA }, 1, HrA), CancellationToken.None);
        Assert.Equal("Batch generation failed.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Exception_propagates()
    {
        var svc = new FakeInterviewCodeService { BatchThrows = new Exception("Batch Code Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new GenerateInterviewCodeBatchCommandHandler(svc).Handle(new GenerateInterviewCodeBatchCommand(new List<Guid> { AppA }, 1, HrA), CancellationToken.None));
        Assert.Equal("Batch Code Error", ex.Message);
    }
}

/// <summary>
/// Xác thực Interview Code tại Kiosk (<see cref="ValidateInterviewCodeCommandHandler"/>) — Report5 Unit v1.2,
/// tab "ValidateInterviewCode" (UTCID01–04): gọi ValidateCodeAsync 1 lần với đúng code, propagate.
/// </summary>
public class ValidateInterviewCodeCommandHandlerTests
{
    [Fact]
    public async Task UTCID01_Valid_forwards()
    {
        var svc = new FakeInterviewCodeService { ValidateResult = Result.Success(new KioskSessionResponse { Valid = true }) };
        var res = await new ValidateInterviewCodeCommandHandler(svc).Handle(new ValidateInterviewCodeCommand("ABC123"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("ABC123", svc.LastValidatedCode);
        Assert.Equal(1, svc.ValidateCallCount);
    }

    [Fact]
    public async Task UTCID02_Invalid_forwards()
    {
        var svc = new FakeInterviewCodeService { ValidateResult = Result.Success(new KioskSessionResponse { Valid = false }) };
        var res = await new ValidateInterviewCodeCommandHandler(svc).Handle(new ValidateInterviewCodeCommand("ABC123"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.False(res.Value.Valid);
    }

    [Fact]
    public async Task UTCID03_Failure_forwarded()
    {
        var svc = new FakeInterviewCodeService { ValidateResult = Result.Failure<KioskSessionResponse>("Code validation failed.") };
        var res = await new ValidateInterviewCodeCommandHandler(svc).Handle(new ValidateInterviewCodeCommand("ABC123"), CancellationToken.None);
        Assert.Equal("Code validation failed.", res.Error);
    }

    [Fact]
    public async Task UTCID04_Exception_propagates()
    {
        var svc = new FakeInterviewCodeService { ValidateThrows = new Exception("Validation Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new ValidateInterviewCodeCommandHandler(svc).Handle(new ValidateInterviewCodeCommand("ABC123"), CancellationToken.None));
        Assert.Equal("Validation Error", ex.Message);
    }
}

/// <summary>
/// Danh sách Interview Code theo job (<see cref="GetInterviewCodesByJobQueryHandler"/>) — Report5 Unit v1.2,
/// tab "GetInterviewCodesByJob" (UTCID01–03): gọi 1 lần với đúng jobId, bọc Result.Success, propagate exception.
/// </summary>
public class GetInterviewCodesByJobQueryHandlerTests
{
    private static readonly Guid JobA = Guid.Parse("42000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task UTCID01_Returns_summaries()
    {
        var svc = new FakeInterviewCodeService { CodesByJob = new() { new(), new() } };
        var res = await new GetInterviewCodesByJobQueryHandler(svc).Handle(new GetInterviewCodesByJobQuery(JobA), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Same(svc.CodesByJob, res.Value);
        Assert.Equal(JobA, svc.LastCodesJobId);
        Assert.Equal(1, svc.CodesByJobCallCount);
    }

    [Fact]
    public async Task UTCID02_Empty_list()
    {
        var svc = new FakeInterviewCodeService();
        var res = await new GetInterviewCodesByJobQueryHandler(svc).Handle(new GetInterviewCodesByJobQuery(JobA), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID03_Exception_propagates()
    {
        var svc = new FakeInterviewCodeService { CodesByJobThrows = new Exception("Interview Code DB Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => new GetInterviewCodesByJobQueryHandler(svc).Handle(new GetInterviewCodesByJobQuery(JobA), CancellationToken.None));
        Assert.Equal("Interview Code DB Error", ex.Message);
    }
}
