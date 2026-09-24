using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Scheduling;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Interviews
{
    /// <summary>
    /// Luật về Mã phỏng vấn dùng chung cho mọi đường cấp mã (nút ở danh sách ứng viên, màn Phỏng vấn,
    /// trang hồ sơ, cấp hàng loạt) và cho Portal ứng viên. Gom một chỗ vì các đường đó phải trả lời
    /// CÙNG một câu — viết riêng thì đường thêm sau sẽ quên một luật.
    /// </summary>
    public static class InterviewCodeRules
    {
        /// <summary>
        /// Lý do không cấp mã cho vòng trắc nghiệm (ADR-049: bài thi làm trong Portal). Lịch vòng trắc
        /// nghiệm vẫn "scheduled" sau khi thi xong, nên ứng viên đã qua bài thi mà chưa được xếp lịch vòng
        /// kế thì lịch cao nhất vẫn là vòng trắc nghiệm — câu trả lời phải chỉ ra bước tiếp theo, không
        /// chỉ nói "không dùng mã" (nghe như hệ thống đang cấp nhầm vòng).
        /// </summary>
        public static string OnlineTestReason(int round, bool hasNextRound = false) =>
            hasNextRound
                ? $"Vòng {round} là bài trắc nghiệm làm trực tuyến — không dùng mã phỏng vấn. "
                  + $"Ứng viên qua bài thi thì xếp lịch vòng {round + 1} trước, mã sẽ cấp cho vòng đó."
                : $"Vòng {round} là bài trắc nghiệm làm trực tuyến — không dùng mã phỏng vấn.";

        /// <summary>Tin có khai vòng sau vòng này không — để lý do chặn chỉ ra bước tiếp theo.</summary>
        public static async Task<bool> HasNextRoundAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int round, CancellationToken ct)
            => await SchedulingSupport.RoundConfigAsync(unitOfWork, jobPostingId, round + 1, ct) != null;

        /// <summary>
        /// Lịch đang giữ chỗ của vòng cao nhất — vòng CẦN mã khi chỗ gọi không nói vòng nào (ADR-058:
        /// dòng booking là sự thật). Không đoán bằng <c>max(phiên đã xong) + 1</c>: vòng TRẮC NGHIỆM
        /// không sinh phiên phỏng vấn nào, nên qua bài thi vòng 1 rồi xếp lịch vòng 2 thì phép đoán đó
        /// vẫn ra vòng 1 — và cấp mã bị từ chối vì "vòng 1 là bài trắc nghiệm". Lịch mới là thứ nói ứng
        /// viên sắp dự vòng nào; hồ sơ xếp lại lịch của chính vòng đó vẫn là vòng đó.
        /// </summary>
        public static async Task<InterviewBooking?> LiveBookingAsync(
            IUnitOfWork unitOfWork, Guid applicationId, CancellationToken ct)
            => (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => b.ApplicationId == applicationId && b.Status == BookingStatus.Scheduled, ct))
                .OrderByDescending(b => b.RoundNumber)
                .ThenByDescending(b => b.CreatedAt)
                .FirstOrDefault();

        /// <summary>
        /// Ứng viên đã VÀO PHÒNG vòng này chưa (đang chờ HM, đang phỏng vấn, hoặc đã xong). Trả lý do
        /// không cấp thêm mã, hoặc <c>null</c> nếu cấp được.
        ///
        /// Vì sao chặn: nhập mã luôn mở một phiên MỚI (<c>StartSessionAsync</c> không nối lại phiên cũ),
        /// nên mã thứ hai là phòng chờ thứ hai cho cùng một người — HM thấy hai ứng viên, AI chấm hai
        /// lần. Phiên hỏng giữa chừng (<c>aborted</c>/<c>error</c>) thì KHÔNG chặn, để vào lại được.
        /// </summary>
        public static async Task<string?> EnteredRoomReasonAsync(
            IUnitOfWork unitOfWork, Guid applicationId, int round, CancellationToken ct)
        {
            var sessions = (await unitOfWork.Repository<InterviewSession>().FindAsync(
                    s => s.ApplicationId == applicationId && s.RoundNumber == round && s.SessionType == "real", ct))
                .ToList();

            if (sessions.Any(s => InterviewSessionStatuses.Is(s.Status, InterviewSessionStatuses.Completed)))
                return $"Ứng viên đã phỏng vấn xong vòng {round} — không cần cấp mã.";

            if (sessions.Any(s => InterviewSessionStatuses.Is(s.Status, InterviewSessionStatuses.Waiting)
                                  || InterviewSessionStatuses.Is(s.Status, InterviewSessionStatuses.Active)))
                return $"Ứng viên đã nhập mã và đang ở phòng phỏng vấn vòng {round} — không cần cấp mã mới.";

            return null;
        }

        /// <summary>
        /// Mã của vòng này có được HIỆN cho ứng viên trong Portal không.
        ///
        /// Chỉ vòng làm TỪ NHÀ (sơ loại): ứng viên cần mã để tự mở phòng. Vòng TẠI VĂN PHÒNG (chuyên
        /// môn) thì Recruiter đưa mã tận tay khi ứng viên đã tới — hiện nó trong Portal là cho phép vào
        /// phòng từ bất cứ đâu, đúng thứ "bắt buộc tại văn phòng" muốn ngăn. Vòng chưa khai loại được
        /// coi như tại văn phòng: không lộ mã khi không chắc.
        /// </summary>
        public static bool ShownToCandidate(string? roundType) =>
            InterviewInviteEmail.IsRemoteRound(roundType) && !InterviewInviteEmail.IsOnlineTest(roundType);
    }
}
