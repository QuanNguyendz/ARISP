using System;
using System.Collections.Generic;
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
/// Nhân sự quản lý kho khung giờ phỏng vấn thật (UC-39/40/41, StaffScheduling): xem danh sách slot theo job/vòng,
/// tạo khung giờ (validate tương lai/sức chứa/vòng), xoá khung giờ (chặn khi đã có người đặt) và sửa sức chứa
/// (không nhỏ hơn số đã đặt). Mọi thao tác qua cổng phân quyền chủ tin / admin (<c>SchedulingSupport.CanManage</c>).
/// </summary>
public class StaffSlotTests
{
    private readonly Guid _ownerId = Guid.NewGuid();

    // ---------- GetAvailabilitySlotsQueryHandler ----------

    private Task<Result<List<AvailabilitySlotResponse>>> RunGet(
        InMemoryUnitOfWork uow, Guid jobId, int? round = null, Guid? user = null, string? role = null)
        => new GetAvailabilitySlotsQueryHandler(uow)
            .Handle(new GetAvailabilitySlotsQuery(jobId, round, user ?? _ownerId, role ?? AppRoles.Recruiter), CancellationToken.None);

    [Fact]
    public async Task GetSlots_empty_job_id_fails()
    {
        var res = await RunGet(new InMemoryUnitOfWork(), Guid.Empty);

        Assert.True(res.IsFailure);
        Assert.Contains("jobPostingId", res.Error);
    }

    [Fact]
    public async Task GetSlots_job_not_found_returns_not_found()
    {
        var res = await RunGet(new InMemoryUnitOfWork(), Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task GetSlots_non_owner_recruiter_is_forbidden()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid()); // chủ tin khác
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(SchedulingData.Slot(job.Id));

        var res = await RunGet(uow, job.Id, user: _ownerId, role: AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task GetSlots_owner_gets_slots_ordered_by_start_time()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var later = SchedulingData.Slot(job.Id, start: SchedulingData.Future.AddDays(2));
        var sooner = SchedulingData.Slot(job.Id, start: SchedulingData.Future);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(later, sooner);

        var res = await RunGet(uow, job.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value!.Count);
        Assert.Equal(sooner.Id, res.Value[0].Id); // sắp theo StartTime tăng dần
    }

    [Fact]
    public async Task GetSlots_filters_by_round()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(SchedulingData.Slot(job.Id, round: 1), SchedulingData.Slot(job.Id, round: 2));

        var res = await RunGet(uow, job.Id, round: 2);

        Assert.Equal(2, Assert.Single(res.Value!).RoundNumber);
    }

    [Fact]
    public async Task GetSlots_admin_can_view_any_job()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(SchedulingData.Slot(job.Id));

        var res = await RunGet(uow, job.Id, user: Guid.NewGuid(), role: AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
    }

    // ---------- CreateSlotCommandHandler ----------

    private Task<Result<AvailabilitySlotResponse>> RunCreate(
        InMemoryUnitOfWork uow, CreateSlotRequest req, Guid? user = null, string? role = null)
        => new CreateSlotCommandHandler(uow)
            .Handle(new CreateSlotCommand(req, user ?? _ownerId, role ?? AppRoles.Recruiter), CancellationToken.None);

    [Fact]
    public async Task Create_empty_job_id_fails()
    {
        var res = await RunCreate(new InMemoryUnitOfWork(), SchedulingData.SlotRequest(Guid.Empty));

        Assert.True(res.IsFailure);
        Assert.Contains("jobPostingId", res.Error);
    }

    [Fact]
    public async Task Create_end_before_start_fails()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var req = SchedulingData.SlotRequest(job.Id, start: SchedulingData.Future, end: SchedulingData.Future.AddHours(-1));
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RunCreate(uow, req);

        Assert.True(res.IsFailure);
        Assert.Contains("Giờ kết thúc", res.Error);
    }

    [Fact]
    public async Task Create_start_in_past_fails()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var req = SchedulingData.SlotRequest(job.Id, start: SchedulingData.Past, end: SchedulingData.Past.AddHours(1));
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RunCreate(uow, req);

        Assert.True(res.IsFailure);
        Assert.Contains("tương lai", res.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Create_capacity_khac_mot_deu_bi_chan(int capacity)
    {
        // ADR-067: một ca = một ứng viên (buổi thật có Hiring Manager ngồi cùng AI). Chặn ở CỔNG
        // TẠO chứ không âm thầm ghi đè về 1 — một ô "sức chứa 3" nhận vào rồi bị bỏ qua trông vẫn
        // như đang có tác dụng.
        var job = SchedulingData.Job(owner: _ownerId);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RunCreate(uow, SchedulingData.SlotRequest(job.Id, capacity: capacity));

        Assert.True(res.IsFailure);
        Assert.Contains("MỘT ứng viên", res.Error);
    }

    [Fact]
    public async Task Create_round_below_one_fails()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RunCreate(uow, SchedulingData.SlotRequest(job.Id, round: 0));

        Assert.True(res.IsFailure);
        Assert.Contains("RoundNumber", res.Error);
    }

    [Fact]
    public async Task Create_job_not_found_returns_not_found()
    {
        // Request hợp lệ mọi mặt nhưng job không tồn tại → NotFound (validate xong mới kiểm tra quyền/tồn tại).
        var res = await RunCreate(new InMemoryUnitOfWork(), SchedulingData.SlotRequest(Guid.NewGuid()));

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Create_non_owner_recruiter_is_forbidden()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RunCreate(uow, SchedulingData.SlotRequest(job.Id), user: _ownerId, role: AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Create_owner_persists_slot_with_zero_booked()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RunCreate(uow, SchedulingData.SlotRequest(job.Id, round: 2, capacity: 1));

        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value!.BookedCount);
        Assert.Equal(1, res.Value.Capacity);
        Assert.Equal(2, res.Value.RoundNumber);
        var stored = Assert.Single(uow.Repo<AvailabilitySlot>().Items);
        Assert.Equal(0, stored.BookedCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Create_defaults_timezone_when_blank()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RunCreate(uow, SchedulingData.SlotRequest(job.Id, tz: "   "));

        Assert.Equal("Asia/Ho_Chi_Minh", res.Value!.Timezone);
    }

    // ---------- DeleteSlotCommandHandler ----------

    private Task<Result> RunDelete(InMemoryUnitOfWork uow, Guid slotId, Guid? user = null, string? role = null)
        => new DeleteSlotCommandHandler(uow)
            .Handle(new DeleteSlotCommand(slotId, user ?? _ownerId, role ?? AppRoles.Recruiter), CancellationToken.None);

    [Fact]
    public async Task Delete_slot_not_found_returns_not_found()
    {
        var res = await RunDelete(new InMemoryUnitOfWork(), Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Delete_non_owner_recruiter_is_forbidden()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var slot = SchedulingData.Slot(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var res = await RunDelete(uow, slot.Id, user: _ownerId, role: AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Single(uow.Repo<AvailabilitySlot>().Items); // vẫn còn
    }

    [Fact]
    public async Task Delete_booked_slot_is_rejected()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, capacity: 2, booked: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var res = await RunDelete(uow, slot.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("đã có ứng viên đặt", res.Error);
        Assert.Single(uow.Repo<AvailabilitySlot>().Items);
    }

    [Fact]
    public async Task Delete_empty_slot_succeeds()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, booked: 0);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var res = await RunDelete(uow, slot.Id);

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<AvailabilitySlot>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // ---------- UpdateSlotCapacityCommandHandler ----------

    private Task<Result<AvailabilitySlotResponse>> RunCapacity(
        InMemoryUnitOfWork uow, Guid slotId, int capacity, Guid? user = null, string? role = null)
        => new UpdateSlotCapacityCommandHandler(uow)
            .Handle(new UpdateSlotCapacityCommand(slotId, capacity, user ?? _ownerId, role ?? AppRoles.Recruiter), CancellationToken.None);

    [Fact]
    public async Task UpdateCapacity_slot_not_found_returns_not_found()
    {
        var res = await RunCapacity(new InMemoryUnitOfWork(), Guid.NewGuid(), 5);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UpdateCapacity_non_owner_recruiter_is_forbidden()
    {
        var job = SchedulingData.Job(owner: Guid.NewGuid());
        var slot = SchedulingData.Slot(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var res = await RunCapacity(uow, slot.Id, 5, user: _ownerId, role: AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task UpdateCapacity_khac_mot_deu_bi_chan(int capacity)
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, capacity: 2);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var res = await RunCapacity(uow, slot.Id, capacity);

        Assert.True(res.IsFailure);
        Assert.Contains("MỘT ứng viên", res.Error);
        Assert.Equal(2, slot.Capacity); // giữ nguyên
    }

    [Fact]
    public async Task UpdateCapacity_ha_ve_mot_thi_khong_duoc_nho_hon_so_da_dat()
    {
        // Ca dữ liệu cũ có 3 người đã đặt: hạ về 1 sẽ đuổi hai người ra khỏi chỗ họ đã giữ mà
        // không ai báo — phải dời họ sang ca khác trước.
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, capacity: 5, booked: 3);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var res = await RunCapacity(uow, slot.Id, 1);

        Assert.True(res.IsFailure);
        Assert.Contains("không được nhỏ hơn số đã đặt", res.Error);
        Assert.Equal(5, slot.Capacity);
    }

    [Fact]
    public async Task UpdateCapacity_ha_ca_cu_ve_mot_thanh_cong()
    {
        var job = SchedulingData.Job(owner: _ownerId);
        var slot = SchedulingData.Slot(job.Id, capacity: 2, booked: 1);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(slot);

        var res = await RunCapacity(uow, slot.Id, 1);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value!.Capacity);
        Assert.Equal(1, slot.Capacity);
        Assert.Equal(1, uow.SaveChangesCount);
    }
}
