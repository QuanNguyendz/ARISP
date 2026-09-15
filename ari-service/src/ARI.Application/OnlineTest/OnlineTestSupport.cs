using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.OnlineTest
{
    /// <summary>Helpers dùng chung của feature Online Test (ngân hàng câu hỏi + chấm bài).</summary>
    internal static class OnlineTestSupport
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Ai được sở hữu ngân hàng đề của một tin: chủ tin, quản trị viên, **và Hiring Manager của
        /// tin đó**.
        ///
        /// <b>Vì sao HM nằm trong danh sách này còn các cổng khác thì không.</b> Ngân hàng đề là một
        /// quyết định CHUYÊN MÔN — hỏi gì, đáp án nào đúng, bao nhiêu điểm là đạt — đúng thứ mà
        /// ADR-061 giao cho Hiring Manager. Recruiter vận hành phễu chứ không ra đề. Trước đây cổng
        /// này dùng thẳng mức `Owner` của <see cref="JobAccess"/>, nên người chịu trách nhiệm về nội
        /// dung bài thi lại là người duy nhất không sửa được nó.
        ///
        /// Đây KHÔNG phải nới lỏng <see cref="JobAccess"/>: mức quyền chung giữ nguyên, chỉ riêng
        /// feature này mở thêm một lối vào có tên. Mở ở `JobAccess` sẽ kéo theo cả sửa tin, xoá tin,
        /// xếp lịch — những việc HM cố ý không được làm.
        /// </summary>
        public static async Task<(bool ok, JobPosting? job)> CanManageAsync(
            IUnitOfWork uow, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
        {
            var (ok, job) = await JobAccess.CanManageAsync(uow, jobPostingId, userId, role, ct);
            if (ok || job == null) return (ok, job);

            if (userId is not { } uid || uid == Guid.Empty) return (false, job);

            var hm = await JobAccess.PrimaryHiringManagerAsync(uow, jobPostingId, ct);
            return (hm != null && hm.UserId == uid, job);
        }

        /// <summary>
        /// Ngôn ngữ đã cấu hình cho vòng trắc nghiệm của job ("vi"/"en"), null nếu tin không có vòng
        /// trắc nghiệm nào hoặc chưa đặt ngôn ngữ. Lấy vòng trắc nghiệm ĐẦU TIÊN theo số vòng.
        /// </summary>
        public static async Task<string?> GetOnlineTestLanguageAsync(
            IUnitOfWork uow, Guid jobPostingId, CancellationToken ct)
        {
            var rounds = await uow.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == jobPostingId, ct);

            var round = rounds
                .Where(r => string.Equals(r.RoundType, "online_test", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.RoundNumber)
                .FirstOrDefault();

            return OnlineTestLanguageGuard.Normalize(round?.InterviewLanguage);
        }

        /// <summary>Xác thực ứng viên đăng nhập sở hữu hồ sơ (theo account id hoặc email claim, có auto-link).</summary>
        public static async Task<(bool ok, ARI.Domain.Entities.Application? app)> AuthorizeCandidateAsync(
            IUnitOfWork uow, Guid applicationId, Guid candidateAccountId, string? email, CancellationToken ct)
        {
            var app = await uow.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(applicationId, ct);
            if (app == null) return (false, null);

            bool byAccount = app.CandidateAccountId == candidateAccountId;
            bool byEmail = !string.IsNullOrEmpty(email) &&
                           string.Equals(app.CandidateEmail, email, StringComparison.OrdinalIgnoreCase);

            // Auto-link hồ sơ chưa gắn account (đồng bộ với các handler portal khác).
            if (!byAccount && !app.CandidateAccountId.HasValue && byEmail)
            {
                app.CandidateAccountId = candidateAccountId;
                uow.Repository<ARI.Domain.Entities.Application>().Update(app);
                await uow.SaveChangesAsync(ct);
                byAccount = true;
            }

            return (byAccount || byEmail, app);
        }

        /// <summary>
        /// Hồ sơ đã qua vòng duyệt CV chưa (đủ điều kiện làm bài thi trắc nghiệm).
        /// Chặn: cv_submitted (chưa duyệt), hm_review (chờ Hiring Manager duyệt), cv_rejected
        /// (bị loại CV), withdrawn (đã rút) và mọi trạng thái lạ khác — xem
        /// <see cref="ApplicationStatuses.CvPassed"/>.
        /// </summary>
        public static bool IsCvPassed(string? status) => ApplicationStatuses.IsCvPassed(status);

        /// <summary>Vòng (RoundNumber) được cấu hình là online_test cho job — mặc định 1 nếu không có.</summary>
        public static async Task<int> ResolveRoundAsync(IUnitOfWork uow, Guid jobPostingId, CancellationToken ct)
        {
            var configs = await uow.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == jobPostingId, ct);
            var online = configs.FirstOrDefault(c => c.RoundType != null && c.RoundType.ToLower() == "online_test");
            return online?.RoundNumber ?? 1;
        }

        /// <summary>
        /// Cửa vào phòng thi mở trong bao lâu kể từ giờ hẹn.
        ///
        /// Bài thi có giờ hẹn như mọi vòng khác, nhưng khác buổi phỏng vấn ở chỗ không ai ngồi đợi:
        /// ứng viên vào muộn thì chẳng có ai để mà lỡ. Vẫn phải có cửa đóng, nếu không "giờ hẹn" chỉ
        /// là trang trí và người thi tuần sau vẫn vào được cùng một đề.
        /// </summary>
        public static readonly TimeSpan EntryWindow = TimeSpan.FromHours(1);

        /// <summary>
        /// Độ trễ mạng được tính thêm vào hạn nộp — bài nộp lúc hết đồng hồ (hoặc lúc đóng trang)
        /// vẫn phải đi hết đường truyền mới tới server.
        /// </summary>
        public static readonly TimeSpan SubmitGrace = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Hạn chót của bài thi: sau giờ này không còn ai đang làm bài hợp lệ.
        ///
        /// Không phải <c>giờ đóng cửa</c> (<see cref="EntryWindow"/>): người vào ở phút thứ 59 vẫn
        /// còn nguyên đồng hồ làm bài của mình. Người vào muộn nhất cũng phải xong trước
        /// <c>giờ đóng cửa + thời lượng bài</c> — nên chỉ sau mốc đó, "chưa có bài" mới chắc chắn là
        /// "không vào làm", và hệ thống mới được nộp thay (xem <see cref="OnlineTestExpiry"/>).
        /// </summary>
        public static DateTimeOffset SubmissionDeadline(DateTimeOffset opensAt, int durationMinutes) =>
            opensAt + EntryWindow + TimeSpan.FromMinutes(Math.Max(0, durationMinutes)) + SubmitGrace;

        /// <summary>
        /// Chấm một bài trên ĐÚNG bộ đề đã bốc cho ứng viên.
        ///
        /// Một câu đúng khi tập đáp án chọn KHỚP HOÀN TOÀN tập đáp án đúng (cả câu một lựa chọn lẫn
        /// nhiều lựa chọn); câu bỏ trắng là sai. Dùng chung cho bài ứng viên nộp và bài hệ thống
        /// nộp thay khi hết hạn — hai đường chấm riêng thì sớm muộn sẽ chấm khác nhau.
        /// </summary>
        public static (int Correct, int Total, decimal Score) Grade(
            IReadOnlyList<OnlineTestQuestion> drawn, IReadOnlyDictionary<Guid, List<int>> answers)
        {
            int correct = drawn.Count(q =>
            {
                var correctSet = ParseInts(q.CorrectOptions).ToHashSet();
                if (correctSet.Count == 0) return false;
                answers.TryGetValue(q.Id, out var picked);
                return (picked ?? new List<int>()).ToHashSet().SetEquals(correctSet);
            });

            int total = drawn.Count;
            decimal score = total > 0 ? Math.Round((decimal)correct / total * 100m, 2) : 0m;
            return (correct, total, score);
        }

        /// <summary>
        /// Giờ hẹn làm bài của một hồ sơ ở vòng <paramref name="round"/>, hoặc <c>null</c> khi nhân
        /// sự chưa xếp ca.
        ///
        /// Đọc theo DÒNG booking đang giữ chỗ (ADR-058) — cột <c>booked_count</c> không nói được ca
        /// nào là của AI.
        /// </summary>
        public static async Task<DateTimeOffset?> ScheduledStartAsync(
            IUnitOfWork uow, Guid applicationId, int round, CancellationToken ct)
        {
            var booking = (await uow.Repository<InterviewBooking>().FindAsync(
                    b => b.ApplicationId == applicationId
                         && b.RoundNumber == round
                         && b.Status == BookingStatus.Scheduled, ct))
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefault();
            if (booking == null) return null;

            var slot = await uow.Repository<AvailabilitySlot>().GetByIdAsync(booking.AvailabilitySlotId, ct);
            return slot?.StartTime;
        }

        public static List<string> ParseOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        public static List<int> ParseInts(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<int>();
            try { return JsonSerializer.Deserialize<List<int>>(json, JsonOpts) ?? new List<int>(); }
            catch { return new List<int>(); }
        }

        public static string SerializeOptions(IEnumerable<string> options)
            => JsonSerializer.Serialize(options.Select(o => o.Trim()).ToList(), JsonOpts);

        public static string SerializeInts(IEnumerable<int> values)
            => JsonSerializer.Serialize(values.Distinct().OrderBy(v => v).ToList(), JsonOpts);

        public static string SerializeAnswers(Dictionary<Guid, List<int>> answers)
            => JsonSerializer.Serialize(answers, JsonOpts);

        /// <summary>
        /// Đọc lại bài làm đã lưu. Hỏng thì trả rỗng thay vì ném — một bản ghi cũ dạng khác không
        /// được làm chết cả màn xem bài (cùng cách <c>ParseOptions</c> xử lý dữ liệu cũ).
        /// </summary>
        public static Dictionary<Guid, List<int>> ParseAnswers(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<Guid, List<int>>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<Guid, List<int>>>(json, JsonOpts)
                       ?? new Dictionary<Guid, List<int>>();
            }
            catch
            {
                return new Dictionary<Guid, List<int>>();
            }
        }

        /// <summary>
        /// Bốc ngẫu nhiên <paramref name="n"/> câu từ ngân hàng — DETERMINISTIC theo (câu, hồ sơ, vòng):
        /// cùng ứng viên/vòng luôn nhận cùng bộ đề (không đổi khi refresh) và chấm lại được ở lúc nộp.
        /// Nếu ngân hàng ít hơn n thì lấy hết.
        /// </summary>
        public static List<OnlineTestQuestion> DrawQuestions(
            IEnumerable<OnlineTestQuestion> all, Guid applicationId, int round, int n)
        {
            var ordered = all.OrderBy(q => StableKey(q.Id, applicationId, round)).ThenBy(q => q.Id).ToList();
            return n > 0 && ordered.Count > n ? ordered.Take(n).ToList() : ordered;
        }

        /// <summary>Khóa sắp xếp ổn định (FNV-1a) từ (questionId, applicationId, round) — không phụ thuộc runtime hash.</summary>
        private static uint StableKey(Guid question, Guid application, int round)
        {
            Span<byte> buf = stackalloc byte[36];
            question.TryWriteBytes(buf.Slice(0, 16));
            application.TryWriteBytes(buf.Slice(16, 16));
            BitConverter.TryWriteBytes(buf.Slice(32, 4), round);

            uint hash = 2166136261u;
            foreach (var b in buf)
            {
                hash ^= b;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
