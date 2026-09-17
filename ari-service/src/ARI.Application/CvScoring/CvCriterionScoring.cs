using System;
using System.Collections.Generic;
using System.Linq;
using ARI.Application.DTOs;
using ARI.Application.Playbooks;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Ra điểm MỘT tiêu chí từ câu trả lời của AI.
    ///
    /// AI làm hai việc định tính: chọn DẢI (đối chiếu bằng chứng với mức neo) và trả lời CÓ/KHÔNG từng ý kiểm kèm
    /// trích dẫn. Vị trí điểm TRONG dải là số học, backend tính:
    /// <code>điểm = đáy dải + (số ý đạt ÷ số ý đã trả lời) × (đỉnh dải − đáy dải)</code>, làm tròn số nguyên —
    /// dải 90–100, đạt 2/4 ý → 95. Nhờ vậy hai CV cùng dải chênh nhau bao nhiêu điểm luôn chỉ ra được là khác nhau
    /// ở ý nào, thay vì là cảm giác của model.
    ///
    /// Hai luật chặn: ý AI đánh "đạt" mà không trích được bằng chứng thì KHÔNG tính (chống bịa); ý AI bỏ sót bị loại
    /// khỏi cả tử lẫn mẫu (thiếu dữ liệu không phải điểm kém — cùng luật với tiêu chí bị bỏ sót).
    ///
    /// Tiêu chí chưa có ý kiểm (bộ tiêu chí lập trước khi có ý kiểm) dùng số AI ước lượng, kẹp vào dải đã chọn.
    /// </summary>
    public static class CvCriterionScoring
    {
        /// <summary>Điểm trong dải tính từ ý kiểm.</summary>
        public const string SourceChecklist = "checklist";
        /// <summary>Điểm trong dải do AI ước lượng (tiêu chí không có ý kiểm, hoặc AI không trả lời ý nào).</summary>
        public const string SourceAi = "ai";

        public sealed record CheckOutcome(string Key, string Text, bool? Met, string? Evidence, bool Unsupported);

        public sealed record Outcome(decimal? Score, string? Band, string? Source, IReadOnlyList<CheckOutcome> Checks)
        {
            public int Met => Checks.Count(c => c.Met == true);
            public int Answered => Checks.Count(c => c.Met != null);
        }

        public static Outcome Resolve(RubricCriterion criterion, CvCriterionAiResult? ai)
        {
            var checks = (criterion.Checks ?? new List<RubricCheck>())
                .Select(chk => Answer(chk, ai?.Checks))
                .ToList();

            if (ai == null) return new Outcome(null, null, null, checks);

            var band = ScoringRubric.NormalizeBand(ai.Band)
                       ?? (ai.Score is { } guessed ? ScoringRubric.BandOf(Math.Clamp(guessed, 0m, 100m)) : null);
            var range = ScoringRubric.BandRange(band);
            var answered = checks.Count(c => c.Met != null);

            if (range is { } r && answered > 0)
            {
                var met = checks.Count(c => c.Met == true);
                var exact = r.Min + (r.Max - r.Min) * met / answered;
                return new Outcome(Math.Round(exact, 0, MidpointRounding.AwayFromZero), band, SourceChecklist, checks);
            }

            if (ai.Score is { } score)
            {
                var clamped = Math.Clamp(score, 0m, 100m);
                if (range is { } rr) clamped = Math.Clamp(clamped, rr.Min, rr.Max);
                return new Outcome(clamped, band ?? ScoringRubric.BandOf(clamped), SourceAi, checks);
            }

            // Có dải mà không có số lẫn câu trả lời ý kiểm → không đủ căn cứ ra điểm; tiêu chí bị loại khỏi phép tính.
            return new Outcome(null, band, null, checks);
        }

        private static CheckOutcome Answer(RubricCheck check, IEnumerable<CvCheckAiResult>? answers)
        {
            var answer = answers?.FirstOrDefault(a =>
                a != null && string.Equals(a.Key?.Trim(), check.Key, StringComparison.OrdinalIgnoreCase));
            if (answer?.Met is not { } met) return new CheckOutcome(check.Key, check.Text, null, null, false);

            var evidence = string.IsNullOrWhiteSpace(answer.Evidence) ? null : answer.Evidence.Trim();
            if (met && evidence == null) return new CheckOutcome(check.Key, check.Text, false, null, true);
            return new CheckOutcome(check.Key, check.Text, met, met ? evidence : null, false);
        }
    }
}
