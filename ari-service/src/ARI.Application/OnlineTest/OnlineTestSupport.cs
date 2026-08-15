using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.OnlineTest
{
    /// <summary>Helpers dùng chung của feature Online Test (ngân hàng câu hỏi + chấm bài).</summary>
    internal static class OnlineTestSupport
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        /// <summary>Kiểm tra staff có quyền quản lý câu hỏi của job này không (chủ tin hoặc admin).</summary>
        public static async Task<(bool ok, JobPosting? job)> CanManageAsync(
            IUnitOfWork uow, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
        {
            var job = await uow.Repository<JobPosting>().GetByIdAsync(jobPostingId, ct);
            if (job == null) return (false, null);
            if (userId is not { } uid || uid == Guid.Empty) return (false, job);
            var isAdmin = role == AppRoles.SuperAdmin || role == AppRoles.HrAdmin;
            return (isAdmin || job.CreatedByUserId == uid, job);
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
        /// Các trạng thái hồ sơ được coi là "CV đã pass" (đã qua vòng duyệt CV) — điều kiện để làm bài thi trắc nghiệm.
        /// Chặn: cv_submitted (chưa duyệt), cv_rejected (bị loại CV), withdrawn (đã rút) và mọi trạng thái lạ khác.
        /// </summary>
        private static readonly HashSet<string> CvPassedStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "invited", "screening", "interview", "pass", "not_pass" };

        /// <summary>Hồ sơ đã qua vòng duyệt CV chưa (đủ điều kiện làm bài thi trắc nghiệm).</summary>
        public static bool IsCvPassed(string? status) =>
            !string.IsNullOrWhiteSpace(status) && CvPassedStatuses.Contains(status);

        /// <summary>Vòng (RoundNumber) được cấu hình là online_test cho job — mặc định 1 nếu không có.</summary>
        public static async Task<int> ResolveRoundAsync(IUnitOfWork uow, Guid jobPostingId, CancellationToken ct)
        {
            var configs = await uow.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == jobPostingId, ct);
            var online = configs.FirstOrDefault(c => c.RoundType != null && c.RoundType.ToLower() == "online_test");
            return online?.RoundNumber ?? 1;
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
