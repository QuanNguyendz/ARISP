using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Playbooks
{
    /// <summary>
    /// Ai được viết playbook ở phạm vi nào, và tài liệu nào hợp lệ để nạp (ADR-025 · ADR-060 · ADR-069).
    ///
    /// <b>Playbook CÔNG TY</b> (<c>org</c>) áp cho mọi tin — văn phong, văn hoá, ràng buộc pháp lý — nên chỉ
    /// HR Leader / Super Admin viết. <b>Playbook THEO TIN</b> (<c>job_posting</c> / <c>round</c>) là nội dung
    /// CHUYÊN MÔN của vị trí — hỏi gì, đáp án tốt trông ra sao, câu nào bắt buộc, chấm theo tiêu chí nào —
    /// nên thuộc về Hiring Manager chính của tin; quản trị viên vẫn làm được (HM nghỉ, HM bị khoá).
    /// Recruiter chủ tin chỉ đọc: người vận hành phễu không quyết định AI hỏi gì.
    ///
    /// Luật nằm ở ĐÂY (tầng Application) chứ không ở controller: tin đi vào qua hai cửa (màn tin và màn
    /// Playbook công ty) — kiểm ở controller là hai bản luật, sửa một quên một.
    /// </summary>
    public static class PlaybookAccess
    {
        /// <summary>Trần kích thước file nạp vào (không phải trần của parser — trần của một tài liệu hợp lý).</summary>
        public const long MaxFileBytes = 15 * 1024 * 1024;

        /// <summary>
        /// Loại tài liệu hợp lệ. Loại tài liệu quyết định AI dùng nó THẾ NÀO (ADR-025: <c>compliance</c> là
        /// điều cấm, <c>must_ask</c> chặn kết thúc phiên…) nên một chuỗi lạ không được lọt vào — nó sẽ nằm
        /// trong kho tri thức mà không có luật nào biết xử lý.
        /// </summary>
        public static readonly IReadOnlySet<string> DocumentTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "style_guide", "question_bank", "competency_framework", "culture_guide",
            PlaybookScope.TypeCompliance, PlaybookScope.TypeRedFlag, "technical_scenario",
            PlaybookScope.TypeExpectedAnswer, PlaybookScope.TypeMustAsk, "round_playbook",
            ScoringRubric.TypeCvRubric, ScoringRubric.TypeInterviewRubric,
        };

        public const string ForbiddenOrg =
            "Chỉ HR Leader hoặc Super Admin quản lý playbook cấp công ty.";
        public const string ForbiddenJob =
            "Chỉ Hiring Manager chính của tin (hoặc HR Leader / Super Admin) được thêm hay xoá playbook của tin này.";

        /// <summary>Chuẩn hoá scope; null = không hợp lệ.</summary>
        public static string? NormalizeScope(string? scope)
        {
            var s = (scope ?? PlaybookScope.ScopeOrg).Trim().ToLowerInvariant();
            return s is PlaybookScope.ScopeOrg or PlaybookScope.ScopeJobPosting or PlaybookScope.ScopeRound ? s : null;
        }

        /// <summary>Phần mở rộng hợp lệ theo loại tài liệu: bộ tiêu chí là bảng Excel, còn lại là văn xuôi.</summary>
        public static string[] AllowedExtensions(string documentType) =>
            ScoringRubric.IsRubricType(documentType)
                ? new[] { ".xlsx" }
                : new[] { ".pdf", ".docx", ".txt", ".md" };

        /// <summary>
        /// Người gọi có được VIẾT (thêm/xoá) playbook ở phạm vi này không. Trả câu lỗi (kèm mã) hoặc null.
        /// Tin phải tồn tại và chưa lưu trữ — playbook cho một tin đã đóng hẳn là tài liệu không bao giờ dùng.
        /// </summary>
        public static async Task<(string? error, string? code)> CheckWriteAsync(
            IUnitOfWork uow, string scope, Guid? jobPostingId, Guid? userId, string? role, CancellationToken ct)
        {
            if (scope == PlaybookScope.ScopeOrg)
                return RoleNames.IsAdmin(role) ? (null, null) : (ForbiddenOrg, CommonErrorCodes.Forbidden);

            if (jobPostingId is not { } jobId || jobId == Guid.Empty)
                return ("Playbook theo tin phải chọn tin tuyển dụng.", null);

            var job = await uow.Repository<JobPosting>().GetByIdAsync(jobId, ct);
            if (job == null)
                return ("Không tìm thấy tin tuyển dụng.", CommonErrorCodes.NotFound);
            if (string.Equals(job.Status, "archived", StringComparison.OrdinalIgnoreCase))
                return ("Tin đã lưu trữ — không thêm hay xoá playbook được nữa.", CommonErrorCodes.Conflict);

            if (RoleNames.IsAdmin(role)) return (null, null);
            return await JobAccess.IsPrimaryHiringManagerAsync(uow, jobId, userId, ct)
                ? (null, null)
                : (ForbiddenJob, CommonErrorCodes.Forbidden);
        }

        /// <summary>
        /// Vòng của playbook phạm vi <c>round</c> phải là một vòng HỘI THOẠI có thật của tin: vòng trắc
        /// nghiệm không có AI phỏng vấn nên tài liệu gắn vào đó không bao giờ được đọc.
        /// </summary>
        public static async Task<string?> CheckRoundAsync(
            IUnitOfWork uow, Guid jobPostingId, int? roundNumber, CancellationToken ct)
        {
            if (roundNumber is not { } round || round < 1)
                return "Playbook theo vòng phải chọn vòng phỏng vấn.";

            var config = (await uow.Repository<InterviewRoundConfig>().FindAsync(
                r => r.JobPostingId == jobPostingId && r.RoundNumber == round, ct)).FirstOrDefault();
            if (config == null)
                return $"Tin này không có vòng {round}.";
            if (!InterviewRoundTypes.NeedsHiringManager(config.RoundType))
                return $"Vòng {round} là bài trắc nghiệm — không có buổi phỏng vấn nào để playbook điều khiển.";
            return null;
        }

        /// <summary>Có quyền viết playbook của tin này không — để giao diện hiện đúng nút.</summary>
        public static async Task<bool> CanManageJobAsync(
            IUnitOfWork uow, Guid jobPostingId, Guid? userId, string? role, CancellationToken ct)
            => RoleNames.IsAdmin(role) || await JobAccess.IsPrimaryHiringManagerAsync(uow, jobPostingId, userId, ct);
    }
}
