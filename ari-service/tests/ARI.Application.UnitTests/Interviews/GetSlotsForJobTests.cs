using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Danh sách ca phỏng vấn của một job cho HR (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>GetSlotsForJobAsync</c>, test-plan B25): không ca → rỗng; đếm booking theo trạng thái xác nhận +
/// cờ IsPast theo giờ bắt đầu.
/// </summary>
public class GetSlotsForJobTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService());

    [Fact]
    public async Task No_slots_returns_empty()
    {
        var result = await Svc(new InMemoryUnitOfWork()).GetSlotsForJobAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Slot_counts_bookings_by_confirmation_and_flags_past()
    {
        var job = SchedulingData.Job();
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 5, start: SchedulingData.Past); // đã qua
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot)
            .Seed(SchedulingData.Booking(Guid.NewGuid(), slot.Id, confirmation: "confirmed"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, confirmation: "declined"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, confirmation: "pending"));

        var result = await Svc(uow).GetSlotsForJobAsync(job.Id, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(slot.Id, dto.SlotId);
        Assert.Equal(3, dto.BookedCount);
        Assert.Equal(1, dto.ConfirmedCount);
        Assert.Equal(1, dto.DeclinedCount);
        Assert.Equal(1, dto.PendingCount);
        Assert.True(dto.IsPast);
    }
}
