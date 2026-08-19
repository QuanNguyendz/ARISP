using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Scheduling
{
    /// <summary>Helpers dùng chung của feature Scheduling.</summary>
    internal static class SchedulingSupport
    {
        /// <summary>Kiểm tra staff có quyền quản lý slot của job này không (chủ tin hoặc admin).</summary>
        public static async Task<(bool ok, JobPosting? job)> CanManageAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
        {
            var job = await unitOfWork.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null) return (false, null);
            if (userId is not { } uid || uid == Guid.Empty) return (false, job);
            var isAdmin = role == AppRoles.SuperAdmin || role == AppRoles.HrAdmin;
            return (isAdmin || job.CreatedByUserId == uid, job);
        }

        /// <summary>
        /// Vòng này đã LỠ buổi phỏng vấn thật chưa: còn lịch hiệu lực (<c>scheduled</c>) đã qua giờ
        /// mà không có phiên phỏng vấn THẬT nào của vòng.
        ///
        /// Lỡ buổi thật rồi thì phỏng vấn thử không còn nghĩa gì — thử là để chuẩn bị cho buổi thật,
        /// mà buổi thật đã trôi qua. Booking đã <c>declined</c>/<c>cancelled</c> KHÔNG tính là lỡ:
        /// đó là người báo bận hoặc bị hệ thống huỷ, nhân sự sẽ xếp lại ca khác (ADR-048/058).
        /// </summary>
        public static async Task<bool> HasMissedRealInterviewAsync(
            IUnitOfWork unitOfWork, Guid applicationId, int roundNumber, CancellationToken ct)
        {
            var bookings = (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                b => b.ApplicationId == applicationId
                     && b.RoundNumber == roundNumber
                     && b.Status == BookingStatus.Scheduled, ct)).ToList();
            if (bookings.Count == 0) return false;

            var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slots = await unitOfWork.Repository<AvailabilitySlot>()
                .FindAsync(s => slotIds.Contains(s.Id), ct);

            var now = DateTimeOffset.UtcNow;
            if (!slots.Any(s => s.EndTime <= now)) return false;

            var realSessions = await unitOfWork.Repository<InterviewSession>().FindAsync(
                s => s.ApplicationId == applicationId
                     && s.RoundNumber == roundNumber
                     && s.SessionType == "real", ct);
            return !realSessions.Any();
        }
    }
}
