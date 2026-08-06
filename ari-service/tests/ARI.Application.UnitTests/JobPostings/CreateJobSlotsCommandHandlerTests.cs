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
/// Cấu hình availability slots cho vòng phỏng vấn thật (UC-47, <see cref="CreateJobSlotsCommandHandler"/>):
/// tạo slot với booked_count = 0, chặn khi job không tồn tại.
/// </summary>
public class CreateJobSlotsCommandHandlerTests
{
    private static Task<Result> Run(InMemoryUnitOfWork uow, Guid jobId, List<CreateAvailabilitySlotRequest> slots)
        => new CreateJobSlotsCommandHandler(uow).Handle(new CreateJobSlotsCommand(jobId, slots), CancellationToken.None);

    [Fact]
    public async Task Job_not_found_fails()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.NewGuid(), new() { JobPostingData.Slot() });

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Creates_slots_with_zero_booked_count()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);
        var slots = new List<CreateAvailabilitySlotRequest>
        {
            JobPostingData.Slot(round: 1, capacity: 3),
            JobPostingData.Slot(round: 2, capacity: 1),
        };

        var res = await Run(uow, job.Id, slots);

        Assert.True(res.IsSuccess);
        var saved = uow.Repo<AvailabilitySlot>().Items;
        Assert.Equal(2, saved.Count);
        Assert.All(saved, s => Assert.Equal(0, s.BookedCount));
        Assert.All(saved, s => Assert.Equal(job.Id, s.JobPostingId));
        Assert.Contains(saved, s => s.RoundNumber == 1 && s.Capacity == 3);
    }

    [Fact]
    public async Task Empty_list_saves_nothing_but_succeeds()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await Run(uow, job.Id, new List<CreateAvailabilitySlotRequest>());

        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<AvailabilitySlot>().Items);
    }
}
