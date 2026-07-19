using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Jobs.Commands.CreateJobSlots
{
    /// <summary>HR cấu hình availability slots (khung giờ phỏng vấn thật per vòng) cho một job.</summary>
    public record CreateJobSlotsCommand(Guid JobId, List<CreateAvailabilitySlotRequest> Slots) : IRequest<Result>;

    public class CreateJobSlotsCommandHandler : IRequestHandler<CreateJobSlotsCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateJobSlotsCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(CreateJobSlotsCommand command, CancellationToken ct)
        {
            var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(command.JobId, ct);
            if (job == null)
                return Result.Failure("Job posting not found.", CommonErrorCodes.NotFound);

            foreach (var slotDto in command.Slots)
            {
                var slot = new AvailabilitySlot
                {
                    JobPostingId = job.Id,
                    RoundNumber = slotDto.RoundNumber,
                    StartTime = slotDto.StartTime,
                    EndTime = slotDto.EndTime,
                    Timezone = slotDto.Timezone,
                    Capacity = slotDto.Capacity,
                    BookedCount = 0
                };
                await _unitOfWork.Repository<AvailabilitySlot>().AddAsync(slot, ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
    }
}
