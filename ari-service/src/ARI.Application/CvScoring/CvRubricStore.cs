using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Domain.Entities;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Đọc bộ tiêu chí chấm CV ĐANG SỐNG của tin (ADR-070) — nguồn sự thật duy nhất cho câu hỏi "tin này
    /// chấm CV theo gì".
    ///
    /// Chỉ đọc bộ cấp TIN. Bộ cấp công ty không còn là đường lùi (nay nó là MẪU để HM chép), và bộ cấp vòng
    /// vô nghĩa với CV — CV được chấm một lần cho cả tin, không theo vòng.
    /// </summary>
    public static class CvRubricStore
    {
        public static async Task<PlaybookDocument?> LiveAsync(IUnitOfWork uow, Guid jobPostingId, CancellationToken ct = default)
        {
            var docs = await uow.Repository<PlaybookDocument>().FindAsync(
                p => p.DeletedAt == null
                     && p.Scope == PlaybookScope.ScopeJobPosting
                     && p.ScopeRefId == jobPostingId
                     && p.DocumentType == ScoringRubric.TypeCvRubric
                     && p.RubricJson != null,
                ct);
            // Index duy nhất bảo đảm chỉ có một; sắp xếp vẫn giữ để dữ liệu cũ (trước index) ra kết quả xác định.
            return docs.OrderByDescending(d => d.CreatedAt).FirstOrDefault();
        }

        public static async Task<bool> HasLiveAsync(IUnitOfWork uow, Guid jobPostingId, CancellationToken ct = default)
            => await uow.Repository<PlaybookDocument>().CountAsync(
                p => p.DeletedAt == null
                     && p.Scope == PlaybookScope.ScopeJobPosting
                     && p.ScopeRefId == jobPostingId
                     && p.DocumentType == ScoringRubric.TypeCvRubric
                     && p.RubricJson != null,
                ct) > 0;

        /// <summary>Bộ tiêu chí sống của nhiều tin một lượt: jobId → id tài liệu.</summary>
        public static async Task<Dictionary<Guid, Guid>> LiveIdsByJobAsync(
            IUnitOfWork uow, IReadOnlyCollection<Guid>? jobIds, CancellationToken ct = default)
        {
            var rows = await uow.Repository<PlaybookDocument>().QueryAsync(
                q => q.Where(p => p.DeletedAt == null
                                  && p.Scope == PlaybookScope.ScopeJobPosting
                                  && p.DocumentType == ScoringRubric.TypeCvRubric
                                  && p.RubricJson != null
                                  && p.ScopeRefId != null
                                  && (jobIds == null || jobIds.Contains(p.ScopeRefId!.Value)))
                      .Select(p => new { JobId = p.ScopeRefId!.Value, p.Id, p.CreatedAt }),
                ct);

            return rows
                .GroupBy(r => r.JobId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First().Id);
        }

        public static List<RubricCriterion> Criteria(PlaybookDocument? doc)
            => doc == null ? new List<RubricCriterion>() : ScoringRubric.Deserialize(doc.RubricJson);

        /// <summary>Công thức cấp tin đi cùng phiên bản bộ tiêu chí (ADR-075); không khai = mặc định.</summary>
        public static CvScoringPolicy Policy(PlaybookDocument? doc) => CvScoringPolicy.FromStorage(doc?.ScoringPolicyJson);
    }
}
