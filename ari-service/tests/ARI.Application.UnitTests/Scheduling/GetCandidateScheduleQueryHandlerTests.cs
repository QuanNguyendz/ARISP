using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Lịch của ứng viên đang đăng nhập (<see cref="GetCandidateScheduleQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetCandidateSchedule" (UTCID01–03): không có hồ sơ → rỗng; có 1 buổi sắp tới + 1 buổi đã qua → phân loại
/// đúng; repo hồ sơ ném lỗi → thoát ra ngoài. Kèm test bổ sung cho AwaitingReschedule (danh sách mới hơn report).
/// </summary>
/// <remarks>
/// Report mô tả DTO 2 danh sách (Upcoming, Past) — bản hiện tại có thêm danh sách thứ 3 (AwaitingReschedule).
/// Test bám hành vi thật của handler.
/// </remarks>
public class GetCandidateScheduleQueryHandlerTests
{
    private static readonly Guid AccountId = Guid.Parse("82000000-0000-0000-0000-000000000001");

    private static Task<Result<CandidateScheduleDto>> Run(InMemoryUnitOfWork uow)
        => new GetCandidateScheduleQueryHandler(uow)
            .Handle(new GetCandidateScheduleQuery(AccountId, "candidate@example.com"), CancellationToken.None);

    private static ARI.Domain.Entities.Application SeedApp(InMemoryUnitOfWork uow)
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, AccountId, status: "interview");
        uow.Seed(job).Seed(app);
        return app;
    }

    // UTCID01 — không có hồ sơ → tất cả danh sách rỗng
    [Fact]
    public async Task UTCID01_No_application_empty()
    {
        var res = await Run(new InMemoryUnitOfWork());
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value.Upcoming);
        Assert.Empty(res.Value.Past);
        Assert.Empty(res.Value.AwaitingReschedule);
    }

    // UTCID02 — 1 buổi sắp tới + 1 buổi đã qua (đều scheduled) → phân loại đúng
    [Fact]
    public async Task UTCID02_One_upcoming_one_past()
    {
        var uow = new InMemoryUnitOfWork();
        var app = SeedApp(uow);
        var future = SchedulingData.Slot(app.JobPostingId, start: DateTimeOffset.UtcNow.AddDays(2));
        var pastSlot = SchedulingData.Slot(app.JobPostingId, start: DateTimeOffset.UtcNow.AddDays(-2));
        uow.Seed(future, pastSlot)
           .Seed(SchedulingData.Booking(app.Id, future.Id, status: "scheduled"),
                 SchedulingData.Booking(app.Id, pastSlot.Id, status: "scheduled"));

        var res = await Run(uow);

        Assert.True(res.IsSuccess);
        Assert.Single(res.Value.Upcoming);
        Assert.Single(res.Value.Past);
    }

    // UTCID03 — repo hồ sơ ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID03_Application_repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<ARI.Domain.Entities.Application>("Application DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Application DB Error", ex.Message);
    }

    // --- Bổ sung: AwaitingReschedule (không có trong report 2-list, nhưng là hành vi thật cần giữ) ---

    [Fact]
    public async Task Extra_Recently_declined_booking_awaits_reschedule()
    {
        var uow = new InMemoryUnitOfWork();
        var app = SeedApp(uow);
        var slot = SchedulingData.Slot(app.JobPostingId);
        uow.Seed(slot).Seed(SchedulingData.Booking(app.Id, slot.Id, round: 1, status: "declined",
            confirmation: "declined", respondedAt: DateTimeOffset.UtcNow.AddHours(-2)));

        var res = await Run(uow);

        Assert.Single(res.Value.AwaitingReschedule);
        Assert.Empty(res.Value.Upcoming);
    }

    [Fact]
    public async Task Extra_Declined_round_with_new_booking_is_not_awaiting()
    {
        var uow = new InMemoryUnitOfWork();
        var app = SeedApp(uow);
        var oldSlot = SchedulingData.Slot(app.JobPostingId, start: DateTimeOffset.UtcNow.AddDays(-1));
        var newSlot = SchedulingData.Slot(app.JobPostingId, start: DateTimeOffset.UtcNow.AddDays(2));
        uow.Seed(oldSlot, newSlot)
           .Seed(SchedulingData.Booking(app.Id, oldSlot.Id, round: 1, status: "declined", confirmation: "declined",
                    respondedAt: DateTimeOffset.UtcNow.AddHours(-3)),
                 SchedulingData.Booking(app.Id, newSlot.Id, round: 1, status: "scheduled"));

        var res = await Run(uow);

        Assert.Empty(res.Value.AwaitingReschedule);
        Assert.Single(res.Value.Upcoming);
    }
}
