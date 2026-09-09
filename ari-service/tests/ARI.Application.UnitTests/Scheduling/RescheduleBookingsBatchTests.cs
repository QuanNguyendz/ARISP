using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Dời ứng viên sang ca khác (<c>RescheduleBookingsAsync</c>) — thay cho việc giao diện gửi N
/// request tuần tự.
///
/// Tính chất phải giữ: ĐƯỢC ĂN CẢ NGÃ VỀ KHÔNG. Cách cũ với ca còn 1 chỗ và 3 người thì người đầu
/// lọt, hai người sau thất bại lần lượt, không có gì hoàn tác — nhân sự nhìn vào không biết ai đã
/// chuyển ai chưa.
///
/// Từ ADR-067 mỗi ca chỉ nhận MỘT ứng viên, nên dồn nhiều người vào cùng một ca bị từ chối CẢ
/// LỆNH — đúng tinh thần được-ăn-cả-ngã-về-không ở trên, chỉ đổi lý do từ "hết chỗ" sang "luật".
/// </summary>
public class RescheduleBookingsBatchTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService());

    [Fact]
    public async Task Don_nhieu_nguoi_vao_mot_ca_thi_khong_doi_ai_ca()
    {
        // Mỗi ca chỉ nhận MỘT ứng viên (ADR-067) — buổi thật có Hiring Manager ngồi cùng AI.
        // Từ chối cả lệnh thay vì lặng lẽ dời một người rồi báo hai người kia hỏng.
        var job = SchedulingData.Job(out var owner);
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 3, booked: 3);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldSlot).Seed(target);

        var bookings = Enumerable.Range(0, 3).Select(_ =>
        {
            var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
            uow.Seed(app);
            var b = SchedulingData.DeclinedBooking(app.Id, oldSlot.Id, round: 1);
            uow.Seed(b);
            return b;
        }).ToList();
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow).RescheduleBookingsAsync(
            bookings.Select(b => b.Id).ToList(), target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Contains("MỘT ứng viên", res.Error);
        Assert.Equal(0, sql.BookedCountOf(target.Id));
        Assert.Equal(3, sql.BookedCountOf(oldSlot.Id));
        Assert.All(bookings, b => Assert.Equal(oldSlot.Id, b.AvailabilitySlotId)); // không ai bị chuyển
    }

    [Fact]
    public async Task Doi_mot_nguoi_thi_chiem_cho_ca_moi_va_khong_tru_lai_ca_cu()
    {
        // Ứng viên đã báo bận nên ca cũ đã ở trạng thái trả chỗ (booked = 0) — không trừ thêm lần nữa.
        var job = SchedulingData.Job(out var owner);
        var slotA = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 0);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slotA).Seed(target);

        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        uow.Seed(app);
        var booking = SchedulingData.DeclinedBooking(app.Id, slotA.Id, round: 1);
        uow.Seed(booking);

        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow).RescheduleBookingsAsync(
            new[] { booking.Id }, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.MovedCount);
        Assert.Empty(res.Value.Failed);
        Assert.Equal(1, sql.BookedCountOf(target.Id));
        Assert.Equal(0, sql.BookedCountOf(slotA.Id));
    }

    /// <summary>Booking hỏng phải rơi ra TRƯỚC khi tính số chỗ, nếu không sẽ chiếm dư chỗ của ca đích.</summary>
    [Fact]
    public async Task Booking_khong_hop_le_bi_loai_truoc_khi_chiem_cho()
    {
        var job = SchedulingData.Job(out var owner);
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 0); // vừa đúng 1 chỗ
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var good = SchedulingData.DeclinedBooking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldSlot).Seed(target).Seed(app).Seed(good);
        var sql = new SlotSqlEmulator(uow);

        var missingId = Guid.NewGuid();
        var res = await Svc(uow).RescheduleBookingsAsync(
            new[] { good.Id, missingId }, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.MovedCount);
        var failure = Assert.Single(res.Value.Failed);
        Assert.Equal(missingId, failure.BookingId);
        Assert.Equal(1, sql.BookedCountOf(target.Id)); // chỉ chiếm 1 chỗ, không phải 2
    }

    [Fact]
    public async Task Danh_sach_rong_bi_tu_choi()
    {
        var job = SchedulingData.Job(out var owner);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(target);

        var res = await Svc(uow).RescheduleBookingsAsync(
            Array.Empty<Guid>(), target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Chưa chọn ứng viên", res.Error);
    }

    [Fact]
    public async Task Trung_id_chi_tinh_mot_cho()
    {
        var job = SchedulingData.Job(out var owner);
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 0);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var booking = SchedulingData.DeclinedBooking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldSlot).Seed(target).Seed(app).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow).RescheduleBookingsAsync(
            new[] { booking.Id, booking.Id }, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.MovedCount);
        Assert.Equal(1, sql.BookedCountOf(target.Id));
    }
}
