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

/// <summary>Hằng số dùng chung cho test kho khung giờ (bám GUID cố định của Report5 Unit v1.2).</summary>
internal static class SlotIds
{
    public static readonly Guid Owner = Guid.Parse("86000000-0000-0000-0000-000000000001");
    public static readonly Guid JobId = Guid.Parse("84000000-0000-0000-0000-000000000001");
    public static readonly Guid SlotId = Guid.Parse("83000000-0000-0000-0000-000000000001");
    public static readonly DateTimeOffset Future = DateTimeOffset.UtcNow.AddDays(3);
    public static readonly DateTimeOffset Past = DateTimeOffset.UtcNow.AddDays(-3);

    public static JobPosting Job(Guid? owner = null)
        => new() { Id = JobId, CreatedByUserId = owner ?? Owner, Title = "Backend Developer" };

    public static AvailabilitySlot Slot(Guid? id = null, int round = 1, int capacity = 2, int booked = 0, DateTimeOffset? start = null)
        => new()
        {
            Id = id ?? SlotId, JobPostingId = JobId, RoundNumber = round,
            StartTime = start ?? Future, EndTime = (start ?? Future).AddHours(1),
            Timezone = "Asia/Ho_Chi_Minh", Capacity = capacity, BookedCount = booked,
        };
}

/// <summary>
/// Xem kho khung giờ theo job/vòng (<see cref="GetAvailabilitySlotsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetAvailabilitySlots" (UTCID01–05): bắt buộc jobId, tồn tại tin, phân quyền chủ tin/admin, sắp theo giờ, lỗi repo.
/// </summary>
public class GetAvailabilitySlotsQueryHandlerTests
{
    private static Task<Result<List<AvailabilitySlotResponse>>> Run(InMemoryUnitOfWork uow, Guid jobId)
        => new GetAvailabilitySlotsQueryHandler(uow)
            .Handle(new GetAvailabilitySlotsQuery(jobId, 1, SlotIds.Owner, AppRoles.Recruiter), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Empty_job_id()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.Empty);
        Assert.True(res.IsFailure);
        Assert.Equal("jobPostingId là bắt buộc.", res.Error);
    }

    [Fact]
    public async Task UTCID02_Job_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(), SlotIds.JobId);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tin tuyển dụng.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Cannot_manage_forbidden()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job(owner: Guid.NewGuid())).Seed(SlotIds.Slot());
        var res = await Run(uow, SlotIds.JobId);
        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không có quyền xem lịch của tin này.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID04_Owner_gets_slots_ordered()
    {
        var early = SlotIds.Slot(id: Guid.Parse("83000000-0000-0000-0000-000000000001"), start: SlotIds.Future);
        var late = SlotIds.Slot(id: Guid.Parse("83000000-0000-0000-0000-000000000002"), start: SlotIds.Future.AddHours(5));
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).Seed(late, early);

        var res = await Run(uow, SlotIds.JobId);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Count);
        Assert.Equal(early.Id, res.Value[0].Id);
    }

    [Fact]
    public async Task UTCID05_Slot_repo_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).FailFindFor<AvailabilitySlot>("Slot DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, SlotIds.JobId));
        Assert.Equal("Slot DB Error", ex.Message);
    }
}

/// <summary>
/// Tạo khung giờ (<see cref="CreateSlotCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "CreateSlot" (UTCID01–09): validate jobId/giờ/tương lai/sức chứa/vòng, phân quyền, happy path, lỗi save.
/// </summary>
public class CreateSlotCommandHandlerTests
{
    private static Task<Result<AvailabilitySlotResponse>> Run(InMemoryUnitOfWork uow, CreateSlotRequest req)
        => new CreateSlotCommandHandler(uow).Handle(new CreateSlotCommand(req, SlotIds.Owner, AppRoles.Recruiter), CancellationToken.None);

    // capacity mặc định là 1: từ ADR-067 mọi giá trị khác đều bị chặn ngay đầu handler, nên để 2 làm
    // mặc định thì mọi ca khác lại dừng ở đúng câu lỗi đó thay vì ở điều kiện nó định kiểm.
    private static CreateSlotRequest Req(Guid? jobId = null, int round = 1, int capacity = 1, DateTimeOffset? start = null, DateTimeOffset? end = null, string tz = "Asia/Ho_Chi_Minh")
        => new()
        {
            JobPostingId = jobId ?? SlotIds.JobId, RoundNumber = round, Capacity = capacity,
            StartTime = start ?? SlotIds.Future, EndTime = end ?? (start ?? SlotIds.Future).AddHours(1), Timezone = tz,
        };

    [Fact]
    public async Task UTCID01_Empty_job_id()
    {
        var res = await Run(new InMemoryUnitOfWork(), Req(jobId: Guid.Empty));
        Assert.Equal("jobPostingId là bắt buộc.", res.Error);
    }

    [Fact]
    public async Task UTCID02_End_not_after_start()
    {
        var res = await Run(new InMemoryUnitOfWork(), Req(start: SlotIds.Future, end: SlotIds.Future.AddHours(-1)));
        Assert.Equal("Giờ kết thúc phải sau giờ bắt đầu.", res.Error);
    }

    [Fact]
    public async Task UTCID03_Start_not_future()
    {
        var res = await Run(new InMemoryUnitOfWork(), Req(start: SlotIds.Past, end: SlotIds.Past.AddHours(1)));
        Assert.Equal("Khung giờ phải nằm trong tương lai.", res.Error);
    }

    // ADR-067: một ca = một ứng viên. Chặn ở CỔNG TẠO chứ không âm thầm ghi đè về 1 — một ô
    // "sức chứa 3" nhận vào rồi bị bỏ qua trông vẫn như đang có tác dụng.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task UTCID04_Capacity_must_be_exactly_one(int capacity)
    {
        var res = await Run(new InMemoryUnitOfWork(), Req(capacity: capacity));
        Assert.True(res.IsFailure);
        Assert.Equal("Mỗi ca phỏng vấn chỉ nhận MỘT ứng viên. Cần nhiều chỗ hơn thì tạo thêm ca.", res.Error);
    }

    [Fact]
    public async Task UTCID05_Round_zero()
    {
        var res = await Run(new InMemoryUnitOfWork(), Req(round: 0));
        Assert.Equal("RoundNumber phải >= 1.", res.Error);
    }

    [Fact]
    public async Task UTCID06_Job_missing()
    {
        var res = await Run(new InMemoryUnitOfWork(), Req());
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tin tuyển dụng.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID07_Unauthorized()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job(owner: Guid.NewGuid()));
        var res = await Run(uow, Req());
        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không có quyền tạo lịch cho tin này.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID08_Valid_creates_slot()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job());

        var res = await Run(uow, Req(round: 2, capacity: 1));

        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value.BookedCount);
        Assert.Equal(1, res.Value.Capacity);
        Assert.Equal(2, res.Value.RoundNumber);
        var stored = Assert.Single(uow.Repo<AvailabilitySlot>().Items);
        Assert.Equal(0, stored.BookedCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID09_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, Req()));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Xoá khung giờ (<see cref="DeleteSlotCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "DeleteSlot" (UTCID01–05): tồn tại, phân quyền, chặn khi đã có người đặt, happy path, lỗi save.
/// </summary>
public class DeleteSlotCommandHandlerTests
{
    private static Task<Result> Run(InMemoryUnitOfWork uow)
        => new DeleteSlotCommandHandler(uow).Handle(new DeleteSlotCommand(SlotIds.SlotId, SlotIds.Owner, AppRoles.Recruiter), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Slot_missing()
    {
        var res = await Run(new InMemoryUnitOfWork());
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy khung giờ.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Unauthorized()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job(owner: Guid.NewGuid())).Seed(SlotIds.Slot());
        var res = await Run(uow);
        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không có quyền xoá khung giờ này.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Booked_slot_rejected()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).Seed(SlotIds.Slot(booked: 1));
        var res = await Run(uow);
        Assert.True(res.IsFailure);
        Assert.Equal("Không thể xoá khung giờ đã có ứng viên đặt lịch.", res.Error);
        Assert.Single(uow.Repo<AvailabilitySlot>().Items);
    }

    [Fact]
    public async Task UTCID04_Empty_slot_deleted()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).Seed(SlotIds.Slot(booked: 0));
        var res = await Run(uow);
        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<AvailabilitySlot>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID05_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).Seed(SlotIds.Slot(booked: 0)).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Sửa sức chứa khung giờ (<see cref="UpdateSlotCapacityCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "UpdateSlotCapacity" (UTCID01–06): tồn tại, phân quyền, tối thiểu 1, không nhỏ hơn số đã đặt, happy path, lỗi save.
/// </summary>
public class UpdateSlotCapacityCommandHandlerTests
{
    private static Task<Result<AvailabilitySlotResponse>> Run(InMemoryUnitOfWork uow, int capacity)
        => new UpdateSlotCapacityCommandHandler(uow).Handle(new UpdateSlotCapacityCommand(SlotIds.SlotId, capacity, SlotIds.Owner, AppRoles.Recruiter), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Slot_missing()
    {
        var res = await Run(new InMemoryUnitOfWork(), 3);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy khung giờ.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Unauthorized()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job(owner: Guid.NewGuid())).Seed(SlotIds.Slot());
        var res = await Run(uow, 3);
        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không có quyền sửa khung giờ này.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // ADR-067 áp cùng luật với lúc tạo: endpoint này còn lại để HẠ ca dữ liệu cũ (sức chứa > 1)
    // về 1, chứ không phải để nâng lên.
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task UTCID03_Capacity_must_be_exactly_one(int capacity)
    {
        var slot = SlotIds.Slot(capacity: 2);
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).Seed(slot);

        var res = await Run(uow, capacity);

        Assert.True(res.IsFailure);
        Assert.Equal("Mỗi ca phỏng vấn chỉ nhận MỘT ứng viên. Cần nhiều chỗ hơn thì tạo thêm ca.", res.Error);
        Assert.Equal(2, slot.Capacity);   // giữ nguyên
    }

    [Fact]
    public async Task UTCID04_Capacity_below_booked()
    {
        // Ca dữ liệu cũ có 2 người đã đặt: hạ về 1 sẽ đuổi một người ra khỏi chỗ họ đã giữ mà
        // không ai báo — phải dời họ sang ca khác trước.
        var slot = SlotIds.Slot(capacity: 5, booked: 2);
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).Seed(slot);

        var res = await Run(uow, 1);

        Assert.True(res.IsFailure);
        Assert.Equal("Sức chứa không được nhỏ hơn số đã đặt (2).", res.Error);
        Assert.Equal(5, slot.Capacity);
    }

    [Fact]
    public async Task UTCID05_Valid_update()
    {
        var slot = SlotIds.Slot(capacity: 2, booked: 1);
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).Seed(slot);

        var res = await Run(uow, 1);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.Capacity);
        Assert.Equal(1, res.Value.BookedCount);
        Assert.Equal(1, slot.Capacity);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID06_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SlotIds.Job()).Seed(SlotIds.Slot(capacity: 2, booked: 1)).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, 1));
        Assert.Equal("Save Error", ex.Message);
    }
}
