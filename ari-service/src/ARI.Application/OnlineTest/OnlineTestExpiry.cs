using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Scheduling;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.OnlineTest
{
    /// <summary>
    /// Bài trắc nghiệm HẾT HẠN = hệ thống nộp thay một bài trống.
    ///
    /// <b>Vì sao không dùng đường "không tham dự" như vòng phỏng vấn.</b> Đường đó (ADR-059) làm ba
    /// việc, và cả ba đều sai với vòng trắc nghiệm:
    /// <list type="number">
    /// <item>Huỷ lịch — rồi Portal đọc lịch đang giữ chỗ, không thấy, và báo ứng viên "Chưa có giờ
    /// làm bài" dù họ đã được hẹn giờ rõ ràng.</item>
    /// <item>TRẢ CHỖ trong ca — ca hiện "Đã đặt 0/1" như chưa ai được hẹn. Chỗ chỉ được trả khi ứng
    /// viên TỪ CHỐI tham dự; người được hẹn mà không đến vẫn là người của ca đó.</item>
    /// <item>Tự ghi <c>not_pass</c> — hồ sơ rơi sang "Không phù hợp" mà không ai bấm gì. Ở vòng
    /// trắc nghiệm, loại hay giữ là quyết định của Recruiter nhìn vào điểm, kể cả điểm 0.</item>
    /// </list>
    /// Nên ở đây: lịch giữ nguyên, hồ sơ giữ nguyên, và vòng có một <b>kết quả</b> — bài trống chấm
    /// như mọi bài khác, đánh dấu <see cref="OnlineTestSubmittedBy.System"/> để mọi màn nói đúng
    /// "hết hạn" thay vì "đã nộp bài".
    ///
    /// <b>Vì sao chờ tới hạn chót chứ không nộp ngay lúc bài đóng.</b> Người đang làm bài tự nộp
    /// đúng giờ đóng, và bài đó còn phải đi hết đường truyền. Chỉ sau
    /// <see cref="OnlineTestWindow.SubmissionDeadline"/> (giờ đóng + độ trễ mạng), "chưa có bài" mới
    /// chắc chắn là "không vào làm". Trong khoảng giữa, Portal đã hiện "Đã hết hạn" nhờ cờ
    /// <c>Expired</c> của <see cref="GetCandidateOnlineTestQuery"/>, nên ứng viên không phải chờ tác
    /// vụ nền mới biết.
    /// </summary>
    public static class OnlineTestExpiry
    {
        /// <summary>Một bài vừa được hệ thống nộp thay — đủ để báo cho đúng người.</summary>
        public sealed record ExpiredSubmission(
            Guid SubmissionId,
            Guid ApplicationId,
            Guid JobPostingId,
            Guid? CandidateAccountId,
            string CandidateName,
            int RoundNumber);

        /// <summary>
        /// Nộp thay mọi bài trắc nghiệm đã quá hạn chót mà chưa có bài. Idempotent: chạy lại không
        /// sinh bài thứ hai (bài đã có thì bỏ qua; DB còn chặn thêm bằng unique
        /// <c>(application_id, round_number)</c>).
        /// </summary>
        public static async Task<List<ExpiredSubmission>> AutoSubmitExpiredAsync(
            IUnitOfWork uow, DateTimeOffset now, CancellationToken ct)
        {
            var created = new List<ExpiredSubmission>();

            // Chỉ lịch ĐANG GIỮ CHỖ: người đã từ chối tham dự (declined) hay đã bị loại (cancelled)
            // không còn bài thi nào để hết hạn.
            var live = (await uow.Repository<InterviewBooking>().FindAsync(
                b => b.Status == BookingStatus.Scheduled, ct)).ToList();
            if (live.Count == 0) return created;

            var slotIds = live.Select(b => b.AvailabilitySlotId).Distinct().ToList();
            var slotById = (await uow.Repository<AvailabilitySlot>().FindAsync(s => slotIds.Contains(s.Id), ct))
                .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());

            // Lọc thô bằng cận dưới không phụ thuộc thời lượng bài (thời lượng tối thiểu + độ trễ mạng)
            // để khỏi tải cấu hình của mọi tin; hạn chót chính xác được tính lại theo từng vòng bên dưới.
            var minimumDeadline = TimeSpan.FromMinutes(OnlineTestWindow.MinDurationMinutes) + OnlineTestWindow.SubmitGrace;
            var candidates = live
                .Where(b => slotById.TryGetValue(b.AvailabilitySlotId, out var s) && s.StartTime + minimumDeadline <= now)
                // Mỗi hồ sơ + vòng chỉ tính lịch MỚI NHẤT — cùng cách ScheduledStartAsync chọn giờ hẹn.
                .GroupBy(b => (b.ApplicationId, b.RoundNumber))
                .Select(g => g.OrderByDescending(b => b.CreatedAt).First())
                .ToList();
            if (candidates.Count == 0) return created;

            var jobIds = candidates.Select(b => slotById[b.AvailabilitySlotId].JobPostingId).Distinct().ToList();
            var roundConfigs = (await uow.Repository<InterviewRoundConfig>()
                .FindAsync(r => jobIds.Contains(r.JobPostingId), ct)).ToList();

            bool IsTestRound(Guid jobId, int round) => InterviewInviteEmail.IsOnlineTest(
                roundConfigs.FirstOrDefault(r => r.JobPostingId == jobId && r.RoundNumber == round)?.RoundType);

            candidates = candidates
                .Where(b => IsTestRound(slotById[b.AvailabilitySlotId].JobPostingId, b.RoundNumber))
                .ToList();
            if (candidates.Count == 0) return created;

            var appIds = candidates.Select(b => b.ApplicationId).Distinct().ToList();
            var submitted = (await uow.Repository<OnlineTestSubmission>()
                    .FindAsync(s => appIds.Contains(s.ApplicationId), ct))
                .Select(s => (s.ApplicationId, s.RoundNumber))
                .ToHashSet();

            var testJobIds = candidates.Select(b => slotById[b.AvailabilitySlotId].JobPostingId).Distinct().ToList();
            var jobs = (await uow.Repository<JobPosting>().FindAsync(j => testJobIds.Contains(j.Id), ct))
                .ToDictionary(j => j.Id);
            var apps = (await uow.Repository<ARI.Domain.Entities.Application>()
                    .FindAsync(a => appIds.Contains(a.Id), ct))
                .ToDictionary(a => a.Id);
            var banks = (await uow.Repository<OnlineTestQuestion>()
                    .FindAsync(q => testJobIds.Contains(q.JobPostingId), ct))
                .GroupBy(q => q.JobPostingId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var noAnswers = new Dictionary<Guid, List<int>>();

            foreach (var booking in candidates)
            {
                if (submitted.Contains((booking.ApplicationId, booking.RoundNumber))) continue;

                var slot = slotById[booking.AvailabilitySlotId];
                if (!jobs.TryGetValue(slot.JobPostingId, out var job)) continue;
                if (!apps.TryGetValue(booking.ApplicationId, out var app)) continue;

                // Hồ sơ đã đóng (rút, bị loại, đã tuyển…) thì không còn vòng nào để ghi kết quả.
                if (ApplicationStatuses.IsTerminal(app.Status)) continue;

                var duration = OnlineTestWindow.DurationOf(roundConfigs, job.Id, booking.RoundNumber);
                if (now <= OnlineTestWindow.SubmissionDeadline(slot.StartTime, duration))
                    continue;

                // Chấm đúng như một bài ứng viên nộp trống — cùng bộ đề (deterministic), cùng hàm chấm.
                var bank = banks.TryGetValue(job.Id, out var qs) ? qs : new List<OnlineTestQuestion>();
                var drawn = OnlineTestSupport.DrawQuestions(bank, app.Id, booking.RoundNumber, job.OnlineTestQuestionsPerTest);
                var (correct, total, score) = OnlineTestSupport.Grade(drawn, noAnswers);

                var submission = new OnlineTestSubmission
                {
                    ApplicationId = app.Id,
                    RoundNumber = booking.RoundNumber,
                    SelectedAnswers = OnlineTestSupport.SerializeAnswers(noAnswers),
                    Score = score,
                    IsPassed = score >= job.OnlineTestPassScore,
                    CorrectCount = correct,
                    TotalQuestions = total,
                    TabSwitchCount = 0,
                    SubmittedBy = OnlineTestSubmittedBy.System,
                };
                await uow.Repository<OnlineTestSubmission>().AddAsync(submission, ct);
                submitted.Add((app.Id, booking.RoundNumber));

                created.Add(new ExpiredSubmission(
                    submission.Id, app.Id, job.Id, app.CandidateAccountId, app.CandidateName, booking.RoundNumber));
            }

            if (created.Count > 0) await uow.SaveChangesAsync(ct);
            return created;
        }
    }
}
