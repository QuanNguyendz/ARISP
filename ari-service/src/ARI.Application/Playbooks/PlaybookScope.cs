using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.Playbooks
{
    /// <summary>
    /// Phạm vi áp dụng của Playbook (ADR-025) — nguồn sự thật DUY NHẤT cho câu hỏi "tài liệu này có
    /// được dùng cho buổi phỏng vấn đang chạy không".
    ///
    /// Trước đây câu hỏi đó không được đặt ra ở đâu cả: cả .NET lẫn rag-service đều lấy TOÀN BỘ chunk
    /// playbook của hệ thống, nên ngân hàng câu hỏi của một vị trí lọt sang buổi phỏng vấn vị trí khác,
    /// và tài liệu đã xoá vẫn tiếp tục điều khiển AI. Phạm vi phải đọc từ bảng <c>playbook_documents</c>
    /// (có <c>deleted_at</c>) chứ không từ metadata của chunk — metadata là bản sao, xoá tài liệu không
    /// cập nhật được nó.
    /// </summary>
    public static class PlaybookScope
    {
        /// <summary>Áp cho mọi tin tuyển dụng — phong cách, văn hoá, ràng buộc pháp lý của doanh nghiệp.</summary>
        public const string ScopeOrg = "org";
        /// <summary>Chỉ áp cho đúng tin tuyển dụng (<c>ScopeRefId</c>).</summary>
        public const string ScopeJobPosting = "job_posting";
        /// <summary>Chỉ áp cho đúng tin + đúng vòng (<c>RoundNumber</c>).</summary>
        public const string ScopeRound = "round";

        /// <summary>Câu hỏi bắt buộc phải hỏi trước khi kết thúc phiên.</summary>
        public const string TypeMustAsk = "must_ask";
        /// <summary>Chủ đề CẤM hỏi (pháp lý). Đưa vào prompt như ràng buộc, KHÔNG phải ngữ cảnh tham khảo.</summary>
        public const string TypeCompliance = "compliance";
        /// <summary>Dấu hiệu cần đào sâu.</summary>
        public const string TypeRedFlag = "red_flag";
        /// <summary>Gợi ý câu trả lời tốt — dùng để chấm/đào sâu, không đọc cho ứng viên nghe.</summary>
        public const string TypeExpectedAnswer = "expected_answer";

        /// <summary>
        /// Id các playbook được phép dùng cho (tin, vòng) này: <c>org</c> áp cho mọi tin;
        /// <c>job_posting</c> phải đúng tin; <c>round</c> phải đúng cả tin lẫn vòng.
        /// <c>DeletedAt == null</c> viết TƯỜNG MINH dù EF đã có global query filter cho
        /// <see cref="ISoftDelete"/>: đây là luật nghiệp vụ ("playbook đã xoá không được nói gì nữa"),
        /// không nên phụ thuộc vào một cấu hình ở tầng khác — và nhờ vậy test được bằng kho in-memory.
        /// </summary>
        public static async Task<List<Guid>> EligibleDocumentIdsAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int roundNumber, CancellationToken ct = default)
        {
            var docs = await unitOfWork.Repository<PlaybookDocument>().QueryAsync(
                q => q.Where(p =>
                        p.DeletedAt == null
                        && (p.Scope == ScopeOrg
                        || (p.ScopeRefId == jobPostingId
                            && (p.Scope == ScopeJobPosting
                                || (p.Scope == ScopeRound && p.RoundNumber == roundNumber)))))
                      .Select(p => p.Id),
                ct);

            return docs.ToList();
        }

        /// <summary>
        /// Tài liệu <c>must_ask</c> của (tin, vòng). Gồm cả scope <c>round</c> — bản cũ chỉ đọc
        /// <c>job_posting</c> nên must-ask khai theo vòng không bao giờ được nạp.
        /// Cố ý KHÔNG lấy scope <c>org</c>: câu bắt buộc là chuyện của từng vị trí.
        /// </summary>
        public static async Task<List<PlaybookDocument>> MustAskDocumentsAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int roundNumber, CancellationToken ct = default)
        {
            var docs = await unitOfWork.Repository<PlaybookDocument>().FindAsync(
                p => p.DeletedAt == null
                     && p.DocumentType == TypeMustAsk
                     && p.ScopeRefId == jobPostingId
                     && (p.Scope == ScopeJobPosting
                         || (p.Scope == ScopeRound && p.RoundNumber == roundNumber)),
                ct);

            return docs.ToList();
        }

        /// <summary>
        /// Bộ tiêu chí chấm điểm áp dụng cho (tin, vòng). Thứ tự ưu tiên: <b>vòng → tin → công ty</b>
        /// — vòng chuyên môn có thể có bộ riêng, không khai thì dùng bộ của tin, không nữa thì bộ chung
        /// của doanh nghiệp. Trả về danh sách rỗng nếu chưa khai bộ nào (nơi gọi tự quyết định fallback).
        /// </summary>
        public static async Task<List<RubricCriterion>> ResolveRubricAsync(
            IUnitOfWork unitOfWork, Guid jobPostingId, int roundNumber, string documentType,
            CancellationToken ct = default)
        {
            var docs = (await unitOfWork.Repository<PlaybookDocument>().FindAsync(
                p => p.DeletedAt == null
                     && p.DocumentType == documentType
                     && p.RubricJson != null
                     && (p.Scope == ScopeOrg
                         || (p.ScopeRefId == jobPostingId
                             && (p.Scope == ScopeJobPosting
                                 || (p.Scope == ScopeRound && p.RoundNumber == roundNumber)))),
                ct)).ToList();

            if (docs.Count == 0) return new List<RubricCriterion>();

            var chosen = docs.FirstOrDefault(d => d.Scope == ScopeRound)
                         ?? docs.FirstOrDefault(d => d.Scope == ScopeJobPosting)
                         ?? docs.OrderByDescending(d => d.CreatedAt).First();

            return ScoringRubric.Deserialize(chosen.RubricJson);
        }

        // Đầu dòng markdown/liệt kê: "- ", "* ", "+ ", "1. ", "1) ", "•".
        private static readonly Regex BulletPrefix = new(@"^\s*([-*+•]|\d+[.)])\s+", RegexOptions.Compiled);

        /// <summary>Câu bắt buộc ngắn nhất được chấp nhận — dưới mức này gần như chắc chắn là rác.</summary>
        private const int MinQuestionLength = 8;

        /// <summary>
        /// Tách nội dung tài liệu must_ask thành danh sách câu hỏi.
        ///
        /// Bản cũ tách theo <c>\n</c> và <c>;</c> rồi lấy tất: tiêu đề markdown, dòng trống, dấu gạch
        /// đầu dòng đều thành "câu hỏi bắt buộc" — mà must-ask là điều kiện CHẶN kết thúc phiên, nên
        /// mỗi dòng rác là một câu AI buộc phải hỏi ứng viên.
        ///
        /// Nay: mỗi dòng một câu (không tách theo <c>;</c> nữa — dấu chấm phẩy nằm giữa câu là chuyện
        /// bình thường), bỏ tiêu đề, gỡ bullet, bỏ dòng quá ngắn, khử trùng lặp không phân biệt hoa thường.
        /// </summary>
        public static List<string> ParseMustAskLines(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Replace("\r", string.Empty).Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#")) continue;                    // tiêu đề markdown
                if (line.All(c => !char.IsLetterOrDigit(c))) continue;  // đường kẻ "---", "===", "***"

                line = BulletPrefix.Replace(line, string.Empty).Trim();
                if (line.Length < MinQuestionLength) continue;

                if (seen.Add(line)) result.Add(line);
            }

            return result;
        }

        /// <summary>Chunk thuộc loại CẤM hỏi (đọc từ metadata JSON của chunk).</summary>
        public static bool IsComplianceType(string? documentType) =>
            string.Equals(documentType?.Trim(), TypeCompliance, StringComparison.OrdinalIgnoreCase);
    }
}
