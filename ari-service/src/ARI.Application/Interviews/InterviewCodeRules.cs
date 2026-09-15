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
        /// <summary>Lý do không cấp mã cho vòng trắc nghiệm (ADR-049: bài thi làm trong Portal).</summary>
        public static string OnlineTestReason(int round) =>
            $"Vòng {round} là bài trắc nghiệm làm trực tuyến — không dùng mã phỏng vấn.";

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
