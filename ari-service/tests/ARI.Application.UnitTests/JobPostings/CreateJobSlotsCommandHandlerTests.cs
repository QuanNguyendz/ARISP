using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.CreateJobSlots;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Cấu hình availability slots cho vòng phỏng vấn thật (<see cref="CreateJobSlotsCommandHandler"/>) —
/// test-plan Report5 Unit v1.2, tab "CreateJobSlots" (UTCID01–06): chặn job không tồn tại, map field slot
/// (BookedCount=0) cho 1/nhiều slot, danh sách rỗng vẫn Success, và lỗi Add/Save.
/// </summary>
public class CreateJobSlotsCommandHandlerTests
{
    private static Task<Result> Run(InMemoryUnitOfWork uow, Guid jobId, List<CreateAvailabilitySlotRequest> slots)
        => new CreateJobSlotsCommandHandler(uow).Handle(new CreateJobSlotsCommand(jobId, slots), CancellationToken.None);

    private static List<CreateAvailabilitySlotRequest> One() => new() { JobPostingData.Slot(round: 1, capacity: 3) };

    // UTCID01 — job không tồn tại → not_found, không thêm slot
    [Fact]
    public async Task UTCID01_Job_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow, Guid.NewGuid(), One());
        Assert.True(res.IsFailure);
        Assert.Equal("Job posting not found.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Empty(uow.Repo<AvailabilitySlot>().Items);
    }

    // UTCID02 — 1 slot → map field, BookedCount=0, save 1 lần
    [Fact]
    public async Task UTCID02_Single_slot_mapped()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, job.Id, One());

        Assert.True(res.IsSuccess);
        var slot = Assert.Single(uow.Repo<AvailabilitySlot>().Items);
        Assert.Equal(job.Id, slot.JobPostingId);
        Assert.Equal(1, slot.RoundNumber);
        Assert.Equal(3, slot.Capacity);
        Assert.Equal(0, slot.BookedCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID03 — nhiều slot → map tất cả
    [Fact]
    public async Task UTCID03_Multiple_slots_mapped()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);
        var slots = new List<CreateAvailabilitySlotRequest> { JobPostingData.Slot(round: 1, capacity: 3), JobPostingData.Slot(round: 2, capacity: 1) };

        var res = await Run(uow, job.Id, slots);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, uow.Repo<AvailabilitySlot>().Items.Count);
        Assert.All(uow.Repo<AvailabilitySlot>().Items, s => Assert.Equal(0, s.BookedCount));
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID04 — danh sách rỗng → không thêm slot, vẫn Success (save vẫn gọi)
    [Fact]
    public async Task UTCID04_Empty_list_succeeds()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, job.Id, new List<CreateAvailabilitySlotRequest>());

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<AvailabilitySlot>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID05 — AddAsync ném lỗi
    [Fact]
    public async Task UTCID05_Add_slot_error()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).FailAddFor<AvailabilitySlot>("Add Slot Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, job.Id, One()));
        Assert.Equal("Add Slot Error", ex.Message);
    }

    // UTCID06 — SaveChangesAsync ném lỗi
    [Fact]
    public async Task UTCID06_Save_error()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, job.Id, One()));
        Assert.Equal("Save Error", ex.Message);
    }
}
