using System;
using System.Collections.Generic;
using System.Linq;
using ARI.Application.DTOs;
using ARI.Application.Playbooks;

namespace ARI.Application.CvScoring
{
    /// <summary>Câu trả lời của AI cho một ý kiểm (chưa áp luật bằng chứng).</summary>
    public sealed record CvCheckObservation(string Key, bool? Met, string? Evidence);

    /// <summary>
    /// Điều AI QUAN SÁT được ở một tiêu chí (ADR-075) — hoàn toàn định tính: dải theo lời neo, vị trí trong dải,
    /// đạt/không đạt từng ý kiểm, đạt/không đạt điều kiện bắt buộc, kèm trích dẫn. Không có con số nào của công
    /// thức ở đây: công thức (<see cref="CvScoreCalculator"/>) áp lên quan sát để ra điểm. Tách hai thứ ra là để
    /// đổi công thức thì tính lại từ quan sát cũ, không phải hỏi lại AI.
    /// </summary>
    /// <param name="Position">0..1 trong dải — với tiêu chí không có ý kiểm.</param>
    /// <param name="LegacyScore">Số điểm AI tự cho ở câu trả lời kiểu cũ (trước ADR-075) — chỉ để tương thích.</param>
    /// <param name="Met">Đạt / không đạt — với điều kiện bắt buộc.</param>
    public sealed record CvCriterionObservation(
        string Key,
        string? Band,
        decimal? Position,
        decimal? LegacyScore,
        IReadOnlyList<CvCheckObservation> Checks,
        bool? Met,
        string? Evidence,
        string? Reasoning);

    public static class CvObservations
    {
        /// <summary>
        /// Câu trả lời của AI → quan sát theo mã tiêu chí. Khớp mã không phân biệt hoa thường / khoảng trắng; mã lạ
        /// hoặc chấm trùng bị bỏ (cùng luật với trước ADR-075).
        /// </summary>
        public static Dictionary<string, CvCriterionObservation> FromAi(
            IReadOnlyList<RubricCriterion> criteria, IEnumerable<CvCriterionAiResult>? results)
        {
            var map = new Dictionary<string, CvCriterionObservation>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in results ?? Enumerable.Empty<CvCriterionAiResult>())
            {
                if (r == null || string.IsNullOrWhiteSpace(r.Key)) continue;
                var normalized = r.Key.Trim().Replace(' ', '_');
                var match = criteria.FirstOrDefault(c => string.Equals(c.Key, normalized, StringComparison.OrdinalIgnoreCase));
                if (match == null || map.ContainsKey(match.Key)) continue;
                map[match.Key] = FromAi(match.Key, r);
            }
            return map;
        }

        public static CvCriterionObservation FromAi(string key, CvCriterionAiResult ai) => new(
            key,
            ai.Band,
            NormalizePosition(ai.Position),
            ai.Score,
            (ai.Checks ?? new List<CvCheckAiResult>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Key))
                .Select(c => new CvCheckObservation(c.Key.Trim(), c.Met, Trim(c.Evidence)))
                .ToList(),
            ai.Met,
            Trim(ai.Evidence),
            Trim(ai.Reasoning));

        /// <summary>
        /// Dựng lại quan sát từ ẢNH CHỤP của một bản chấm cũ (ADR-075) — để tính lại theo công thức mới mà không gọi
        /// AI. Trả <c>null</c> nếu ảnh chụp thiếu tiêu chí nào đang sống (không đủ căn cứ → phải hỏi AI).
        ///
        /// Ảnh chụp lưu kết quả ĐÃ áp luật bằng chứng: ý "đạt mà không trích dẫn" lưu là <c>unsupported</c> — dựng lại
        /// thành "đạt, không trích dẫn" để bộ tính gắn cờ y như lần đầu. Ảnh chụp thời ADR-070 (chỉ có số, chưa có
        /// dải) thì dải suy từ số theo ngưỡng dải của bản cũ, vị trí = (số − đáy) ÷ độ rộng.
        /// </summary>
        public static Dictionary<string, CvCriterionObservation>? TryFromSnapshot(
            IReadOnlyList<CvScoreSnapshot.View> views, CvScoringPolicy donorPolicy, IReadOnlyList<RubricCriterion> liveCriteria)
        {
            var byKey = views.ToDictionary(v => v.Key, StringComparer.OrdinalIgnoreCase);
            var bands = donorPolicy.Normalized().Bands;
            var map = new Dictionary<string, CvCriterionObservation>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in liveCriteria)
            {
                if (!byKey.TryGetValue(c.Key, out var v)) return null;

                if (c.IsKnockout)
                {
                    var unsupported = v.Unsupported == true;
                    map[c.Key] = new CvCriterionObservation(c.Key, null, null, null, Array.Empty<CvCheckObservation>(),
                        unsupported ? true : v.Met, unsupported ? null : v.Evidence, v.Reasoning);
                    continue;
                }

                var band = ScoringRubric.NormalizeBand(v.Band) ?? (v.Score is { } s0 ? bands.BandOf(Math.Clamp(s0, 0m, 100m)) : null);
                var checks = (v.Checks ?? Array.Empty<CvScoreSnapshot.CheckItem>())
                    .Select(x => x.Unsupported == true
                        ? new CvCheckObservation(x.Key, true, null)
                        : new CvCheckObservation(x.Key, x.Met, x.Evidence))
                    .ToList();

                decimal? position = v.Position;
                if (position == null && v.Score is { } score && !string.Equals(v.ScoreSource, CvCriterionScoring.SourceChecklist, StringComparison.Ordinal)
                    && bands.Range(band) is { } r && r.Max > r.Min)
                    position = Math.Clamp((Math.Clamp(score, 0m, 100m) - r.Min) / (r.Max - r.Min), 0m, 1m);

                map[c.Key] = new CvCriterionObservation(c.Key, band, position, v.Score, checks, null, v.Evidence, v.Reasoning);
            }
            return map;
        }

        /// <summary>0..1; model lỡ trả theo thang 100 (vd 75) thì đổi về 0,75. Âm / không đọc được → kẹp / bỏ.</summary>
        public static decimal? NormalizePosition(decimal? position)
        {
            if (position is not { } p) return null;
            if (p > 1m && p <= 100m) p /= 100m;
            return Math.Clamp(p, 0m, 1m);
        }

        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
