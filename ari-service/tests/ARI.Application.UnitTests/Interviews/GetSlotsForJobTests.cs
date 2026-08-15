using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.UnitTests.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.Interviews;

/// <summary>
/// Danh sách ca phỏng vấn của một job cho nhân sự (<see cref="ARI.Application.Services.InterviewService"/>
/// <c>GetSlotsForJobAsync</c>, test-plan B25).
///
/// Trọng tâm: <c>BookedCount</c> là SỐ CHỖ ĐANG BỊ CHIẾM (booking <c>status = 'scheduled'</c>), không
/// phải tổng số dòng booking. Người báo bận / quá hạn / bị loại đều đã trả chỗ nên không được tính —
/// nếu tính, giao diện sẽ hiện phân số vô nghĩa kiểu "4/3 ứng viên" trong khi server vẫn còn chỗ trống.
/// Kèm kiểm quyền: chỉ chủ tin hoặc admin đọc được.
/// </summary>
public class GetSlotsForJobTests
{
    private static ARI.Application.Services.InterviewService Svc(InMemoryUnitOfWork uow)
        => InterviewServiceFactory.Create(uow, new RecordingNotificationService());

    [Fact]
    public async Task Khong_tim_thay_tin_tra_ve_NotFound()
    {
        var res = await Svc(new InMemoryUnitOfWork())
            .GetSlotsForJobAsync(Guid.NewGuid(), Guid.NewGuid(), AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Khong_phai_chu_tin_thi_bi_tu_choi()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Svc(uow).GetSlotsForJobAsync(job.Id, Guid.NewGuid(), AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Khong_co_ca_tra_ve_rong()
    {
        var owner = Guid.NewGuid();
        var job = SchedulingData.Job(owner);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Svc(uow).GetSlotsForJobAsync(job.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task Dem_tach_bach_theo_trang_thai_va_danh_dau_ca_da_qua()
    {
        var owner = Guid.NewGuid();
        var job = SchedulingData.Job(owner);
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 5, start: SchedulingData.Past); // đã qua
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot)
            .Seed(SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "confirmed"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "pending"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Declined, confirmation: "declined"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Cancelled, confirmation: "declined"));

        var res = await Svc(uow).GetSlotsForJobAsync(job.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        var dto = Assert.Single(res.Value);
        Assert.Equal(slot.Id, dto.SlotId);
        Assert.Equal(2, dto.BookedCount);        // chỉ 2 booking 'scheduled' đang giữ chỗ
        Assert.Equal(1, dto.ConfirmedCount);
        Assert.Equal(1, dto.PendingCount);
        Assert.Equal(1, dto.DeclinedCount);      // đã trả chỗ
        Assert.Equal(1, dto.CancelledCount);     // đã trả chỗ
        Assert.Equal(4, dto.TotalBookingRows);   // con số mà giao diện cũ từng hiện trong phân số
        Assert.Equal(3, dto.SeatsAvailable);     // 5 - 2
        Assert.False(dto.IsOverCapacity);
        Assert.True(dto.IsPast);
    }

    /// <summary>Đúng kịch bản trong ảnh chụp màn hình người dùng gửi: "4/3 ứng viên · 2 xác nhận · 2 từ chối".</summary>
    [Fact]
    public async Task Ung_vien_da_tu_choi_khong_chiem_cho()
    {
        var owner = Guid.NewGuid();
        var job = SchedulingData.Job(owner);
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 3);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot)
            .Seed(SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "confirmed"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "confirmed"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Declined, confirmation: "declined"),
                  SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Declined, confirmation: "declined"));

        var res = await Svc(uow).GetSlotsForJobAsync(job.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        var dto = Assert.Single(res.Value);
        Assert.Equal(2, dto.BookedCount);        // KHÔNG phải 4
        Assert.Equal(1, dto.SeatsAvailable);     // vẫn còn nhận thêm được 1 người
        Assert.False(dto.IsOverCapacity);
    }

    [Fact]
    public async Task Vuot_suc_chua_thi_cho_trong_bang_0_chu_khong_am()
    {
        var owner = Guid.NewGuid();
        var job = SchedulingData.Job(owner);
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 2);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot)
            .Seed(Enumerable.Range(0, 3)
                .Select(_ => SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled))
                .ToArray());

        var res = await Svc(uow).GetSlotsForJobAsync(job.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        var dto = Assert.Single(res.Value);
        Assert.Equal(3, dto.BookedCount);
        Assert.Equal(0, dto.SeatsAvailable);
        Assert.True(dto.IsOverCapacity);
    }

    /// <summary>
    /// Vị từ chiếm chỗ ở đây phải khớp từng chữ với migration ReconcileSlotBookedCount, nếu không
    /// thì màn hình và cột `booked_count` lại nói hai chuyện khác nhau như trước.
    /// </summary>
    [Fact]
    public async Task Bat_bien_so_cho_bang_so_booking_scheduled()
    {
        var owner = Guid.NewGuid();
        var job = SchedulingData.Job(owner);
        var slot = SchedulingData.Slot(job.Id, round: 1, capacity: 10);
        var bookings = new[]
        {
            SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "pending"),
            SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "confirmed"),
            SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Declined, confirmation: "declined"),
            SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Cancelled, confirmation: "declined"),
            SchedulingData.Booking(Guid.NewGuid(), slot.Id, status: BookingStatus.Scheduled, confirmation: "pending"),
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(bookings);

        var res = await Svc(uow).GetSlotsForJobAsync(job.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        var expected = bookings.Count(b => b.Status == BookingStatus.Scheduled);
        Assert.Equal(expected, Assert.Single(res.Value).BookedCount);
    }
}
