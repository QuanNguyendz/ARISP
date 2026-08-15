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
/// Dời NHIỀU ứng viên trong một lần (<c>RescheduleBookingsAsync</c>) — thay cho việc giao diện gửi N
/// request tuần tự.
///
/// Tính chất phải giữ: ĐƯỢC ĂN CẢ NGÃ VỀ KHÔNG. Cách cũ với ca còn 1 chỗ và 3 người thì người đầu
/// lọt, hai người sau thất bại lần lượt, không có gì hoàn tác — nhân sự nhìn vào không biết ai đã
/// chuyển ai chưa.
/// </summary>
public class RescheduleBookingsBatchTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService());

    [Fact]
    public async Task Khong_du_cho_thi_khong_doi_ai_ca()
    {
        var job = SchedulingData.Job(out var owner);
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 3, booked: 3);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 0); // chỉ 2 chỗ
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldSlot).Seed(target);

        var bookings = Enumerable.Range(0, 3).Select(_ =>
        {
            var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
            uow.Seed(app);
            var b = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
            uow.Seed(b);
            return b;
        }).ToList();
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow).RescheduleBookingsAsync(
            bookings.Select(b => b.Id).ToList(), target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Contains("không còn đủ 3 chỗ", res.Error);
        Assert.Equal(0, sql.BookedCountOf(target.Id));
        Assert.Equal(3, sql.BookedCountOf(oldSlot.Id));
        Assert.All(bookings, b => Assert.Equal(oldSlot.Id, b.AvailabilitySlotId)); // không ai bị chuyển
    }

    [Fact]
    public async Task Du_cho_thi_doi_het_va_tra_dung_tung_ca_cu()
    {
        var job = SchedulingData.Job(out var owner);
        var slotA = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 2);
        var slotB = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 5, booked: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slotA).Seed(slotB).Seed(target);

        var fromA = Enumerable.Range(0, 2).Select(_ =>
        {
            var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
            uow.Seed(app);
            var b = SchedulingData.Booking(app.Id, slotA.Id, round: 1);
            uow.Seed(b);
            return b;
        }).ToList();

        var appB = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        uow.Seed(appB);
        var fromB = SchedulingData.Booking(appB.Id, slotB.Id, round: 1);
        uow.Seed(fromB);

        var sql = new SlotSqlEmulator(uow);
        var ids = fromA.Select(b => b.Id).Append(fromB.Id).ToList();

        var res = await Svc(uow).RescheduleBookingsAsync(ids, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(3, res.Value.MovedCount);
        Assert.Empty(res.Value.Failed);
        Assert.Equal(3, sql.BookedCountOf(target.Id));
        Assert.Equal(0, sql.BookedCountOf(slotA.Id)); // trả đúng 2
        Assert.Equal(0, sql.BookedCountOf(slotB.Id)); // trả đúng 1
    }

    /// <summary>Booking hỏng phải rơi ra TRƯỚC khi tính số chỗ, nếu không sẽ chiếm dư chỗ của ca đích.</summary>
    [Fact]
    public async Task Booking_khong_hop_le_bi_loai_truoc_khi_chiem_cho()
    {
        var job = SchedulingData.Job(out var owner);
        var oldSlot = SchedulingData.Slot(job.Id, round: 1, capacity: 2, booked: 1);
        var target = SchedulingData.Slot(job.Id, round: 1, capacity: 1, booked: 0); // vừa đúng 1 chỗ
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), status: "interview");
        var good = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
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
        var booking = SchedulingData.Booking(app.Id, oldSlot.Id, round: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldSlot).Seed(target).Seed(app).Seed(booking);
        var sql = new SlotSqlEmulator(uow);

        var res = await Svc(uow).RescheduleBookingsAsync(
            new[] { booking.Id, booking.Id }, target.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.MovedCount);
        Assert.Equal(1, sql.BookedCountOf(target.Id));
    }
}
