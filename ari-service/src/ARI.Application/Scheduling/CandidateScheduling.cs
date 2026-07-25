using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Scheduling
{
    // ============================================================
    // GET /api/candidate/schedule — lịch của ứng viên đang đăng nhập
    // (Ứng viên KHÔNG tự chọn lịch nữa — HR gán trực tiếp từ kho slot, ADR-048.
    //  Đây là view chỉ-đọc để ứng viên xem giờ đã được nhân sự xếp.)
    // ============================================================

    public record CandidateScheduleDto(List<AvailabilitySlotResponse> UpcomingSlots, List<AvailabilitySlotResponse> PastSlots);

    public record GetCandidateScheduleQuery(Guid AccountId, string? Email) : IRequest<Result<CandidateScheduleDto>>;

    public class GetCandidateScheduleQueryHandler : IRequestHandler<GetCandidateScheduleQuery, Result<CandidateScheduleDto>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetCandidateScheduleQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<CandidateScheduleDto>> Handle(GetCandidateScheduleQuery request, CancellationToken ct)
        {
            var accId = request.AccountId;
            var emailClaim = request.Email;

            var myAppIds = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>().FindAsync(
                    a => (accId != Guid.Empty && a.CandidateAccountId == accId)
                         || (emailClaim != null && a.CandidateEmail == emailClaim), ct))
                .Select(a => a.Id).ToHashSet();

            var upcoming = new List<AvailabilitySlotResponse>();
            var past = new List<AvailabilitySlotResponse>();

            if (myAppIds.Count > 0)
            {
                var bookings = (await _unitOfWork.Repository<InterviewBooking>().FindAsync(
                        b => myAppIds.Contains(b.ApplicationId) && b.Status == "scheduled", ct)).ToList();
                var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
                if (slotIds.Count > 0)
                {
                    var slots = await _unitOfWork.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id), ct);
                    var now = DateTimeOffset.UtcNow;
                    foreach (var s in slots.OrderBy(s => s.StartTime))
                    {
                        var dto = AvailabilitySlotResponse.FromEntity(s);
                        (s.StartTime >= now ? upcoming : past).Add(dto);
                    }
                }
            }

            return Result.Success(new CandidateScheduleDto(upcoming, past));
        }
    }
}
