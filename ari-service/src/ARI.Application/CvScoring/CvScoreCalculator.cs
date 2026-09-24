using System;
using System.Collections.Generic;
using System.Linq;
using ARI.Application.Playbooks;
using ARI.Domain.Constants;

namespace ARI.Application.CvScoring
{
    /// <summary>Kết quả một cổng của công thức (ADR-075).</summary>
    public static class CvGateOutcomes
    {
        public const string Pass = "pass";
        public const string Fail = "fail";
        /// <summary>Chưa xác minh được — cần người kiểm tra tay, không ép khuyến nghị.</summary>
        public const string Unknown = "unknown";
    }

    /// <summary>Lý do của một cổng không qua (mã máy đọc; giao diện tự nói bằng lời của mình).</summary>
    public static class CvGateReasons
    {
        /// <summary>AI xác định CV không đáp ứng điều kiện bắt buộc.</summary>
        public const string KnockoutFailed = "knockout_failed";
        /// <summary>AI bỏ sót điều kiện, hoặc đánh "đạt" mà không trích được bằng chứng.</summary>
        public const string KnockoutUnverified = "knockout_unverified";
        /// <summary>Điểm tiêu chí thấp hơn điểm tối thiểu.</summary>
        public const string BelowMinScore = "below_min_score";
        /// <summary>Tiêu chí có điểm tối thiểu nhưng không ra được điểm.</summary>
        public const string MinScoreUnscored = "min_score_unscored";
    }

    /// <summary>Một tiêu chí sau khi áp công thức.</summary>
    public sealed record CvCriterionResult(
        RubricCriterion Criterion,
        CvCriterionScoring.Outcome Outcome,
        /// <summary>Điều kiện bắt buộc: đạt (có trích dẫn) / không đạt / null = chưa trả lời.</summary>
        bool? KnockoutMet,
        /// <summary>Điều kiện bắt buộc bị AI đánh "đạt" nhưng không có trích dẫn — không tính là đạt.</summary>
        bool KnockoutUnsupported,
        /// <summary><c>pass</c> | <c>fail</c> | <c>unknown</c>; <c>null</c> = tiêu chí không có cổng.</summary>
        string? Gate,
        string? GateReason,
        string? Evidence,
        string? Reasoning)
    {
        public decimal? Score => Criterion.IsKnockout ? null : Outcome.Score;
    }

    /// <summary>Kết quả áp công thức lên cả bộ tiêu chí.</summary>
    public sealed record CvScoreResult(
        IReadOnlyList<CvCriterionResult> Criteria,
        CvScoringPolicy Policy,
        decimal WeightedSum,
        decimal TotalWeight,
        /// <summary>Σ sᵢ·wᵢ ÷ Σ wᵢ, CHƯA làm tròn. <c>null</c> khi không tiêu chí chấm điểm nào ra được điểm.</summary>
        decimal? Exact,
        /// <summary>Điểm tổng — làm tròn ĐÚNG MỘT LẦN (AwayFromZero).</summary>
        int? Score,
        /// <summary>Nhãn theo điểm (chưa xét cổng).</summary>
        string? ScoreTier,
        /// <summary><see cref="CvGateStatuses"/>; <c>null</c> khi bộ tiêu chí không có cổng.</summary>
        string? GateStatus,
        /// <summary>Nhãn cuối: <c>Reject</c> khi trượt cổng, ngược lại là <see cref="ScoreTier"/>.</summary>
        string? Recommendation);

    /// <summary>
    /// Công thức chấm CV — MỘT chỗ duy nhất làm số học (ADR-075, tiếp ADR-060/071: phán đoán giao AI, số học giữ ở
    /// backend). Hàm thuần: cùng bộ tiêu chí + công thức + quan sát thì luôn ra cùng kết quả, nên tính lại được bất
    /// cứ lúc nào từ quan sát đã lưu, và nút "Xem trước tác động" chạy đúng đường này trong bộ nhớ.
    ///
    /// Mô hình lai của tài liệu tuyển dụng: <b>bù trừ</b> (trung bình có trọng số — điểm cao bù điểm thấp) cộng hai
    /// loại <b>cổng không bù trừ</b>: điều kiện bắt buộc và điểm tối thiểu từng tiêu chí. Trượt cổng chỉ ép nhãn khuyến
    /// nghị về "Reject" — điểm vẫn tính, vẫn hiện, hồ sơ không bị đụng tới (ADR-053).
    /// </summary>
    public static class CvScoreCalculator
    {
        public static CvScoreResult Compute(
            IReadOnlyList<RubricCriterion> criteria,
            CvScoringPolicy? policy,
            IReadOnlyDictionary<string, CvCriterionObservation> observations)
        {
            var p = (policy ?? CvScoringPolicy.Default).Normalized();
            var results = new List<CvCriterionResult>(criteria.Count);

            foreach (var c in criteria)
            {
                observations.TryGetValue(c.Key, out var obs);
                results.Add(c.IsKnockout ? Knockout(c, obs) : Scored(c, obs, p.Bands));
            }

            decimal weighted = 0, total = 0;
            foreach (var r in results)
            {
                if (r.Criterion.IsKnockout || r.Outcome.Score is not { } s) continue;
                // Tiêu chí không ra điểm bị loại khỏi CẢ tử lẫn mẫu (ADR-060) — thiếu dữ liệu không phải điểm kém.
                weighted += Math.Clamp(s, 0m, 100m) * r.Criterion.Weight;
                total += r.Criterion.Weight;
            }

            decimal? exact = total > 0 ? weighted / total : null;
            int? score = exact is { } e ? (int)Math.Round(e, 0, MidpointRounding.AwayFromZero) : null;

            var gates = results.Where(r => r.Gate != null).Select(r => r.Gate!).ToList();
            string? gateStatus = gates.Count == 0 ? null
                : gates.Contains(CvGateOutcomes.Fail) ? CvGateStatuses.Fail
                : gates.Contains(CvGateOutcomes.Unknown) ? CvGateStatuses.Review
                : CvGateStatuses.Pass;

            var tier = score is { } sc ? p.Tier(sc) : null;
            var recommendation = tier == null ? null
                : gateStatus == CvGateStatuses.Fail ? CvRecommendations.Reject
                : tier;

            return new CvScoreResult(results, p, weighted, total, exact, score, tier, gateStatus, recommendation);
        }

        /// <summary>
        /// Điểm tổng hiển thị 2 chữ số, cắt (không làm tròn) — luôn khớp với điểm đã làm tròn một lần: 79,495 hiện
        /// "79,49 → 79" chứ không phải "79,50 → 79".
        /// </summary>
        public static decimal DisplayExact(decimal exact) => Math.Round(exact, 2, MidpointRounding.ToZero);

        private static CvCriterionResult Scored(RubricCriterion c, CvCriterionObservation? obs, CvBandCuts bands)
        {
            var outcome = CvCriterionScoring.Resolve(c, obs, bands);
            string? gate = null, reason = null;
            if (c.MinScore is { } min)
            {
                if (outcome.Score is not { } s) { gate = CvGateOutcomes.Unknown; reason = CvGateReasons.MinScoreUnscored; }
                else if (s < min) { gate = CvGateOutcomes.Fail; reason = CvGateReasons.BelowMinScore; }
                else gate = CvGateOutcomes.Pass;
            }
            return new CvCriterionResult(c, outcome, null, false, gate, reason, obs?.Evidence, obs?.Reasoning);
        }

        private static CvCriterionResult Knockout(RubricCriterion c, CvCriterionObservation? obs)
        {
            var empty = new CvCriterionScoring.Outcome(null, null, null, Array.Empty<CvCriterionScoring.CheckOutcome>());
            var evidence = string.IsNullOrWhiteSpace(obs?.Evidence) ? null : obs!.Evidence!.Trim();

            // Cùng luật chống bịa với ý kiểm: "đạt" phải có trích dẫn. Nhưng thiếu trích dẫn KHÔNG phải bằng chứng
            // ứng viên trượt — nên thành "chưa xác minh", cần người kiểm, không ép "Reject".
            if (obs?.Met == true && evidence == null)
                return new CvCriterionResult(c, empty, false, true, CvGateOutcomes.Unknown, CvGateReasons.KnockoutUnverified, null, obs.Reasoning);
            if (obs?.Met == true)
                return new CvCriterionResult(c, empty, true, false, CvGateOutcomes.Pass, null, evidence, obs.Reasoning);
            if (obs?.Met == false)
                return new CvCriterionResult(c, empty, false, false, CvGateOutcomes.Fail, CvGateReasons.KnockoutFailed, null, obs.Reasoning);
            return new CvCriterionResult(c, empty, null, false, CvGateOutcomes.Unknown, CvGateReasons.KnockoutUnverified, null, obs?.Reasoning);
        }
    }
}
