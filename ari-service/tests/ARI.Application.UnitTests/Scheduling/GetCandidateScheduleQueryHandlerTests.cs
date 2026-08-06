using System;
using System.Linq;
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
/// Lịch của ứng viên đang đăng nhập (<see cref="GetCandidateScheduleQueryHandler"/>): phân loại
/// Upcoming / Past theo giờ slot, và AwaitingReschedule cho booking bị từ chối CHƯA được xếp lại.
/// </summary>
public class GetCandidateScheduleQueryHandlerTests
{
    private readonly Guid _accountId = Guid.NewGuid();

    private Task<Result<CandidateScheduleDto>> Run(InMemoryUnitOfWork uow)
        => new GetCandidateScheduleQueryHandler(uow)
            .Handle(new GetCandidateScheduleQuery(_accountId, null), CancellationToken.None);

    private ARI.Domain.Entities.Application NewApp(InMemoryUnitOfWork uow)
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, _accountId, status: "interview");
        uow.Seed(job).Seed(app);
        return app;
    }

    [Fact]
    public async Task Scheduled_future_booking_is_upcoming()
    {
        var uow = new InMemoryUnitOfWork();
        var app = NewApp(uow);
        var slot = SchedulingData.Slot(app.JobPostingId, start: DateTimeOffset.UtcNow.AddDays(2));
        uow.Seed(slot).Seed(SchedulingData.Booking(app.Id, slot.Id, status: "scheduled"));

        var res = await Run(uow);

        Assert.Single(res.Value.Upcoming);
        Assert.Empty(res.Value.Past);
        Assert.Empty(res.Value.AwaitingReschedule);
    }

    [Fact]
    public async Task Scheduled_past_booking_is_past()
    {
        var uow = new InMemoryUnitOfWork();
        var app = NewApp(uow);
        var slot = SchedulingData.Slot(app.JobPostingId, start: DateTimeOffset.UtcNow.AddDays(-2));
        uow.Seed(slot).Seed(SchedulingData.Booking(app.Id, slot.Id, status: "scheduled"));

        var res = await Run(uow);

        Assert.Single(res.Value.Past);
        Assert.Empty(res.Value.Upcoming);
    }

    [Fact]
    public async Task Recently_declined_booking_awaits_reschedule()
    {
        var uow = new InMemoryUnitOfWork();
        var app = NewApp(uow);
        var slot = SchedulingData.Slot(app.JobPostingId);
        uow.Seed(slot).Seed(SchedulingData.Booking(
            app.Id, slot.Id, round: 1, status: "declined", confirmation: "declined",
            respondedAt: DateTimeOffset.UtcNow.AddHours(-2)));

        var res = await Run(uow);

        Assert.Single(res.Value.AwaitingReschedule);
        Assert.Empty(res.Value.Upcoming);
        Assert.Empty(res.Value.Past);
    }

    [Fact]
    public async Task Declined_round_with_new_scheduled_booking_is_not_awaiting()
    {
        var uow = new InMemoryUnitOfWork();
        var app = NewApp(uow);
        var oldSlot = SchedulingData.Slot(app.JobPostingId, start: DateTimeOffset.UtcNow.AddDays(-1));
        var newSlot = SchedulingData.Slot(app.JobPostingId, start: DateTimeOffset.UtcNow.AddDays(2));
        uow.Seed(oldSlot, newSlot)
           .Seed(SchedulingData.Booking(app.Id, oldSlot.Id, round: 1, status: "declined", confirmation: "declined",
                    respondedAt: DateTimeOffset.UtcNow.AddHours(-3)))
           .Seed(SchedulingData.Booking(app.Id, newSlot.Id, round: 1, status: "scheduled"));

        var res = await Run(uow);

        Assert.Empty(res.Value.AwaitingReschedule); // vòng đã có lịch mới → không còn chờ xếp lại
        Assert.Single(res.Value.Upcoming);
    }

    [Fact]
    public async Task No_applications_returns_empty_lists()
    {
        var res = await Run(new InMemoryUnitOfWork());

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value.Upcoming);
        Assert.Empty(res.Value.Past);
        Assert.Empty(res.Value.AwaitingReschedule);
    }
}
