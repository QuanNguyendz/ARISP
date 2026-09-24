using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
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
        var res = await Svc(new InMemoryUnitOfWork(), notif).SendBookingReminderAsync(Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin, ct: CancellationToken.None);

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

        var res = await Svc(uow, new()).SendBookingReminderAsync(booking.Id, Guid.NewGuid(), AppRoles.HrAdmin, ct: CancellationToken.None);

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

        var res = await Svc(uow, new()).SendBookingReminderAsync(booking.Id, Guid.NewGuid(), AppRoles.HrAdmin, ct: CancellationToken.None);

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

        var res = await Svc(uow, notif).SendBookingReminderAsync(booking.Id, Guid.NewGuid(), AppRoles.HrAdmin, ct: CancellationToken.None);

        Assert.True(res.IsSuccess);
        var rec = Assert.Single(uow.Repo<Notification>().Items);
        Assert.Equal("schedule_reminder", rec.Type);
        Assert.Equal(accId, rec.CandidateAccountId);
        Assert.Contains((accId, "ReceiveUserNotification"), notif.UserEvents);
        Assert.True(booking.Reminder24hSent);
        Assert.True(uow.SaveChangesCount >= 1);
    }

    [Fact]
    public async Task Khong_phai_chu_tin_thi_khong_nhac_lich_duoc()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif)
            .SendBookingReminderAsync(booking.Id, Guid.NewGuid(), AppRoles.Recruiter, ct: CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Empty(notif.UserEvents);
        Assert.False(booking.Reminder24hSent);
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

        var res = await Svc(uow, notif).SendBookingReminderAsync(booking.Id, Guid.NewGuid(), AppRoles.HrAdmin, ct: CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<Notification>().Items); // không có account → không tạo bell
        Assert.Empty(notif.UserEvents);
        Assert.True(booking.Reminder24hSent);          // vẫn đánh dấu đã nhắc (email best-effort)
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    //  Nhắc lịch theo TRẠNG THÁI lịch hẹn (ScheduleReminder.Resolve)
    //
    //  Trước đây nút "Nhắc lịch" gửi cùng một lá thư cho mọi tình huống — kể cả người đã báo bận
    //  hay buổi đã diễn ra xong. Bốn ca dưới đây khoá lại từng trạng thái.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ung_vien_da_bao_ban_thi_khong_nhac_duoc()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.DeclinedBooking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif)
            .SendBookingReminderAsync(booking.Id, owner, AppRoles.Recruiter, ct: CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Empty(notif.Emails);
    }

    [Fact]
    public async Task Da_qua_gio_hen_thi_khong_nhac_duoc()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        var slot = SchedulingData.Slot(job.Id, round: 1, start: DateTimeOffset.UtcNow.AddHours(-3));
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif)
            .SendBookingReminderAsync(booking.Id, owner, AppRoles.Recruiter, ct: CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Empty(notif.Emails);
    }

    [Fact]
    public async Task Chua_xac_nhan_thi_chuong_bao_di_XAC_NHAN_lich()
    {
        var accId = Guid.NewGuid();
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, accId);
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1, confirmation: "pending");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif)
            .SendBookingReminderAsync(booking.Id, owner, AppRoles.Recruiter, ct: CancellationToken.None);

        Assert.True(res.IsSuccess);
        var rec = Assert.Single(uow.Repo<Notification>().Items);
        Assert.Contains("xác nhận", rec.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Da_xac_nhan_thi_chuong_NHAC_GIO_chu_khong_doi_xac_nhan_nua()
    {
        var accId = Guid.NewGuid();
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, accId);
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(
            app.Id, slot.Id, round: 1, confirmation: "confirmed",
            respondedAt: DateTimeOffset.UtcNow.AddHours(-2));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif)
            .SendBookingReminderAsync(booking.Id, owner, AppRoles.Recruiter, ct: CancellationToken.None);

        Assert.True(res.IsSuccess);
        var rec = Assert.Single(uow.Repo<Notification>().Items);
        Assert.Contains("Nhắc", rec.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Moi_lan_nhac_sinh_mot_khoa_chong_trung_rieng()
    {
        // `notifications` có unique index (người nhận, dedup_key), mà `DedupKey` mặc định là chuỗi
        // RỖNG. Lệnh nhắc không gán khoá → lời nhắc THỨ HAI cho cùng ứng viên ném 23505, và vì
        // SaveChanges chạy SAU khi thư đã gửi nên ứng viên vẫn nhận mail còn nhân sự thấy "lỗi hệ thống".
        var accId = Guid.NewGuid();
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, accId);
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        var svc = Svc(uow, new RecordingNotificationService());

        Assert.True((await svc.SendBookingReminderAsync(booking.Id, owner, AppRoles.Recruiter, ct: CancellationToken.None)).IsSuccess);
        Assert.True((await svc.SendBookingReminderAsync(booking.Id, owner, AppRoles.Recruiter, ct: CancellationToken.None)).IsSuccess);

        var keys = uow.Repo<Notification>().Items.Select(n => n.DedupKey).ToList();
        Assert.Equal(2, keys.Count);
        Assert.All(keys, k => Assert.False(string.IsNullOrWhiteSpace(k)));
        Assert.Equal(2, keys.Distinct().Count());
    }
}
