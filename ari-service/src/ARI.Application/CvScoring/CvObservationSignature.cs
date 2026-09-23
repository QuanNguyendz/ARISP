using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARI.Application.Playbooks;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Chữ ký "AI được hỏi gì" của từng tiêu chí (ADR-075) — căn cứ để TÍNH LẠI điểm theo công thức mới từ câu trả
    /// lời cũ của AI, không gọi AI lại.
    ///
    /// Prompt chấm CV không chứa con số nào (<see cref="ScoringRubric.ToCvPromptText"/>): AI chỉ thấy tên, loại, chuẩn
    /// chấm, lời neo và chữ của ý kiểm. Nên chữ ký băm ĐÚNG những thứ đó và bỏ qua mọi phần số học — trọng số,
    /// điểm tối thiểu, trọng số ý kiểm, ngưỡng dải, ngưỡng khuyến nghị, thứ tự tiêu chí. Hai phiên bản bộ tiêu chí có
    /// cùng chữ ký ở một tiêu chí thì câu trả lời của AI cho tiêu chí đó dùng lại được nguyên vẹn.
    ///
    /// <see cref="Version"/> tăng khi HỢP ĐỒNG prompt đổi (câu lệnh cho AI, dạng câu trả lời) — mọi bản chấm cũ
    /// khi đó tự hết dùng lại được.
    /// </summary>
    public static class CvObservationSignature
    {
        public const int Version = 1;

        /// <summary>Mã tiêu chí → chữ ký.</summary>
        public static Dictionary<string, string> Of(IReadOnlyList<RubricCriterion> criteria)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in criteria)
                if (!string.IsNullOrWhiteSpace(c.Key)) map[c.Key] = OfCriterion(c);
            return map;
        }

        public static string OfCriterion(RubricCriterion c)
        {
            var knockout = c.IsKnockout;
            // Thứ tự thuộc tính cố định (kiểu vô danh giữ thứ tự khai báo) → JSON chuẩn tắc.
            var canonical = new
            {
                v = Version,
                key = c.Key.Trim().ToLowerInvariant(),
                kind = knockout ? RubricCriterionKinds.Knockout : RubricCriterionKinds.Scored,
                name = c.Name ?? string.Empty,
                description = c.Description ?? string.Empty,
                levels = knockout || c.Levels is not { IsEmpty: false } lv
                    ? null
                    : new[] { lv.Excellent ?? string.Empty, lv.Good ?? string.Empty, lv.Fair ?? string.Empty, lv.Poor ?? string.Empty },
                checks = knockout
                    ? Array.Empty<string[]>()
                    : (c.Checks ?? new List<RubricCheck>())
                        .OrderBy(x => x.Key, StringComparer.Ordinal)
                        .Select(x => new[] { x.Key, x.Text })
                        .ToArray(),
            };
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical)));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Bản chấm theo bộ <paramref name="donor"/> dùng lại được cho bộ <paramref name="live"/> khi MỌI tiêu chí đang
        /// sống đều có trong bộ cũ với cùng chữ ký. Bớt tiêu chí → vẫn phủ; thêm tiêu chí hay sửa lời → không phủ.
        /// </summary>
        public static bool Covers(IReadOnlyDictionary<string, string> donor, IReadOnlyDictionary<string, string> live)
        {
            if (live.Count == 0) return false;
            foreach (var (key, sig) in live)
                if (!donor.TryGetValue(key, out var d) || !string.Equals(d, sig, StringComparison.Ordinal))
                    return false;
            return true;
        }
    }
}
