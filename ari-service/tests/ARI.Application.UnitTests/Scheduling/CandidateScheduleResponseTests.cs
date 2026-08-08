using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Ứng viên phản hồi lịch được HR gán (ADR-048): xác nhận (<see cref="ConfirmScheduleCommandHandler"/>)
/// và báo bận/từ chối kèm lý do (<see cref="DeclineScheduleCommandHandler"/> — trả chỗ slot cho HR xếp lại).
/// </summary>
public class CandidateScheduleResponseTests
{
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private const string Email = "cand@example.io";

    private (InMemoryUnitOfWork uow, JobPosting job, ARI.Domain.Entities.Application app, InterviewBooking booking, AvailabilitySlot slot)
        Scheduled(string confirmation = "pending", int booked = 1)
    {
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "interview", email: Email);
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: booked);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1, status: "scheduled", confirmation: confirmation);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        return (uow, job, app, booking, slot);
    }

    // ---------- Confirm ----------

    [Fact]
    public async Task Confirm_sets_confirmed_and_notifies_staff()
    {
        var (uow, job, _, booking, _) = Scheduled();
        var notif = new RecordingNotificationService();

        var res = await new ConfirmScheduleCommandHandler(uow, notif)
            .Handle(new ConfirmScheduleCommand(booking.Id, _accountId, Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("confirmed", booking.ConfirmationStatus);
        Assert.NotNull(booking.RespondedAt);
        Assert.Contains(notif.GroupEvents, e => e.Group == "hr_admin" && e.EventType == "ReceiveScheduleResponse");
        Assert.Contains(notif.UserEvents, e => e.UserId == job.CreatedByUserId);
    }

    [Fact]
    public async Task Confirm_is_idempotent_when_already_confirmed()
    {
        var (uow, _, _, booking, _) = Scheduled(confirmation: "confirmed");
        var notif = new RecordingNotificationService();

        var res = await new ConfirmScheduleCommandHandler(uow, notif)
            .Handle(new ConfirmScheduleCommand(booking.Id, _accountId, Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(notif.GroupEvents); // không thông báo lại lần hai
    }

    [Fact]
    public async Task Confirm_fails_when_booking_not_scheduled()
    {
        var (uow, _, _, booking, _) = Scheduled();
        booking.Status = "declined";

        var res = await new ConfirmScheduleCommandHandler(uow, new RecordingNotificationService())
            .Handle(new ConfirmScheduleCommand(booking.Id, _accountId, Email), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không còn hiệu lực", res.Error);
    }

    [Fact]
    public async Task Confirm_by_non_owner_is_forbidden()
    {
        var (uow, _, app, booking, _) = Scheduled();
        app.CandidateAccountId = Guid.NewGuid(); // hồ sơ thuộc người khác

        var res = await new ConfirmScheduleCommandHandler(uow, new RecordingNotificationService())
            .Handle(new ConfirmScheduleCommand(booking.Id, Guid.NewGuid(), "intruder@example.io"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Confirm_unknown_booking_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await new ConfirmScheduleCommandHandler(uow, new RecordingNotificationService())
            .Handle(new ConfirmScheduleCommand(Guid.NewGuid(), _accountId, Email), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // ---------- Decline ----------

    [Fact]
    public async Task Decline_releases_slot_and_records_reason()
    {
        var (uow, _, _, booking, slot) = Scheduled(booked: 1);
        var sql = new SlotSqlEmulator(uow);
        var notif = new RecordingNotificationService();

        var res = await new DeclineScheduleCommandHandler(uow, notif)
            .Handle(new DeclineScheduleCommand(booking.Id, "Tôi bận hôm đó", _accountId, Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("declined", booking.Status);
        Assert.Equal("declined", booking.ConfirmationStatus);
        Assert.Equal("Tôi bận hôm đó", booking.DeclineReason);
        Assert.NotNull(booking.RespondedAt);
        Assert.Equal(0, sql.BookedCountOf(slot.Id)); // chỗ được trả lại cho HR xếp lại
        Assert.Contains(notif.GroupEvents, e => e.Group == "hr_admin");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ok")]
    [InlineData("  a ")]
    public async Task Decline_requires_reason_of_at_least_three_chars(string reason)
    {
        var (uow, _, _, booking, _) = Scheduled();

        var res = await new DeclineScheduleCommandHandler(uow, new RecordingNotificationService())
            .Handle(new DeclineScheduleCommand(booking.Id, reason, _accountId, Email), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("scheduled", booking.Status); // không đổi trạng thái khi lý do không hợp lệ
    }

    [Fact]
    public async Task Decline_truncates_overly_long_reason_to_500()
    {
        var (uow, _, _, booking, _) = Scheduled();
        _ = new SlotSqlEmulator(uow);

        var res = await new DeclineScheduleCommandHandler(uow, new RecordingNotificationService())
            .Handle(new DeclineScheduleCommand(booking.Id, new string('x', 600), _accountId, Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(500, booking.DeclineReason!.Length);
    }

    [Fact]
    public async Task Decline_fails_when_booking_not_scheduled()
    {
        var (uow, _, _, booking, _) = Scheduled();
        booking.Status = "declined";

        var res = await new DeclineScheduleCommandHandler(uow, new RecordingNotificationService())
            .Handle(new DeclineScheduleCommand(booking.Id, "Tôi bận", _accountId, Email), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không còn hiệu lực", res.Error);
    }

    [Fact]
    public async Task Decline_is_locked_after_confirm()
    {
        // Quyết định khoá: đã xác nhận tham dự thì không được đổi sang từ chối (một lần / lịch).
        var (uow, _, _, booking, _) = Scheduled(confirmation: "confirmed");

        var res = await new DeclineScheduleCommandHandler(uow, new RecordingNotificationService())
            .Handle(new DeclineScheduleCommand(booking.Id, "Đổi ý muốn báo bận", _accountId, Email), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã xác nhận", res.Error);
        Assert.Equal("confirmed", booking.ConfirmationStatus); // không đổi
        Assert.Equal("scheduled", booking.Status);
    }

    [Fact]
    public async Task Decline_by_non_owner_is_forbidden()
    {
        var (uow, _, app, booking, _) = Scheduled();
        app.CandidateAccountId = Guid.NewGuid();

        var res = await new DeclineScheduleCommandHandler(uow, new RecordingNotificationService())
            .Handle(new DeclineScheduleCommand(booking.Id, "Tôi bận", Guid.NewGuid(), "intruder@example.io"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ---------- Dismiss (ứng viên ẩn lịch đã bị huỷ/từ chối khỏi danh sách) ----------

    [Fact]
    public async Task Dismiss_marks_declined_booking_hidden()
    {
        var (uow, _, _, booking, _) = Scheduled();
        booking.Status = "declined";

        var res = await new DismissDeclinedScheduleCommandHandler(uow)
            .Handle(new DismissDeclinedScheduleCommand(booking.Id, _accountId, Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(booking.CandidateDismissedAt); // đã ẩn khỏi danh sách ứng viên
    }

    [Fact]
    public async Task Dismiss_rejects_active_scheduled_booking()
    {
        var (uow, _, _, booking, _) = Scheduled(); // Status = "scheduled" (còn hiệu lực)

        var res = await new DismissDeclinedScheduleCommandHandler(uow)
            .Handle(new DismissDeclinedScheduleCommand(booking.Id, _accountId, Email), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Null(booking.CandidateDismissedAt); // không ẩn lịch còn hiệu lực
    }

    [Fact]
    public async Task Dismiss_is_idempotent_when_already_hidden()
    {
        var (uow, _, _, booking, _) = Scheduled();
        booking.Status = "declined";
        var firstAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        booking.CandidateDismissedAt = firstAt;

        var res = await new DismissDeclinedScheduleCommandHandler(uow)
            .Handle(new DismissDeclinedScheduleCommand(booking.Id, _accountId, Email), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(firstAt, booking.CandidateDismissedAt); // không ghi đè thời điểm ẩn lần đầu
    }

    [Fact]
    public async Task Dismiss_by_non_owner_is_forbidden()
    {
        var (uow, _, app, booking, _) = Scheduled();
        booking.Status = "declined";
        app.CandidateAccountId = Guid.NewGuid();

        var res = await new DismissDeclinedScheduleCommandHandler(uow)
            .Handle(new DismissDeclinedScheduleCommand(booking.Id, Guid.NewGuid(), "intruder@example.io"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }
}
