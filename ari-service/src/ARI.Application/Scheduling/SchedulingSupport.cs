using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Scheduling
{
    /// <summary>Helpers dùng chung của feature Scheduling.</summary>
    internal static class SchedulingSupport
    {
        /// <summary>
        /// Kiểm tra staff có quyền quản lý slot của job này không (chủ tin hoặc admin).
        /// Vị từ thật nằm ở <see cref="JobAccess"/> — trước đây file này và
        /// <c>OnlineTestSupport</c> giữ hai bản giống hệt nhau từng byte, nên thêm một mức quyền
        /// mới vào một bản sẽ lặng lẽ tách đôi hành vi (sửa được ca phỏng vấn nhưng không xem
        /// được ngân hàng câu hỏi của cùng một tin).
        /// </summary>
        public static Task<(bool ok, JobPosting? job)> CanManageAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
            => JobAccess.CanManageAsync(unitOfWork, jobPostingId, userId, role, ct);

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

        /// <summary>
        /// Ca mới/ca vừa sửa có CHỒNG GIỜ với ca nào khác của cùng tin không (ADR-067).
        /// Trả thông báo lỗi, hoặc <c>null</c> nếu không đụng ai.
        ///
        /// <b>Vì sao xét cả tin chứ không chỉ cùng vòng.</b> Buổi phỏng vấn thật có Hiring Manager
        /// ngồi cùng AI, mà mỗi tin chỉ có MỘT Hiring Manager — hai ca chồng giờ nghĩa là bắt họ ở
        /// hai phòng cùng lúc, dù đó là hai vòng khác nhau.
        ///
        /// <paramref name="excludeSlotId"/>: khi SỬA giờ, chính ca đang sửa không được tính là đụng
        /// với bản thân nó.
        /// </summary>
        public static async Task<string?> ValidateSlotTimeAsync(
            IUnitOfWork unitOfWork,
            Guid jobPostingId,
            DateTimeOffset start,
            DateTimeOffset end,
            Guid? excludeSlotId,
            CancellationToken ct)
        {
            var siblings = (await unitOfWork.Repository<AvailabilitySlot>()
                    .FindAsync(s => s.JobPostingId == jobPostingId, ct))
                .Where(s => s.Id != excludeSlotId)
                .ToList();

            // Chồng lấn = giao nhau thật sự. Ca 14:00–15:00 và 15:00–16:00 nối đuôi nhau thì KHÔNG
            // tính là đụng: biên trùng nhau là cách xếp ca liên tiếp bình thường.
            var clash = siblings.FirstOrDefault(s => start < s.EndTime && s.StartTime < end);
            if (clash == null) return null;

            var from = clash.StartTime.ToOffset(TimeSpan.FromHours(7));
            var to = clash.EndTime.ToOffset(TimeSpan.FromHours(7));
            return $"Khung giờ này chồng lên ca đã có ({from:HH:mm}–{to:HH:mm} ngày {from:dd/MM/yyyy} giờ VN). "
                   + "Hiring Manager dự cùng ứng viên nên hai ca không được trùng giờ.";
        }

        /// <summary>
        /// Ba luật chống xếp lịch hỏng (ADR-067). Trả về thông báo lỗi, hoặc <c>null</c> nếu xếp được.
        ///
        /// Nằm ở ĐÂY chứ không trong từng handler vì có ba đường cùng ghi một booking — gán ca, dời
        /// một người, dời cả nhóm. Viết riêng ở ba nơi thì luật thứ tư thêm sau sẽ chỉ vào được hai.
        ///
        /// <paramref name="excludeBookingId"/>: khi DỜI lịch, chính booking đang dời không được tính
        /// là "đã có lịch trùng giờ" với bản thân nó.
        /// </summary>
        public static async Task<string?> ValidateAssignmentAsync(
            IUnitOfWork unitOfWork,
            ARI.Domain.Entities.Application application,
            AvailabilitySlot slot,
            int roundNumber,
            Guid? excludeBookingId,
            CancellationToken ct)
        {
            // Vòng TRẮC NGHIỆM là một ngoại lệ có thật, không phải kẽ hở: ứng viên làm bài trực
            // tuyến TẠI NHÀ (ADR-049), không có Hiring Manager ngồi cùng, không có phòng. Mọi luật
            // dưới đây sinh ra từ sự có mặt của HM nên không áp được — trừ luật 2 (một con người
            // không thể vừa thi vừa phỏng vấn ở nơi khác), luật đó vẫn giữ.
            var roundType = (await unitOfWork.Repository<InterviewRoundConfig>().FindAsync(
                    r => r.JobPostingId == slot.JobPostingId && r.RoundNumber == roundNumber, ct))
                .FirstOrDefault()?.RoundType;
            var isOnlineTest = InterviewInviteEmail.IsOnlineTest(roundType);

            // ---- Luật 1: MỘT ca = MỘT ứng viên ----------------------------------------------
            // Buổi phỏng vấn có Hiring Manager ngồi cùng AI, nên một ca không thể phục vụ hai người.
            // Kiểm theo DÒNG booking chứ không theo cột `booked_count`: cột là khoá tương tranh, dòng
            // mới là sự thật (bài học ADR-058 — hai nguồn thì sẽ có ngày lệch).
            var onSlot = (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => b.AvailabilitySlotId == slot.Id && b.Status == BookingStatus.Scheduled, ct))
                .Where(b => b.Id != excludeBookingId && b.ApplicationId != application.Id)
                .ToList();
            if (!isOnlineTest && onSlot.Count > 0)
                return "Khung giờ này đã có ứng viên khác. Mỗi ca phỏng vấn chỉ nhận một ứng viên.";

            // ---- Luật 2: ứng viên không dự hai buổi cùng lúc ---------------------------------
            // Kể cả ở tin KHÁC: cùng một con người thì không thể ngồi hai phòng. Khác giờ thì được,
            // nên vị từ là CHỒNG LẤN thời gian chứ không phải "đã có lịch".
            var siblingIds = await SameCandidateApplicationIdsAsync(unitOfWork, application, ct);
            if (siblingIds.Count > 0)
            {
                var others = (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                        b => siblingIds.Contains(b.ApplicationId) && b.Status == BookingStatus.Scheduled, ct))
                    .Where(b => b.Id != excludeBookingId
                                && !(b.ApplicationId == application.Id && b.RoundNumber == roundNumber))
                    .ToList();

                if (others.Count > 0)
                {
                    var otherSlotIds = others.Select(b => b.AvailabilitySlotId).Distinct().ToList();
                    var otherSlots = await unitOfWork.Repository<AvailabilitySlot>()
                        .FindAsync(s => otherSlotIds.Contains(s.Id), ct);

                    var clash = otherSlots.FirstOrDefault(
                        s => slot.StartTime < s.EndTime && s.StartTime < slot.EndTime);
                    if (clash != null)
                    {
                        var when = clash.StartTime.ToOffset(TimeSpan.FromHours(7));
                        return $"Ứng viên đã có buổi phỏng vấn khác trùng khung giờ này "
                               + $"({when:HH:mm} ngày {when:dd/MM/yyyy} giờ VN). Hãy chọn khung giờ khác.";
                    }
                }
            }

            // ---- Luật 3: ca phải nằm trong giờ Hiring Manager có mặt được --------------------
            // Chỉ áp khi tin ĐÃ gán Hiring Manager: tin chưa có ai thì không có ràng buộc nào để áp,
            // và hành vi giữ y như trước ADR-067. Vòng trắc nghiệm cũng thoát: HM không dự bài thi.
            if (isOnlineTest) return null;

            var hm = await JobAccess.PrimaryHiringManagerAsync(unitOfWork, slot.JobPostingId, ct);
            if (hm == null) return null;

            var windows = await HmAvailabilitySupport.ActiveWindowsAsync(
                unitOfWork, slot.JobPostingId, roundNumber, ct);
            if (windows.Count == 0)
                return "Hiring Manager chưa gửi khung giờ có thể tham gia phỏng vấn cho vòng này. "
                       + "Hãy đề nghị họ gửi lịch rảnh trước khi xếp ca.";

            if (!HmAvailabilitySupport.IsCovered(windows, slot.StartTime, slot.EndTime))
                return "Khung giờ này nằm ngoài lịch rảnh của Hiring Manager. "
                       + $"Họ có mặt được: {HmAvailabilitySupport.Describe(windows)}.";

            // ---- Luật 4: chính Hiring Manager cũng không dự hai buổi cùng lúc ----------------
            // Luật 1 chỉ chặn trùng TRONG một ca, `ValidateSlotTimeAsync` chỉ chặn trùng trong CÙNG
            // một tin. Nhưng một người có thể là HM của nhiều tin, và họ vẫn chỉ có một mặt: hai ca
            // ở hai tin khác nhau, trùng giờ, hai ứng viên khác nhau — cả hai đều lọt.
            var hmJobIds = (await unitOfWork.Repository<JobHiringTeamMember>().FindAsync(
                    m => m.UserId == hm.UserId
                         && m.IsPrimary
                         && m.RoleOnJob == JobTeamRoles.HiringManager, ct))
                .Select(m => m.JobPostingId)
                .Where(id => id != slot.JobPostingId)
                .Distinct()
                .ToList();

            if (hmJobIds.Count == 0) return null;

            var hmSlotIds = (await unitOfWork.Repository<AvailabilitySlot>().FindAsync(
                    s => hmJobIds.Contains(s.JobPostingId)
                         && slot.StartTime < s.EndTime && s.StartTime < slot.EndTime, ct))
                .Select(s => s.Id)
                .ToList();
            if (hmSlotIds.Count == 0) return null;

            var hmClash = (await unitOfWork.Repository<InterviewBooking>().FindAsync(
                    b => hmSlotIds.Contains(b.AvailabilitySlotId)
                         && b.Status == BookingStatus.Scheduled, ct))
                .Any(b => b.Id != excludeBookingId);

            return hmClash
                ? "Hiring Manager đã có buổi phỏng vấn khác trùng khung giờ này ở một tin tuyển dụng "
                  + "khác. Hãy chọn khung giờ khác."
                : null;
        }

        /// <summary>
        /// Mọi hồ sơ của CÙNG MỘT NGƯỜI (kể cả hồ sơ đang xét), để đối chiếu lịch chéo giữa các tin.
        /// Nhận diện bằng tài khoản ứng viên nếu có; hồ sơ nộp không đăng nhập thì đối chiếu email.
        /// </summary>
        private static async Task<List<Guid>> SameCandidateApplicationIdsAsync(
            IUnitOfWork unitOfWork, ARI.Domain.Entities.Application application, CancellationToken ct)
        {
            var repo = unitOfWork.Repository<ARI.Domain.Entities.Application>();

            if (application.CandidateAccountId is Guid accountId && accountId != Guid.Empty)
                return (await repo.FindAsync(a => a.CandidateAccountId == accountId, ct))
                    .Select(a => a.Id).ToList();

            var email = (application.CandidateEmail ?? string.Empty).Trim().ToLowerInvariant();
            if (email.Length == 0) return new List<Guid> { application.Id };

            return (await repo.FindAsync(
                    a => a.CandidateEmail != null && a.CandidateEmail.ToLower() == email, ct))
                .Select(a => a.Id).ToList();
        }
    }
}
