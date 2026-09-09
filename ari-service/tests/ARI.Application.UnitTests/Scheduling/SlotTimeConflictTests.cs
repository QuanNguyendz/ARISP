using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Ca phỏng vấn không được CHỒNG GIỜ nhau (ADR-067), và ca chưa ai đặt thì sửa được giờ.
///
/// Vì sao chồng giờ là lỗi: buổi thật có Hiring Manager ngồi cùng AI, mà mỗi tin chỉ có MỘT Hiring
/// Manager — hai ca trùng giờ nghĩa là bắt họ ở hai phòng cùng lúc. Trước đây màn cấu hình lịch cho
/// tạo thoải mái, người dùng bắt được hai ca `22:00–22:30` và `22:20–22:30` nằm cạnh nhau.
/// </summary>
public class SlotTimeConflictTests
{
    private readonly Guid _ownerId = Guid.NewGuid();

    private Task<Result<AvailabilitySlotResponse>> Create(
        InMemoryUnitOfWork uow, CreateSlotRequest req) =>
        new CreateSlotCommandHandler(uow)
            .Handle(new CreateSlotCommand(req, _ownerId, AppRoles.Recruiter), CancellationToken.None);

    private Task<Result<AvailabilitySlotResponse>> UpdateTime(
        InMemoryUnitOfWork uow, Guid slotId, DateTimeOffset start, DateTimeOffset end) =>
        new UpdateSlotTimeCommandHandler(uow)
            .Handle(new UpdateSlotTimeCommand(slotId, start, end, _ownerId, AppRoles.Recruiter),
                CancellationToken.None);

    // ---------- Tạo ca ----------

    [Fact]
    public async Task Ca_chong_gio_trong_cung_vong_bi_chan()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(SchedulingData.Slot(job.Id, round: 1, start: start)); // start → start+1h

        var res = await Create(uow, SchedulingData.SlotRequest(
            job.Id, round: 1, start: start.AddMinutes(20), end: start.AddMinutes(50)));

        Assert.True(res.IsFailure);
        Assert.Contains("chồng lên ca đã có", res.Error);
    }

    [Fact]
    public async Task Ca_chong_gio_o_VONG_KHAC_cung_tin_cung_bi_chan()
    {
        // Vòng khác không cứu được: vẫn là một Hiring Manager, vẫn một khoảng thời gian.
        var job = SchedulingData.Job(owner: _ownerId);
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(SchedulingData.Slot(job.Id, round: 1, start: start));

        var res = await Create(uow, SchedulingData.SlotRequest(
            job.Id, round: 2, start: start.AddMinutes(30), end: start.AddMinutes(90)));

        Assert.True(res.IsFailure);
        Assert.Contains("chồng lên ca đã có", res.Error);
    }

    [Fact]
    public async Task Ca_noi_duoi_nhau_thi_van_tao_duoc()
    {
        // 14:00–15:00 rồi 15:00–16:00 là cách xếp ca liên tiếp bình thường, không phải chồng lấn.
        var job = SchedulingData.Job(owner: _ownerId);
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(SchedulingData.Slot(job.Id, round: 1, start: start));

        var res = await Create(uow, SchedulingData.SlotRequest(
            job.Id, round: 1, start: start.AddHours(1), end: start.AddHours(2)));

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Ca_cua_TIN_KHAC_khong_bi_coi_la_chong()
    {
        // Ràng buộc ở đây là "cùng một tin"; hai tin khác nhau có thể do hai Hiring Manager phụ
        // trách. Trùng giờ mà CÙNG một HM thì bị chặn ở bước GÁN ca (luật 4).
        var jobA = SchedulingData.Job(owner: _ownerId);
        var jobB = SchedulingData.Job(owner: _ownerId);
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var uow = new InMemoryUnitOfWork().Seed(jobA).Seed(jobB)
            .Seed(SchedulingData.Slot(jobA.Id, round: 1, start: start));

        var res = await Create(uow, SchedulingData.SlotRequest(jobB.Id, round: 1, start: start));

        Assert.True(res.IsSuccess);
    }

    // ---------- Sửa giờ ----------

    [Fact]
    public async Task Sua_gio_ca_chua_ai_dat_thi_duoc()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddDays(2));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var newStart = DateTimeOffset.UtcNow.AddDays(3);
        var res = await UpdateTime(uow, slot.Id, newStart, newStart.AddHours(1));

        Assert.True(res.IsSuccess);
        Assert.Equal(newStart, slot.StartTime);
    }

    [Fact]
    public async Task Sua_gio_ca_DA_CO_nguoi_giu_cho_bi_chan()
    {
        // Đổi giờ một ca đã hẹn là đổi lịch hẹn của người khác mà không báo họ — đường đúng cho việc
        // đó là "dời lịch", nơi có gửi thông báo.
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddDays(2), booked: 1);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.Booking(app.Id, slot.Id));

        var newStart = DateTimeOffset.UtcNow.AddDays(3);
        var res = await UpdateTime(uow, slot.Id, newStart, newStart.AddHours(1));

        Assert.True(res.IsFailure);
        Assert.Contains("dời lịch", res.Error);
    }

    [Fact]
    public async Task Sua_gio_thanh_chong_ca_khac_bi_chan()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var slot = SchedulingData.Slot(job.Id, start: start);
        var other = SchedulingData.Slot(job.Id, start: start.AddHours(3));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(other);

        var res = await UpdateTime(uow, slot.Id, start.AddHours(3).AddMinutes(30), start.AddHours(5));

        Assert.True(res.IsFailure);
        Assert.Contains("chồng lên ca đã có", res.Error);
    }

    [Fact]
    public async Task Sua_gio_ve_qua_khu_bi_chan()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddDays(2));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var past = DateTimeOffset.UtcNow.AddDays(-1);
        var res = await UpdateTime(uow, slot.Id, past, past.AddHours(1));

        Assert.True(res.IsFailure);
        Assert.Contains("tương lai", res.Error);
    }

    [Fact]
    public async Task Nguoi_ngoai_khong_sua_duoc_gio_ca()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddDays(2));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var newStart = DateTimeOffset.UtcNow.AddDays(3);
        var res = await UpdateTime(uow, slot.Id, newStart, newStart.AddHours(1));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ---------- Danh sách ca kèm ứng viên đã đặt ----------

    [Fact]
    public async Task Danh_sach_ca_tra_kem_ung_vien_dang_giu_cho()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddDays(2), booked: 1);
        var app = SchedulingData.Application(job.Id, Guid.NewGuid(), email: "cand@example.io");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.Booking(app.Id, slot.Id));

        var res = await new GetAvailabilitySlotsQueryHandler(uow)
            .Handle(new GetAvailabilitySlotsQuery(job.Id, null, _ownerId, AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsSuccess);
        var dto = Assert.Single(res.Value);
        var booking = Assert.Single(dto.Bookings);
        Assert.Equal(app.Id, booking.ApplicationId);
        Assert.Equal("cand@example.io", booking.CandidateEmail);
    }

    [Fact]
    public async Task Ca_bi_huy_khong_tinh_la_dang_giu_cho()
    {
        // Chỉ booking `scheduled` mới thật sự giữ chỗ — vị từ duy nhất của ADR-058.
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, start: DateTimeOffset.UtcNow.AddDays(2));
        var app = SchedulingData.Application(job.Id, Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot).Seed(app)
            .Seed(SchedulingData.DeclinedBooking(app.Id, slot.Id));

        var res = await new GetAvailabilitySlotsQueryHandler(uow)
            .Handle(new GetAvailabilitySlotsQuery(job.Id, null, _ownerId, AppRoles.Recruiter),
                CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value.Single().Bookings);
    }
}
