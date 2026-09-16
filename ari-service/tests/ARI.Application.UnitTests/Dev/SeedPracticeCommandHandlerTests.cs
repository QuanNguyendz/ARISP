using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Dev.SeedPractice;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Dev;

/// <summary>
/// Gieo hồ sơ phỏng vấn thử (DEV-ONLY, <see cref="SeedPracticeCommandHandler"/>, ADR-050): dựng candidate +
/// job sandbox + application <c>Status="interview"</c> (đủ điều kiện thử) + booking/slot. Idempotent theo marker
/// (tái dùng application), <c>Fresh=true</c> tạo application mới. Mọi tin sandbox vẫn có Hiring Manager chính (ADR-068).
/// </summary>
public class SeedPracticeCommandHandlerTests
{
    private static SeedPracticeCommandHandler Handler(InMemoryUnitOfWork uow)
        => new(uow, new FakePasswordHasher());

    [Fact]
    public async Task UTCID01_Fresh_seed_builds_eligible_application()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow).Handle(new SeedPracticeCommand(Fresh: false), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.False(res.Value.ReusedApplication);
        Assert.Equal("practice.dev@arisp.local", res.Value.CandidateEmail);
        Assert.Contains(res.Value.ApplicationId.ToString(), res.Value.PracticeUrl);

        var app = Assert.Single(uow.Repo<ARI.Domain.Entities.Application>().Items);
        Assert.Equal("interview", app.Status);                 // sau-xếp-lịch → PracticeEligible
        Assert.NotEmpty(uow.Repo<CandidateAccount>().Items);
        Assert.NotEmpty(uow.Repo<JobPosting>().Items);
        Assert.NotEmpty(uow.Repo<InterviewBooking>().Items);    // độ thực tế: có lịch sắp tới
        Assert.NotEmpty(uow.Repo<AvailabilitySlot>().Items);
    }

    [Fact]
    public async Task UTCID02_Sandbox_job_has_primary_hiring_manager()
    {
        var uow = new InMemoryUnitOfWork();

        await Handler(uow).Handle(new SeedPracticeCommand(), CancellationToken.None);

        Assert.Contains(uow.Repo<JobHiringTeamMember>().Items, m => m.IsPrimary); // ADR-068
    }

    [Fact]
    public async Task UTCID03_Second_call_reuses_application()
    {
        var uow = new InMemoryUnitOfWork();
        await Handler(uow).Handle(new SeedPracticeCommand(Fresh: false), CancellationToken.None);

        var second = await Handler(uow).Handle(new SeedPracticeCommand(Fresh: false), CancellationToken.None);

        Assert.True(second.Value.ReusedApplication);
        Assert.Single(uow.Repo<ARI.Domain.Entities.Application>().Items);   // không tạo trùng
    }

    [Fact]
    public async Task UTCID04_Fresh_true_creates_new_application()
    {
        var uow = new InMemoryUnitOfWork();
        await Handler(uow).Handle(new SeedPracticeCommand(Fresh: false), CancellationToken.None);

        var fresh = await Handler(uow).Handle(new SeedPracticeCommand(Fresh: true), CancellationToken.None);

        Assert.False(fresh.Value.ReusedApplication);
        Assert.Equal(2, uow.Repo<ARI.Domain.Entities.Application>().Items.Count);
    }
}
