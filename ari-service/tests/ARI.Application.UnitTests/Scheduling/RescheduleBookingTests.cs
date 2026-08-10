using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Dời lịch phỏng vấn (<see cref="ARI.Application.Services.InterviewService"/> <c>RescheduleBookingAsync</c>,
/// test-plan B4). Nhân sự chuyển 1 booking sang ca khác CÙNG VÒNG: kiểm các guard (không tự dời sang chính
/// mình / ca đích thiếu / khác vòng / quá khứ / đã đầy), nhả–chốt chỗ 2 ca, reset booking về chờ xác nhận,
/// và báo ứng viên (bell + realtime InterviewRescheduled).
/// </summary>
public class RescheduleBookingTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => InterviewServiceFactory.Create(uow, notif);

    // ---------- Guards ----------

    [Fact]
    public async Task Booking_not_found_fails()
    {
        var res = await Svc(new InMemoryUnitOfWork(), new())
            .RescheduleBookingAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy lịch phỏng vấn", res.Error);
    }

    [Fact]
    public async Task Rescheduling_to_the_same_slot_is_rejected()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);

        var res = await Svc(uow, new()).RescheduleBookingAsync(booking.Id, slot.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã nằm trong ca này", res.Error);
    }

    [Fact]
    public async Task Missing_target_slot_fails()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);

        var res = await Svc(uow, new()).RescheduleBookingAsync(booking.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy ca phỏng vấn đích", res.Error);
    }

    [Fact]
    public async Task Target_slot_of_different_round_is_rejected()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1);
        var target = SchedulingData.Slot(job.Id, round: 2); // khác vòng
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);

        var res = await Svc(uow, new()).RescheduleBookingAsync(booking.Id, target.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("thuộc vòng thi khác", res.Error);
        Assert.Equal(0, target.BookedCount); // không chốt chỗ khi guard fail
    }

    [Fact]
    public async Task Target_slot_in_the_past_is_rejected()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, start: SchedulingData.Past); // đã qua
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);

        var res = await Svc(uow, new()).RescheduleBookingAsync(booking.Id, target.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã diễn ra trong quá khứ", res.Error);
    }

    [Fact]
    public async Task Full_target_slot_is_rejected()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1); // đã đầy
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);

        var res = await Svc(uow, new()).RescheduleBookingAsync(booking.Id, target.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã đầy", res.Error);
        Assert.Equal(1, target.BookedCount); // không đổi
    }

    // ---------- Happy path ----------

    [Fact]
    public async Task Reschedule_moves_seat_resets_booking_and_notifies_candidate()
    {
        var accId = Guid.NewGuid();
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, accId, status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1,
            status: "declined", confirmation: "declined", respondedAt: DateTimeOffset.UtcNow.AddDays(-1));
        booking.DeclineReason = "Bận đột xuất";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif).RescheduleBookingAsync(booking.Id, target.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        // Nhả chỗ ca cũ, chốt chỗ ca mới.
        Assert.Equal(0, oldSlot.BookedCount);
        Assert.Equal(1, target.BookedCount);
        // Booking trỏ sang ca mới + reset về "chờ xác nhận", xoá dấu vết từ chối.
        Assert.Equal(target.Id, booking.AvailabilitySlotId);
        Assert.Equal("scheduled", booking.Status);
        Assert.Equal("pending", booking.ConfirmationStatus);
        Assert.Null(booking.DeclineReason);
        Assert.Null(booking.RespondedAt);
        // Thông báo ứng viên: bell record + realtime.
        var rec = Assert.Single(uow.Repo<Notification>().Items);
        Assert.Equal("schedule_rescheduled", rec.Type);
        Assert.Equal(accId, rec.CandidateAccountId);
        Assert.Contains((accId, "ReceiveUserNotification"), notif.UserEvents);
    }

    [Fact]
    public async Task Reschedule_from_empty_old_slot_floors_booked_count_at_zero()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 0); // đã ở 0
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);

        var res = await Svc(uow, new()).RescheduleBookingAsync(booking.Id, target.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, oldSlot.BookedCount); // Math.Max(0, -1) → không âm
        Assert.Equal(1, target.BookedCount);
    }

    [Fact]
    public async Task Reschedule_without_candidate_account_succeeds_without_notification()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, accountId: null, status: "interview"); // hồ sơ không có account
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif).RescheduleBookingAsync(booking.Id, target.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(target.Id, booking.AvailabilitySlotId);   // vẫn dời ca
        Assert.Empty(uow.Repo<Notification>().Items);           // không tạo bell (không account)
        Assert.Empty(notif.UserEvents);                        // không realtime
    }
}
