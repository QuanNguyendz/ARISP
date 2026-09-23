using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.Playbooks
{
    /// <summary>
    /// Đọc bộ tiêu chí chấm PHỎNG VẤN đang sống của tin (ADR-073) — nguồn sự thật duy nhất cho câu hỏi
    /// "buổi phỏng vấn vòng này chấm theo gì".
    ///
    /// Thứ tự: <b>bộ riêng của vòng → bộ chung của tin</b>. KHÔNG lùi về bộ công ty: Hiring Manager khai
    /// tiêu chí cho từng tin (quyết định 2026-09-19), bộ cấp công ty chỉ là MẪU để chép — giống bộ tiêu chí
    /// chấm CV (ADR-070). Trước đây có đường lùi đó nhưng production không có bộ công ty nào, nên mọi buổi
    /// phỏng vấn đều không ra báo cáo mà không ai biết vì sao.
    /// </summary>
    public static class InterviewRubricStore
    {
        /// <summary>Bộ chung của tin (áp mọi vòng hội thoại chưa có bộ riêng).</summary>
        public static async Task<PlaybookDocument?> JobLevelAsync(IUnitOfWork uow, Guid jobPostingId, CancellationToken ct = default)
            => (await uow.Repository<PlaybookDocument>().FindAsync(
                    p => p.DeletedAt == null
                         && p.Scope == PlaybookScope.ScopeJobPosting
                         && p.ScopeRefId == jobPostingId
                         && p.DocumentType == ScoringRubric.TypeInterviewRubric
                         && p.RubricJson != null,
                    ct))
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefault();

        /// <summary>Bộ riêng của một vòng (null = vòng dùng bộ chung).</summary>
        public static async Task<PlaybookDocument?> RoundLevelAsync(
            IUnitOfWork uow, Guid jobPostingId, int roundNumber, CancellationToken ct = default)
            => (await uow.Repository<PlaybookDocument>().FindAsync(
                    p => p.DeletedAt == null
                         && p.Scope == PlaybookScope.ScopeRound
                         && p.ScopeRefId == jobPostingId
                         && p.RoundNumber == roundNumber
                         && p.DocumentType == ScoringRubric.TypeInterviewRubric
                         && p.RubricJson != null,
                    ct))
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefault();

        /// <summary>Mọi bộ đang sống của tin (chung + từng vòng).</summary>
        public static async Task<List<PlaybookDocument>> LiveForJobAsync(IUnitOfWork uow, Guid jobPostingId, CancellationToken ct = default)
            => (await uow.Repository<PlaybookDocument>().FindAsync(
                    p => p.DeletedAt == null
                         && (p.Scope == PlaybookScope.ScopeJobPosting || p.Scope == PlaybookScope.ScopeRound)
                         && p.ScopeRefId == jobPostingId
                         && p.DocumentType == ScoringRubric.TypeInterviewRubric
                         && p.RubricJson != null,
                    ct))
                .ToList();

        /// <summary>
        /// Bộ tiêu chí áp cho (tin, vòng): bộ riêng của vòng nếu có, không thì bộ chung của tin.
        /// Danh sách rỗng = chưa khai — nơi gọi KHÔNG được tự chế một bộ thay thế.
        /// </summary>
        public static async Task<List<RubricCriterion>> ResolveAsync(
            IUnitOfWork uow, Guid jobPostingId, int roundNumber, CancellationToken ct = default)
        {
            var docs = await LiveForJobAsync(uow, jobPostingId, ct);
            var chosen = Pick(docs, roundNumber);
            return chosen == null ? new List<RubricCriterion>() : ScoringRubric.Deserialize(chosen.RubricJson);
        }

        /// <summary>Chọn bộ áp cho một vòng từ danh sách bộ sống đã nạp sẵn (dùng khi xử lý theo lô).</summary>
        public static PlaybookDocument? Pick(IEnumerable<PlaybookDocument> liveDocs, int roundNumber)
        {
            var docs = liveDocs.ToList();
            return docs.Where(d => d.Scope == PlaybookScope.ScopeRound && d.RoundNumber == roundNumber)
                       .OrderByDescending(d => d.CreatedAt).FirstOrDefault()
                   ?? docs.Where(d => d.Scope == PlaybookScope.ScopeJobPosting)
                       .OrderByDescending(d => d.CreatedAt).FirstOrDefault();
        }

        /// <summary>
        /// Các vòng HỘI THOẠI của tin chưa có bộ tiêu chí nào áp vào. Rỗng = tin chấm được mọi buổi phỏng vấn.
        /// Vòng trắc nghiệm không tính (tự chấm, không có AI hội thoại). Tin chưa khai vòng nào được coi là một
        /// vòng sơ loại — khớp cách <c>StartSessionAsync</c> xử lý tin cũ.
        /// </summary>
        public static async Task<List<int>> MissingRoundsAsync(IUnitOfWork uow, Guid jobPostingId, CancellationToken ct = default)
        {
            var rounds = (await uow.Repository<InterviewRoundConfig>().FindAsync(r => r.JobPostingId == jobPostingId, ct))
                .Where(r => InterviewRoundTypes.NeedsHiringManager(r.RoundType))
                .Select(r => r.RoundNumber)
                .Distinct()
                .ToList();
            var hasAnyRoundConfig = await uow.Repository<InterviewRoundConfig>()
                .CountAsync(r => r.JobPostingId == jobPostingId, ct) > 0;
            if (!hasAnyRoundConfig) rounds.Add(1);

            var docs = await LiveForJobAsync(uow, jobPostingId, ct);
            return rounds.Where(r => Pick(docs, r) == null).OrderBy(r => r).ToList();
        }

        /// <summary>Những tin (trong danh sách) đã có ít nhất một bộ tiêu chí phỏng vấn đang sống.</summary>
        public static async Task<HashSet<Guid>> JobsWithAnyRubricAsync(
            IUnitOfWork uow, IReadOnlyCollection<Guid> jobIds, CancellationToken ct = default)
        {
            if (jobIds.Count == 0) return new HashSet<Guid>();
            var ids = await uow.Repository<PlaybookDocument>().QueryAsync(
                q => q.Where(p => p.DeletedAt == null
                                  && (p.Scope == PlaybookScope.ScopeJobPosting || p.Scope == PlaybookScope.ScopeRound)
                                  && p.DocumentType == ScoringRubric.TypeInterviewRubric
                                  && p.RubricJson != null
                                  && p.ScopeRefId != null
                                  && jobIds.Contains(p.ScopeRefId!.Value))
                      .Select(p => p.ScopeRefId!.Value),
                ct);
            return ids.ToHashSet();
        }
    }
}
