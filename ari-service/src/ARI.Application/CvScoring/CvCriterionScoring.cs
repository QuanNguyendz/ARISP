using System;
using System.Collections.Generic;
using System.Linq;
using ARI.Application.DTOs;
using ARI.Application.Playbooks;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Ra điểm MỘT tiêu chí chấm điểm từ quan sát của AI và công thức của tin.
    ///
    /// AI làm hai việc định tính: chọn DẢI (đối chiếu bằng chứng với lời neo) và trả lời CÓ/KHÔNG từng ý kiểm kèm
    /// trích dẫn. Vị trí điểm TRONG dải là số học, backend tính:
    /// <code>điểm = đáy dải + (Σ trọng số ý đạt ÷ Σ trọng số ý đã trả lời) × (đỉnh dải − đáy dải)</code>, làm tròn số
    /// nguyên — dải 90–100, đạt 2/4 ý ×1 → 95. Ngưỡng dải và trọng số ý (×1/×2/×3) là công thức HM khai theo tin
    /// (ADR-075); toàn ×1 với ngưỡng mặc định thì ra đúng số như ADR-071.
    ///
    /// Hai luật chặn: ý AI đánh "đạt" mà không trích được bằng chứng thì KHÔNG tính (chống bịa — vẫn nằm ở mẫu); ý AI
    /// bỏ sót bị loại khỏi cả tử lẫn mẫu (thiếu dữ liệu không phải điểm kém — cùng luật với tiêu chí bị bỏ sót).
    ///
    /// Tiêu chí không có ý kiểm: AI cho VỊ TRÍ 0..1 trong dải (prompt không có con số nào), backend đổi ra điểm. Câu
    /// trả lời kiểu cũ (AI tự cho số) vẫn đọc được: số kẹp vào dải.
    /// </summary>
    public static class CvCriterionScoring
    {
        /// <summary>Điểm trong dải tính từ ý kiểm.</summary>
        public const string SourceChecklist = "checklist";
        /// <summary>Điểm trong dải từ vị trí / số AI ước lượng (tiêu chí không có ý kiểm, hoặc AI không trả lời ý nào).</summary>
        public const string SourceAi = "ai";

        public sealed record CheckOutcome(string Key, string Text, bool? Met, string? Evidence, bool Unsupported, int Weight = 1);

        public sealed record Outcome(decimal? Score, string? Band, string? Source, IReadOnlyList<CheckOutcome> Checks,
            decimal? Position = null)
        {
            public int Met => Checks.Count(c => c.Met == true);
            public int Answered => Checks.Count(c => c.Met != null);
            public int MetWeight => Checks.Where(c => c.Met == true).Sum(c => c.Weight);
            public int AnsweredWeight => Checks.Where(c => c.Met != null).Sum(c => c.Weight);
        }

        /// <summary>Tương thích: câu trả lời AI trực tiếp, ngưỡng dải mặc định.</summary>
        public static Outcome Resolve(RubricCriterion criterion, CvCriterionAiResult? ai)
            => Resolve(criterion, ai == null ? null : CvObservations.FromAi(criterion.Key, ai), new CvBandCuts());

        public static Outcome Resolve(RubricCriterion criterion, CvCriterionObservation? obs, CvBandCuts bands)
        {
            var checks = (criterion.Checks ?? new List<RubricCheck>())
                .Select(chk => Answer(chk, obs?.Checks))
                .ToList();

            if (obs == null) return new Outcome(null, null, null, checks);

            var band = ScoringRubric.NormalizeBand(obs.Band)
                       ?? (obs.LegacyScore is { } guessed ? bands.BandOf(Math.Clamp(guessed, 0m, 100m)) : null);
            var range = bands.Range(band);
            var answeredWeight = checks.Where(c => c.Met != null).Sum(c => c.Weight);

            if (range is { } r && answeredWeight > 0)
            {
                var metWeight = checks.Where(c => c.Met == true).Sum(c => c.Weight);
                var exact = r.Min + (r.Max - r.Min) * metWeight / answeredWeight;
                return new Outcome(Math.Round(exact, 0, MidpointRounding.AwayFromZero), band, SourceChecklist, checks);
            }

            if (range is { } rp && obs.Position is { } position)
            {
                var p = Math.Clamp(position, 0m, 1m);
                var exact = rp.Min + (rp.Max - rp.Min) * p;
                return new Outcome(Math.Round(exact, 0, MidpointRounding.AwayFromZero), band, SourceAi, checks, p);
            }

            if (obs.LegacyScore is { } score)
            {
                var clamped = Math.Clamp(score, 0m, 100m);
                if (range is { } rr) clamped = Math.Clamp(clamped, rr.Min, rr.Max);
                return new Outcome(clamped, band ?? bands.BandOf(clamped), SourceAi, checks);
            }

            // Có dải mà không có vị trí lẫn câu trả lời ý kiểm → không đủ căn cứ ra điểm; tiêu chí bị loại khỏi phép tính.
            return new Outcome(null, band, null, checks);
        }

        private static CheckOutcome Answer(RubricCheck check, IEnumerable<CvCheckObservation>? answers)
        {
            var weight = check.EffectiveWeight;
            var answer = answers?.FirstOrDefault(a =>
                a != null && string.Equals(a.Key?.Trim(), check.Key, StringComparison.OrdinalIgnoreCase));
            if (answer?.Met is not { } met) return new CheckOutcome(check.Key, check.Text, null, null, false, weight);

            var evidence = string.IsNullOrWhiteSpace(answer.Evidence) ? null : answer.Evidence.Trim();
            if (met && evidence == null) return new CheckOutcome(check.Key, check.Text, false, null, true, weight);
            return new CheckOutcome(check.Key, check.Text, met, met ? evidence : null, false, weight);
        }
    }
}
