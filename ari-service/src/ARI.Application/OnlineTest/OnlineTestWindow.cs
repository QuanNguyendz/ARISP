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
    /// Khung giờ của MỘT ca thi trắc nghiệm — luật DUY NHẤT về "khi nào được vào, khi nào bài đóng" (ADR-072).
    ///
    /// <b>Bài thi là một đợt thi có giờ cố định:</b> mở lúc giờ hẹn, đóng lúc
    /// <c>giờ hẹn + thời lượng bài</c>. Ứng viên vào lúc nào trong khung cũng được, nhưng đồng hồ luôn
    /// đếm tới GIỜ ĐÓNG chứ không đếm đủ thời lượng từ lúc họ bấm bắt đầu — ca 09:00, bài 30 phút, vào
    /// lúc 09:15 thì còn 15 phút.
    ///
    /// Trước đây cửa vào mở một tiếng và mỗi người có nguyên đồng hồ của riêng mình, nên một ca 30 phút
    /// thực chất kéo dài tới 1 tiếng 35 phút, người vào muộn nhất làm bài khi người khác đã xong từ lâu,
    /// và "giờ đóng" mỗi người một khác — không ai nói được bài thi kết thúc lúc nào.
    ///
    /// <b>Thời lượng có đúng MỘT nguồn:</b> <see cref="InterviewRoundConfig.MaxDurationMinutes"/> của
    /// vòng trắc nghiệm. Ô "Số phút" ở màn tạo tin và ô "Thời lượng" ở màn ngân hàng đề cùng ghi vào
    /// cột đó. Trước đây có hai cột (thêm <c>job_postings.online_test_duration_minutes</c>) và bài thi
    /// chỉ đọc một trong hai, nên số phút khai ở màn tạo tin không có tác dụng gì.
    /// </summary>
    public static class OnlineTestWindow
    {
        /// <summary>Thời lượng khi vòng chưa khai (hoặc khai hỏng) — trùng mặc định của màn tạo tin.</summary>
        public const int DefaultDurationMinutes = 30;

        public const int MinDurationMinutes = 1;
        public const int MaxDurationMinutes = 300;

        public const string InvalidDurationMessage = "Thời lượng bài thi trắc nghiệm phải từ 1 đến 300 phút.";

        /// <summary>
        /// Mã lỗi khi đổi thời lượng lúc đang có ca thi chưa đóng — giao diện dịch theo mã.
        /// </summary>
        public const string DurationLockedCode = "online_test_duration_locked";

        /// <summary>
        /// Độ trễ mạng được tính thêm vào hạn nộp. Bài tự nộp đúng lúc hết đồng hồ (hoặc lúc đóng trang)
        /// vẫn phải đi hết đường truyền mới tới server.
        ///
        /// Cố ý ngắn: đồng hồ phía trình duyệt tự nộp ĐÚNG giờ đóng, nên khoảng này chỉ để phủ một lượt
        /// gửi mạng. Để dài thì ai chặn được lệnh tự nộp là có thêm chừng ấy phút làm bài.
        /// </summary>
        public static readonly TimeSpan SubmitGrace = TimeSpan.FromMinutes(1);

        public static bool IsValidDuration(int minutes) =>
            minutes >= MinDurationMinutes && minutes <= MaxDurationMinutes;

        public static bool IsTestRound(InterviewRoundConfig? round) =>
            InterviewInviteEmail.IsOnlineTest(round?.RoundType);

        /// <summary>Thời lượng của một vòng trắc nghiệm. Chưa khai hoặc khai hỏng (≤ 0) → mặc định.</summary>
        public static int DurationOf(InterviewRoundConfig? round) =>
            round is { MaxDurationMinutes: > 0 } r ? r.MaxDurationMinutes : DefaultDurationMinutes;

        /// <summary>Thời lượng bài của (tin, vòng), tra trong danh sách cấu hình vòng đã nạp sẵn.</summary>
        public static int DurationOf(IEnumerable<InterviewRoundConfig> configs, Guid jobPostingId, int roundNumber) =>
            DurationOf(configs.FirstOrDefault(r => r.JobPostingId == jobPostingId && r.RoundNumber == roundNumber));

        /// <summary>Giờ bài thi ĐÓNG: sau mốc này không ai vào được nữa và đồng hồ của mọi người đã về 0.</summary>
        public static DateTimeOffset ClosesAt(DateTimeOffset opensAt, int durationMinutes) =>
            opensAt + TimeSpan.FromMinutes(durationMinutes > 0 ? durationMinutes : DefaultDurationMinutes);

        /// <summary>
        /// Hạn chót server còn NHẬN bài: giờ đóng + độ trễ mạng. Qua mốc này, "chưa có bài" chắc chắn là
        /// "không vào làm", và hệ thống nộp thay (<see cref="OnlineTestExpiry"/>).
        /// </summary>
        public static DateTimeOffset SubmissionDeadline(DateTimeOffset opensAt, int durationMinutes) =>
            ClosesAt(opensAt, durationMinutes) + SubmitGrace;

        /// <summary>Bài đang mở — vào làm được ngay lúc này.</summary>
        public static bool IsOpen(DateTimeOffset opensAt, int durationMinutes, DateTimeOffset now) =>
            now >= opensAt && now < ClosesAt(opensAt, durationMinutes);

        /// <summary>
        /// Vòng trắc nghiệm ĐẦU TIÊN của tin theo số vòng, hoặc <c>null</c> khi tin không có vòng nào.
        /// Mọi chỗ "vòng thi của tin là vòng nào" đi qua đây để cùng chọn một vòng.
        /// </summary>
        public static async Task<InterviewRoundConfig?> TestRoundAsync(
            IUnitOfWork uow, Guid jobPostingId, CancellationToken ct)
        {
            var rounds = await uow.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == jobPostingId, ct);
            return rounds.Where(IsTestRound).OrderBy(r => r.RoundNumber).FirstOrDefault();
        }

        /// <summary>
        /// Lý do KHÔNG được đổi thời lượng của vòng thi lúc này, hoặc <c>null</c> nếu đổi được.
        ///
        /// Thời lượng quyết định giờ đóng bài của MỌI ca thi trong vòng. Người đã được hẹn thì đã nhận
        /// thư ghi giờ đóng, người đang làm thì đồng hồ trên máy họ đang đếm về giờ đóng cũ — đổi lúc
        /// này là đổi giờ thi của người khác mà không báo, và người đang làm có thể bị từ chối bài vì
        /// hạn nộp phía server đã dời sớm hơn đồng hồ họ thấy. Cùng lý do màn xếp lịch không cho sửa
        /// giờ một ca đã có người giữ chỗ.
        ///
        /// Chỉ tính người CÒN việc: lịch đang giữ chỗ, ca chưa đóng, chưa có bài.
        /// </summary>
        public static async Task<string?> DurationChangeBlockerAsync(
            IUnitOfWork uow, Guid jobPostingId, int roundNumber, int currentDurationMinutes,
            DateTimeOffset now, CancellationToken ct)
        {
            var slots = (await uow.Repository<AvailabilitySlot>().FindAsync(
                    s => s.JobPostingId == jobPostingId && s.RoundNumber == roundNumber, ct))
                .Where(s => ClosesAt(s.StartTime, currentDurationMinutes) > now)
                .ToList();
            if (slots.Count == 0) return null;

            var slotIds = slots.Select(s => s.Id).ToList();
            var appIds = (await uow.Repository<InterviewBooking>().FindAsync(
                    b => slotIds.Contains(b.AvailabilitySlotId)
                         && b.RoundNumber == roundNumber
                         && b.Status == BookingStatus.Scheduled, ct))
                .Select(b => b.ApplicationId)
                .Distinct()
                .ToList();
            if (appIds.Count == 0) return null;

            var done = (await uow.Repository<OnlineTestSubmission>().FindAsync(
                    x => appIds.Contains(x.ApplicationId) && x.RoundNumber == roundNumber, ct))
                .Select(x => x.ApplicationId)
                .ToHashSet();

            var waiting = appIds.Count(id => !done.Contains(id));
            if (waiting == 0) return null;

            return $"Đang có {waiting} ứng viên được hẹn làm bài trắc nghiệm vòng {roundNumber} mà ca thi chưa kết thúc. "
                   + "Đổi thời lượng lúc này là đổi giờ đóng bài của họ mà không báo trước — "
                   + "hãy đợi các ca thi đó kết thúc, hoặc dời lịch họ sang ca khác rồi mới đổi.";
        }

        /// <summary>
        /// Kéo giờ kết thúc của các ca thi CHƯA ĐÓNG theo thời lượng mới.
        ///
        /// Giờ kết thúc của ca thi là giờ đóng bài (xem <see cref="EndTimeFor"/>) — để nguyên thì màn
        /// xếp lịch, Portal và thư mời vẫn in khung giờ cũ trong khi bài thi đóng theo giờ mới. Ca đã
        /// đóng giữ nguyên: đó là lịch sử của một đợt thi đã diễn ra với thời lượng cũ.
        ///
        /// Không tự lưu — nơi gọi lưu cùng lúc với thay đổi thời lượng.
        /// </summary>
        public static async Task SyncSlotEndsAsync(
            IUnitOfWork uow, Guid jobPostingId, int roundNumber, int oldDurationMinutes, int newDurationMinutes,
            DateTimeOffset now, CancellationToken ct)
        {
            var slots = (await uow.Repository<AvailabilitySlot>().FindAsync(
                    s => s.JobPostingId == jobPostingId && s.RoundNumber == roundNumber, ct))
                .Where(s => ClosesAt(s.StartTime, oldDurationMinutes) > now)
                .ToList();

            foreach (var slot in slots)
            {
                var end = EndTimeFor(slot.StartTime, newDurationMinutes);
                if (slot.EndTime == end) continue;
                slot.EndTime = end;
                slot.UpdatedAt = now;
                uow.Repository<AvailabilitySlot>().Update(slot);
            }
        }

        /// <summary>
        /// Giờ kết thúc LƯU TRÊN một ca thi: đúng bằng giờ đóng bài. Ca thi không có giờ kết thúc riêng
        /// — người tạo ca chỉ chọn giờ mở, server tự tính phần còn lại.
        /// </summary>
        public static DateTimeOffset EndTimeFor(DateTimeOffset startTime, int durationMinutes) =>
            ClosesAt(startTime, durationMinutes);
    }
}
