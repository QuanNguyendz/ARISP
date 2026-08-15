using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Dời lịch phỏng vấn 1 người (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>RescheduleBookingAsync</c>, test-plan B4).
///
/// Ngoài các guard cũ, khoá thêm ba điều từng sai:
///  - PHẢI kiểm quyền theo chủ tin (trước đây không kiểm gì).
///  - KHÔNG được dời sang ca của tin khác (trước đây chỉ so vòng, mà vòng 1 thì tin nào cũng có).
///  - Dời một booking ĐÃ TỪ CHỐI thì KHÔNG trả chỗ lần nữa (chỗ đã được trả lúc từ chối rồi).
/// </summary>
public class RescheduleBookingTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => InterviewServiceFactory.Create(uow, notif);

    // ---------- Quyền ----------

    [Fact]
    public async Task Khong_phai_chu_tin_thi_bi_tu_choi()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, Guid.NewGuid(), AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Equal(0, sql.BookedCountOf(target.Id)); // không chiếm chỗ khi bị chặn quyền
    }

    [Fact]
    public async Task HrAdmin_duoc_phep_du_khong_phai_chu_tin()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, Guid.NewGuid(), AppRoles.HrAdmin, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, sql.BookedCountOf(target.Id));
    }

    // ---------- Guards ----------

    [Fact]
    public async Task Khong_tim_thay_booking_thi_bao_loi()
    {
        var job = SchedulingData.Job(out var owner);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(target);
        _ = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            Guid.NewGuid(), target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy lịch phỏng vấn", res.Error);
    }

    [Fact]
    public async Task Doi_sang_chinh_ca_dang_o_bi_tu_choi()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 1);
        var booking = SchedulingData.Booking(app.Id, slot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(slot).Seed(booking);
        _ = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, slot.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã nằm trong ca này", res.Error);
    }

    [Fact]
    public async Task Khong_tim_thay_ca_dich_tra_ve_NotFound()
    {
        var job = SchedulingData.Job(out var owner);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            Guid.NewGuid(), Guid.NewGuid(), owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    /// <summary>Lỗ hổng nghiêm trọng nhất: vòng 1 tồn tại ở MỌI tin nên chỉ so vòng là chưa đủ.</summary>
    [Fact]
    public async Task Khong_the_doi_sang_ca_cua_tin_khac()
    {
        var jobA = SchedulingData.Job(out var ownerA);
        var jobB = SchedulingData.Job(ownerA);   // cùng chủ, nhưng là TIN KHÁC
        var app = SchedulingData.Application(jobA.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(jobA.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(jobB.Id, round: 1, capacity: 5); // cùng vòng, khác tin
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(jobA).Seed(jobB).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, ownerA, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không thuộc tin tuyển dụng", res.Error);
        Assert.Equal(0, sql.BookedCountOf(target.Id));
        Assert.Equal(oldSlot.Id, booking.AvailabilitySlotId); // không đụng gì tới booking
    }

    [Fact]
    public async Task Ca_dich_khac_vong_bi_tu_choi()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 2, capacity: 5);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("thuộc vòng thi khác", res.Error);
        Assert.Equal(0, sql.BookedCountOf(target.Id));
    }

    [Fact]
    public async Task Ca_dich_o_qua_khu_bi_tu_choi()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, start: SchedulingData.Past);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        _ = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã diễn ra trong quá khứ", res.Error);
    }

    [Fact]
    public async Task Ca_dich_day_thi_that_bai_va_khong_chiem_cho()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1); // đã đầy
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Equal(1, sql.BookedCountOf(target.Id));   // không đổi
        Assert.Equal(1, sql.BookedCountOf(oldSlot.Id));  // ca cũ cũng không bị trả chỗ oan
    }

    [Fact]
    public async Task Ho_so_da_bi_loai_thi_khong_doi_lich_duoc()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "not_pass");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 5);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1,
            status: BookingStatus.Cancelled, confirmation: "declined", declinedBy: BookingDeclinedBy.Staff);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("đã bị loại", res.Error);
        Assert.Equal(0, sql.BookedCountOf(target.Id));
    }

    // ---------- Happy path ----------

    [Fact]
    public async Task Doi_lich_chuyen_cho_reset_booking_va_bao_ung_vien()
    {
        var accId = Guid.NewGuid();
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, accId, status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, sql.BookedCountOf(oldSlot.Id));
        Assert.Equal(1, sql.BookedCountOf(target.Id));
        Assert.Equal(target.Id, booking.AvailabilitySlotId);
        Assert.Equal(BookingStatus.Scheduled, booking.Status);
        Assert.Equal("pending", booking.ConfirmationStatus);

        var rec = Assert.Single(uow.Repo<Notification>().Items);
        Assert.Equal("schedule_rescheduled", rec.Type);
        Assert.Equal(accId, rec.CandidateAccountId);
        Assert.Contains((accId, "ReceiveUserNotification"), notif.UserEvents);
    }

    /// <summary>
    /// Đúng công dụng chính của nút "Dời lịch": xếp lại cho người đã báo bận. Chỗ của họ đã được
    /// trả lúc từ chối, nên KHÔNG được trừ lần thứ hai — đây là một trong hai nguồn làm lệch
    /// `booked_count` mà migration đối soát phải đi dọn.
    /// </summary>
    [Fact]
    public async Task Doi_lich_ung_vien_da_tu_choi_khong_nha_cho_hai_lan()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0); // chỗ đã trả rồi
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1,
            status: BookingStatus.Declined, confirmation: "declined",
            respondedAt: DateTimeOffset.UtcNow.AddDays(-1), declinedBy: BookingDeclinedBy.Candidate);
        booking.DeclineReason = "Bận đột xuất";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(0, sql.BookedCountOf(oldSlot.Id)); // KHÔNG bị trừ thành số âm/kẹp về 0 lần nữa
        Assert.Equal(1, sql.BookedCountOf(target.Id));
    }

    [Fact]
    public async Task Xoa_sach_dau_vet_tu_choi_khi_xep_lai()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 2);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1,
            status: BookingStatus.Declined, confirmation: "declined",
            respondedAt: DateTimeOffset.UtcNow.AddDays(-1), declinedBy: BookingDeclinedBy.System);
        booking.DeclineReason = "[Hệ thống] Ứng viên không xác nhận lịch trong thời hạn.";
        booking.CandidateDismissedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        _ = new SlotSqlEmulator(uow);

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(BookingStatus.Scheduled, booking.Status);
        Assert.Equal("pending", booking.ConfirmationStatus);
        Assert.Null(booking.DeclineReason);
        Assert.Null(booking.DeclinedBy);
        Assert.Null(booking.RespondedAt);
        Assert.Null(booking.CandidateDismissedAt); // hiện lại trong danh sách của ứng viên
    }

    [Fact]
    public async Task Luu_that_bai_thi_tra_lai_cho_da_chiem()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        var sql = new SlotSqlEmulator(uow);
        uow.ThrowOnSaveChanges = true;

        var res = await Svc(uow, new()).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(0, sql.BookedCountOf(target.Id));  // đã bù trừ
        Assert.Equal(1, sql.BookedCountOf(oldSlot.Id)); // ca cũ chưa bị đụng tới
    }

    [Fact]
    public async Task Ho_so_khong_co_tai_khoan_van_doi_duoc_nhung_khong_gui_thong_bao()
    {
        var job = SchedulingData.Job(out var owner);
        var app = SchedulingData.Application(job.Id, accountId: null, status: "interview");
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0);
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(oldSlot).Seed(target).Seed(booking);
        _ = new SlotSqlEmulator(uow);
        var notif = new RecordingNotificationService();

        var res = await Svc(uow, notif).RescheduleBookingAsync(
            booking.Id, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(target.Id, booking.AvailabilitySlotId);
        Assert.Empty(uow.Repo<Notification>().Items);
        Assert.Empty(notif.UserEvents);
    }
}
