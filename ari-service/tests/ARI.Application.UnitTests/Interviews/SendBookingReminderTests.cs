using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Nhắc lịch phỏng vấn (<see cref="ARI.Application.Services.InterviewService"/> <c>SendBookingReminderAsync</c>,
/// test-plan B15): guard thiếu booking/hồ sơ/ca; happy path đẩy DB notification 'schedule_reminder' + realtime,
/// email best-effort, và đóng dấu Reminder24hSent.
/// </summary>
public class SendBookingReminderTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => InterviewServiceFactory.Create(uow, notif);

    [Fact]
    public async Task Missing_booking_fails_without_notifying()
    {
        var notif = new RecordingNotificationService();
        var res = await Svc(new InMemoryUnitOfWork(), notif).SendBookingReminderAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy lịch phỏng vấn", res.Error);
        Assert.Empty(notif.UserEvents);
    }

    [Fact]
    public async Task Missing_application_fails_and_leaves_booking_unmarked()
    {
        var slot = SchedulingData.Slot(Guid.NewGuid());
        var booking = SchedulingData.Booking(Guid.NewGuid(), slot.Id); // App không seed
        var uow = new InMemoryUnitOfWork().Seed(slot).Seed(booking);

        var res = await Svc(uow, new()).SendBookingReminderAsync(booking.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy hồ sơ ứng viên", res.Error);
        Assert.False(booking.Reminder24hSent);
    }

    [Fact]
    public async Task Missing_slot_fails()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        var booking = SchedulingData.Booking(app.Id, Guid.NewGuid()); // Slot không seed
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(booking);

        var res = await Svc(uow, new()).SendBookingReminderAsync(booking.Id, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy ca phỏng vấn", res.Error);
        Assert.False(booking.Reminder24hSent);
    }

    [Fact]
    public async Task Reminder_notifies_candidate_and_marks_sent()
    {
        var accId = Guid.NewGuid();
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, accId);
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif).SendBookingReminderAsync(booking.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        var rec = Assert.Single(uow.Repo<Notification>().Items);
        Assert.Equal("schedule_reminder", rec.Type);
        Assert.Equal(accId, rec.CandidateAccountId);
        Assert.Contains((accId, "ReceiveUserNotification"), notif.UserEvents);
        Assert.True(booking.Reminder24hSent);
        Assert.True(uow.SaveChangesCount >= 1);
    }

    [Fact]
    public async Task Reminder_without_account_still_marks_sent_without_db_notification()
    {
        var job = SchedulingData.Job();
        var app = SchedulingData.Application(job.Id, accountId: null); // hồ sơ on-site không tài khoản
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif).SendBookingReminderAsync(booking.Id, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<Notification>().Items); // không có account → không tạo bell
        Assert.Empty(notif.UserEvents);
        Assert.True(booking.Reminder24hSent);          // vẫn đánh dấu đã nhắc (email best-effort)
    }
}
