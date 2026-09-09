using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Scheduling;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Applications
{
    /// <summary>
    /// Trạng thái CHI TIẾT của hồ sơ tại vòng đang diễn ra — thứ mà cột "Trạng thái" trên bảng ứng
    /// viên cần hiển thị.
    ///
    /// <b>Vì sao không dùng thẳng <c>applications.status</c>.</b> Cột đó chỉ nói hồ sơ đang ở KHÚC
    /// nào của phễu (<c>interview</c> = "đang trong chuỗi vòng phỏng vấn"), nên suốt cả một vòng —
    /// từ lúc xếp lịch, ứng viên xác nhận, báo bận, ngồi làm bài, vào phòng chờ, phỏng vấn xong, tới
    /// lúc chờ chốt kết quả — bảng chỉ hiện đúng một chữ "Đang phỏng vấn". Người vận hành nhìn vào
    /// không biết việc tiếp theo của mình là gì.
    ///
    /// <b>SUY RA, không lưu thêm cột.</b> Mọi giá trị dưới đây tính từ dữ liệu đã có (booking, bài
    /// thi, phiên phỏng vấn, đánh giá). Thêm một cột trạng thái thứ hai vào DB là tạo nguồn sự thật
    /// thứ hai phải nhớ cập nhật ở ~10 chỗ — đúng kiểu trôi lệch mà ADR-058 đã phải đi chữa.
    /// </summary>
    public static class ApplicationStageStatus
    {
        // Giai đoạn hồ sơ (trước khi vào vòng)
        public const string CvPending = "cv_pending";
        public const string HmReview = "hm_review";
        public const string AwaitingSchedule = "awaiting_schedule";

        // Đã có lịch cho vòng hiện tại
        public const string SchedulePending = "schedule_pending";
        public const string ScheduleConfirmed = "schedule_confirmed";
        public const string ScheduleDeclined = "schedule_declined";

        // Vòng trắc nghiệm
        public const string TestOpen = "test_open";
        public const string TestSubmitted = "test_submitted";

        // Vòng hội thoại với AI
        public const string InterviewWaiting = "interview_waiting";
        public const string InterviewActive = "interview_active";
        public const string InterviewDone = "interview_done";

        public const string PendingResult = "pending_result";
        public const string Missed = "missed";

        /// <summary>
        /// Tính trạng thái chi tiết cho một loạt hồ sơ. Mọi truy vấn gom theo LÔ — bảng ứng viên có
        /// thể vài trăm dòng, hỏi từng dòng là bấy nhiêu lượt đi DB.
        /// </summary>
        public static async Task<Dictionary<Guid, string>> ComputeAsync(
            IUnitOfWork unitOfWork,
            IReadOnlyList<(Guid Id, Guid JobPostingId, string Status, int? CurrentRound)> apps,
            CancellationToken ct)
        {
            var result = new Dictionary<Guid, string>();
            if (apps.Count == 0) return result;

            var appIds = apps.Select(a => a.Id).ToList();
            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();

            var bookings = (await unitOfWork.Repository<InterviewBooking>()
                .FindAsync(b => appIds.Contains(b.ApplicationId), ct)).ToList();

            var slotIds = bookings.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slots = slotIds.Count == 0
                ? new List<AvailabilitySlot>()
                : (await unitOfWork.Repository<AvailabilitySlot>()
                    .FindAsync(s => slotIds.Contains(s.Id), ct)).ToList();

            var submissions = (await unitOfWork.Repository<OnlineTestSubmission>()
                .FindAsync(s => appIds.Contains(s.ApplicationId), ct)).ToList();

            var sessions = (await unitOfWork.Repository<InterviewSession>()
                .FindAsync(s => appIds.Contains(s.ApplicationId) && s.SessionType == "real", ct)).ToList();

            var roundConfigs = (await unitOfWork.Repository<InterviewRoundConfig>()
                .FindAsync(r => jobIds.Contains(r.JobPostingId), ct)).ToList();

            var now = DateTimeOffset.UtcNow;

            foreach (var app in apps)
            {
                result[app.Id] = Resolve(app, bookings, slots, submissions, sessions, roundConfigs, now);
            }

            return result;
        }

        private static string Resolve(
            (Guid Id, Guid JobPostingId, string Status, int? CurrentRound) app,
            List<InterviewBooking> bookings,
            List<AvailabilitySlot> slots,
            List<OnlineTestSubmission> submissions,
            List<InterviewSession> sessions,
            List<InterviewRoundConfig> roundConfigs,
            DateTimeOffset now)
        {
            // Trạng thái ĐÃ ĐÓNG nói lên tất cả — không có "việc đang diễn ra" nào để mô tả thêm.
            if (ApplicationStatuses.IsTerminal(app.Status)) return app.Status;

            if (ApplicationStatuses.Is(app.Status, ApplicationStatuses.CvSubmitted)
                || ApplicationStatuses.Is(app.Status, ApplicationStatuses.Invited))
                return CvPending;

            if (ApplicationStatuses.Is(app.Status, ApplicationStatuses.HmReview)) return HmReview;

            var round = app.CurrentRound ?? 1;

            // ---- Vòng đang diễn ra: phiên phỏng vấn nói to nhất -------------------------------
            // Có phiên là ứng viên đã có mặt; trạng thái phiên là thứ đang xảy ra NGAY LÚC NÀY.
            var session = sessions
                .Where(s => s.ApplicationId == app.Id && s.RoundNumber == round)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            if (session != null)
            {
                if (InterviewSessionStatuses.Is(session.Status, InterviewSessionStatuses.Waiting))
                    return InterviewWaiting;
                if (InterviewSessionStatuses.Is(session.Status, InterviewSessionStatuses.Active))
                    return InterviewActive;
                // Phiên đã đóng: còn chờ AI chấm + nhân sự chốt.
                return InterviewDone;
            }

            // ---- Vòng trắc nghiệm --------------------------------------------------------------
            var roundType = roundConfigs
                .FirstOrDefault(r => r.JobPostingId == app.JobPostingId && r.RoundNumber == round)?.RoundType;

            if (InterviewInviteEmail.IsOnlineTest(roundType))
            {
                if (submissions.Any(s => s.ApplicationId == app.Id && s.RoundNumber == round))
                    return TestSubmitted;
                // Chưa nộp: mở rồi hay chưa tới giờ — cả hai đều là "chờ ứng viên làm bài", nhưng
                // phân biệt được thì người vận hành biết có nên nhắc hay không.
                return TestOpen;
            }

            // ---- Lịch của vòng ----------------------------------------------------------------
            var roundBookings = bookings
                .Where(b => b.ApplicationId == app.Id && b.RoundNumber == round)
                .ToList();

            var live = roundBookings
                .Where(b => string.Equals(b.Status, BookingStatus.Scheduled, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefault();

            if (live != null)
            {
                var slot = slots.FirstOrDefault(s => s.Id == live.AvailabilitySlotId);

                // Quá giờ mà không có phiên phỏng vấn nào → ứng viên không đến (ADR-059 gọi đây là
                // no-show; tác vụ nền sẽ đóng hồ sơ, nhưng bảng phải nói ngay).
                if (slot != null && slot.EndTime <= now) return Missed;

                if (string.Equals(live.ConfirmationStatus, BookingConfirmationStatus.Confirmed,
                        StringComparison.OrdinalIgnoreCase))
                    return ScheduleConfirmed;

                return SchedulePending;
            }

            // Từng có lịch nhưng người ta báo bận → đang chờ xếp lại. Khác hẳn "chưa từng xếp lịch",
            // và đó là phân biệt quyết định việc tiếp theo của Recruiter.
            if (roundBookings.Any(b => string.Equals(b.DeclinedBy, BookingDeclinedBy.Candidate,
                    StringComparison.OrdinalIgnoreCase)))
                return ScheduleDeclined;

            if (ApplicationStatuses.Is(app.Status, ApplicationStatuses.Screening)) return AwaitingSchedule;

            return PendingResult;
        }
    }
}
