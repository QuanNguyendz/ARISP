using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Luồng CHÍNH: HR gán cứng 1 khung giờ cho ứng viên (<see cref="AssignSlotCommandHandler"/>, ADR-048).
/// Chốt: tạo booking scheduled/pending + chốt chỗ nguyên tử (chống overbooking), screening→interview,
/// đánh dấu invite, thông báo ứng viên (bell + realtime), liên kết xếp-lại sau decline, bù trừ khi lưu lỗi;
/// cùng các cổng chặn: CV chưa duyệt, đã có lịch vòng, sai job/vòng, quá khứ, phân quyền, not-found.
/// </summary>
public class AssignSlotCommandHandlerTests
{
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    private Task<Result<AssignSlotResultDto>> Run(
        InMemoryUnitOfWork uow, RecordingNotificationService notif, AssignSlotCommand cmd)
        => new AssignSlotCommandHandler(uow, notif, EmptyConfig).Handle(cmd, CancellationToken.None);

    private AssignSlotCommand Cmd(Guid appId, Guid slotId, int round = 1, Guid? user = null, string? role = null)
        => new(appId, slotId, round, user ?? _staffId, role ?? AppRoles.Recruiter);

    // ---------- Thành công ----------

    [Fact]
    public async Task Assign_books_slot_and_moves_screening_to_interview()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 0);
        uow.Seed(job).Seed(app).Seed(slot);
        var sql = new SlotSqlEmulator(uow);

        var res = await Run(uow, notif, Cmd(app.Id, slot.Id));

        Assert.True(res.IsSuccess);
        var booking = Assert.Single(uow.Repo<InterviewBooking>().Items);
        Assert.Equal("scheduled", booking.Status);
        Assert.Equal("pending", booking.ConfirmationStatus);
        Assert.Equal(1, booking.RoundNumber);
        Assert.Equal(booking.Id, res.Value.BookingId);
        Assert.Equal("interview", app.Status);              // screening → interview
        Assert.Equal(1, sql.BookedCountOf(slot.Id));        // đã chốt 1 chỗ
        Assert.Equal(1, res.Value.Slot.BookedCount);        // DTO không đếm kép
    }

    [Fact]
    public async Task Assign_notifies_candidate_with_bell_and_realtime()
    {
        var uow = new InMemoryUnitOfWork();
        var notif = new RecordingNotificationService();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        var slot = SchedulingData.Slot(job.Id);
        uow.Seed(job).Seed(app).Seed(slot);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, notif, Cmd(app.Id, slot.Id));

        var bell = Assert.Single(uow.Repo<Notification>().Items);
        Assert.Equal(_accountId, bell.CandidateAccountId);
        Assert.Equal("schedule", bell.Type);
        Assert.Equal($"schedule_assigned:{res.Value.BookingId}", bell.DedupKey);
        Assert.Contains(notif.UserEvents, e => e.UserId == _accountId && e.EventType == "ReceiveUserNotification");
    }

    [Fact]
    public async Task Assign_marks_pending_invite_scheduled()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var invite = new InterviewInvite { ApplicationId = app.Id, RoundNumber = 1, ScheduledAt = null };
        uow.Seed(job).Seed(app).Seed(slot).Seed(invite);
        _ = new SlotSqlEmulator(uow);

        await Run(uow, new RecordingNotificationService(), Cmd(app.Id, slot.Id));

        Assert.NotNull(invite.ScheduledAt);
    }

    [Fact]
    public async Task Round_two_assignment_keeps_interview_status()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 2);
        uow.Seed(job).Seed(app).Seed(slot);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, slot.Id, round: 2));

        Assert.True(res.IsSuccess);
        Assert.Equal("interview", app.Status);
        Assert.Single(uow.Repo<InterviewBooking>().Items);
    }

    [Fact]
    public async Task Reassign_after_decline_links_to_prior_declined_booking()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1);
        var newSlot = SchedulingData.Slot(job.Id, round: 1);
        var declined = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1, status: "declined",
            confirmation: "declined", respondedAt: DateTimeOffset.UtcNow.AddHours(-1));
        uow.Seed(job).Seed(app).Seed(oldSlot, newSlot).Seed(declined);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, newSlot.Id));

        Assert.True(res.IsSuccess);
        var created = uow.Repo<InterviewBooking>().Items.Single(b => b.Status == "scheduled");
        Assert.Equal(declined.Id, created.RescheduledFromId);
    }

    // ---------- Chống overbooking / trùng lịch ----------

    [Fact]
    public async Task Full_slot_is_rejected_and_no_booking_created()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        var slot = SchedulingData.Slot(job.Id, capacity: 1, booked: 1); // đã đầy
        uow.Seed(job).Seed(app).Seed(slot);
        var sql = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, slot.Id));

        Assert.True(res.IsFailure);
        Assert.Contains("đầy", res.Error);
        Assert.Empty(uow.Repo<InterviewBooking>().Items);
        Assert.Equal(1, sql.BookedCountOf(slot.Id)); // không vượt sức chứa
    }

    [Fact]
    public async Task Already_scheduled_round_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 1);
        var existing = SchedulingData.Booking(app.Id, Guid.NewGuid(), round: 1, status: "scheduled");
        uow.Seed(job).Seed(app).Seed(slot).Seed(existing);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, slot.Id));

        Assert.True(res.IsFailure);
        Assert.Contains("đã có lịch", res.Error);
        Assert.Single(uow.Repo<InterviewBooking>().Items); // không thêm booking mới
    }

    [Fact]
    public async Task Save_failure_compensates_the_reserved_seat()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        var slot = SchedulingData.Slot(job.Id, capacity: 1, booked: 0);
        uow.Seed(job).Seed(app).Seed(slot);
        var sql = new SlotSqlEmulator(uow);
        uow.ThrowOnSaveChanges = true; // mô phỏng trùng vòng do double-click (unique index)

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, slot.Id));

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể hoàn tất", res.Error);
        Assert.Equal(0, sql.BookedCountOf(slot.Id)); // chỗ đã chiếm được nhả lại
    }

    // ---------- Cổng chặn ----------

    [Theory]
    [InlineData("cv_submitted")]
    [InlineData("invited")]
    [InlineData("cv_rejected")]
    [InlineData("not_pass")]
    [InlineData("rejected")]
    public async Task Cannot_assign_before_cv_passed(string status)
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: status);
        var slot = SchedulingData.Slot(job.Id);
        uow.Seed(job).Seed(app).Seed(slot);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, slot.Id));

        Assert.True(res.IsFailure);
        Assert.Contains("duyệt CV", res.Error);
        Assert.Empty(uow.Repo<InterviewBooking>().Items);
    }

    [Fact]
    public async Task Slot_of_wrong_round_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 2);
        uow.Seed(job).Seed(app).Seed(slot);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, slot.Id, round: 1));

        Assert.True(res.IsFailure);
        Assert.Contains("không thuộc", res.Error);
    }

    [Fact]
    public async Task Past_slot_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        var slot = SchedulingData.Slot(job.Id, round: 1, start: SchedulingData.Past);
        uow.Seed(job).Seed(app).Seed(slot);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, slot.Id));

        Assert.True(res.IsFailure);
        Assert.Contains("quá khứ", res.Error);
    }

    [Fact]
    public async Task Non_owner_staff_is_forbidden()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: Guid.NewGuid()); // chủ tin khác
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        var slot = SchedulingData.Slot(job.Id);
        uow.Seed(job).Seed(app).Seed(slot);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(),
            Cmd(app.Id, slot.Id, user: _staffId, role: AppRoles.Recruiter));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Admin_can_assign_for_any_job()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        var slot = SchedulingData.Slot(job.Id);
        uow.Seed(job).Seed(app).Seed(slot);
        _ = new SlotSqlEmulator(uow);

        var res = await Run(uow, new RecordingNotificationService(),
            Cmd(app.Id, slot.Id, user: Guid.NewGuid(), role: AppRoles.HrAdmin));

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Unknown_application_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow, new RecordingNotificationService(), Cmd(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Unknown_slot_returns_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var job = SchedulingData.Job(owner: _staffId);
        var app = SchedulingData.Application(job.Id, _accountId, status: "screening");
        uow.Seed(job).Seed(app);

        var res = await Run(uow, new RecordingNotificationService(), Cmd(app.Id, Guid.NewGuid()));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }
}
