using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.OnlineTest
{
    /// <summary>
    /// Luật DUY NHẤT về độ lớn ngân hàng đề (ADR-049): tin có vòng <c>online_test</c> thì ngân hàng phải có ít
    /// nhất <see cref="JobPosting.OnlineTestQuestionsPerTest"/> câu. Mỗi lượt thi bốc đúng N câu — ngân hàng
    /// ít hơn N thì ứng viên nhận đề thiếu câu, mỗi câu gánh nhiều điểm hơn cấu hình.
    ///
    /// <para><b>Hai thời điểm áp luật.</b></para>
    /// <list type="bullet">
    /// <item>Rời bản nháp (gửi duyệt / đăng thẳng) — <c>UpdateJobStatusCommand</c> chặn cứng.</item>
    /// <item>Tin đã qua cổng đó (<see cref="IsEnforcedFor"/>): tăng số câu mỗi bài hoặc xoá câu không được
    /// kéo ngân hàng xuống dưới ngưỡng. Trước đây cổng chỉ đứng ở lối ra bản nháp, nên tin đang tuyển vẫn
    /// hạ được ngân hàng xuống dưới N sau khi đã đăng — và ứng viên kế tiếp nhận đề thiếu câu.</item>
    /// </list>
    /// Ở bản nháp chỉ CẢNH BÁO (màn tin + màn ngân hàng đề): khai số câu mỗi bài trước rồi mới nhập câu hỏi
    /// là thứ tự làm việc hợp lệ, chặn ở đây là bắt người dùng làm ngược.
    /// </summary>
    public static class OnlineTestBankGate
    {
        /// <summary>Mã lỗi — giao diện dịch theo mã (<c>resolveApiError</c>) thay vì câu tiếng Việt của server.</summary>
        public const string InsufficientCode = "online_test_bank_insufficient";

        /// <summary>Ảnh chụp ngân hàng đề so với cấu hình bài thi của một tin có vòng trắc nghiệm.</summary>
        public sealed record Snapshot(IReadOnlyList<int> RoundNumbers, int QuestionCount, int Required)
        {
            public bool IsSufficient => QuestionCount >= Required;
        }

        /// <summary>Số câu tối thiểu ngân hàng phải có. Cấu hình hỏng (≤ 0) vẫn đòi ít nhất 1 câu.</summary>
        public static int RequiredFor(int questionsPerTest) => questionsPerTest > 0 ? questionsPerTest : 1;

        /// <summary>
        /// Tin đã rời bản nháp — luật giữ nguyên với mọi thay đổi ngân hàng / cấu hình. <c>closed</c> vẫn tính:
        /// ứng viên đã được xếp ca thi trước lúc đóng tin vẫn vào thi, và mở lại tin đi lại qua cổng đăng.
        /// </summary>
        public static bool IsEnforcedFor(string? jobStatus) =>
            (jobStatus ?? string.Empty).Trim().ToLowerInvariant() is "pending" or "active" or "closed";

        /// <summary>Null khi tin không có vòng trắc nghiệm nào — luật không áp.</summary>
        public static async Task<Snapshot?> EvaluateAsync(IUnitOfWork uow, JobPosting job, CancellationToken ct)
        {
            var rounds = await uow.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == job.Id, ct);

            var onlineTestRounds = rounds
                .Where(r => string.Equals((r.RoundType ?? string.Empty).Trim(), "online_test", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.RoundNumber)
                .OrderBy(n => n)
                .ToList();
            if (onlineTestRounds.Count == 0) return null;

            var questionCount = await uow.Repository<OnlineTestQuestion>()
                .CountAsync(q => q.JobPostingId == job.Id, ct);

            return new Snapshot(onlineTestRounds, questionCount, RequiredFor(job.OnlineTestQuestionsPerTest));
        }

        /// <summary>Câu báo khi tin rời bản nháp mà ngân hàng chưa đủ câu.</summary>
        public static string LeaveDraftMessage(Snapshot s)
        {
            var roundLabel = string.Join(", ", s.RoundNumbers.Select(n => $"vòng {n}"));
            return s.QuestionCount == 0
                ? $"Tin này có vòng trắc nghiệm ({roundLabel}) nhưng ngân hàng đề đang trống. "
                  + $"Hãy thêm ít nhất {s.Required} câu hỏi (mỗi lượt thi bốc {s.Required} câu) trong mục Ngân hàng câu hỏi của tin rồi gửi duyệt lại."
                : $"Ngân hàng đề của tin mới có {s.QuestionCount} câu, chưa đủ {s.Required} câu cho mỗi lượt thi của vòng trắc nghiệm ({roundLabel}). "
                  + "Hãy thêm câu hỏi hoặc giảm số câu mỗi bài trong mục Ngân hàng câu hỏi của tin rồi gửi duyệt lại.";
        }
    }
}
