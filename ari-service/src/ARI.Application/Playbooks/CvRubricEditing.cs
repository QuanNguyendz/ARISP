using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ARI.Application.Playbooks
{
    /// <summary>Một dòng bộ tiêu chí do người dùng gửi lên (trình soạn, Excel, AI gợi ý) — ADR-070.</summary>
    public class CvRubricCriterionInput
    {
        /// <summary>Để trống với tiêu chí mới — hệ thống tự sinh. Tiêu chí đã có thì gửi lại đúng mã cũ.</summary>
        public string? Key { get; set; }
        public string? Name { get; set; }
        public decimal Weight { get; set; }
        public string? Description { get; set; }
        public RubricLevels? Levels { get; set; }
        /// <summary>Ý kiểm — mã để trống với ý mới (hệ thống sinh <c>k1</c>, <c>k2</c>…), ý đã có gửi lại mã cũ.</summary>
        public List<RubricCheck>? Checks { get; set; }
    }

    /// <summary>
    /// Cửa DUY NHẤT biến dữ liệu người dùng nhập thành bộ tiêu chí chấm CV hợp lệ (ADR-070).
    ///
    /// Bộ tiêu chí đi vào từ bốn cửa — form phiếu, màn tin, file Excel, AI gợi ý. Mỗi cửa tự chuẩn hoá
    /// là bốn bản luật, và bản nào lệch thì cùng một bộ tiêu chí cho ra hai bộ mã khác nhau, tức điểm
    /// đã chấm không còn tra được về tiêu chí nào.
    ///
    /// <b>Mã tiêu chí do hệ thống sinh</b> từ tên (bỏ dấu → snake_case). Hiring Manager không phải nghĩ
    /// ra "hard_skills". Nhưng mã là khoá của điểm đã lưu, nên sửa TÊN tiêu chí cũ không được đổi mã:
    /// client gửi lại mã cũ thì giữ nguyên.
    /// </summary>
    public static class CvRubricEditing
    {
        public const int MaxNameLength = 120;
        public const int MaxTextLength = 1000;

        public record NormalizeResult(List<RubricCriterion> Criteria, List<string> Errors)
        {
            public bool IsValid => Errors.Count == 0;
        }

        public static NormalizeResult Normalize(IEnumerable<CvRubricCriterionInput>? input)
        {
            var rows = (input ?? Enumerable.Empty<CvRubricCriterionInput>())
                .Where(r => r != null && !(string.IsNullOrWhiteSpace(r.Name) && string.IsNullOrWhiteSpace(r.Key) && r.Weight == 0))
                .ToList();

            var errors = new List<string>();
            var criteria = new List<RubricCriterion>();
            var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Mã cũ giữ trước, để tiêu chí mới trùng tên không "cướp" mã của tiêu chí đang có điểm.
            foreach (var r in rows.Where(r => !string.IsNullOrWhiteSpace(r.Key)))
                usedKeys.Add(r.Key!.Trim().ToLowerInvariant());

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var name = Clean(r.Name, MaxNameLength);
                if (string.IsNullOrEmpty(name))
                {
                    errors.Add($"Tiêu chí thứ {i + 1} chưa có tên.");
                    continue;
                }

                string key;
                if (!string.IsNullOrWhiteSpace(r.Key))
                {
                    key = r.Key!.Trim().ToLowerInvariant();
                }
                else
                {
                    key = UniqueKey(Slugify(name), usedKeys);
                    usedKeys.Add(key);
                }

                var levels = r.Levels == null ? null : new RubricLevels
                {
                    Excellent = Clean(r.Levels.Excellent, MaxTextLength),
                    Good = Clean(r.Levels.Good, MaxTextLength),
                    Fair = Clean(r.Levels.Fair, MaxTextLength),
                    Poor = Clean(r.Levels.Poor, MaxTextLength),
                };

                criteria.Add(new RubricCriterion
                {
                    Key = key,
                    Name = name,
                    Weight = Math.Round(r.Weight, 2, MidpointRounding.AwayFromZero),
                    Description = Clean(r.Description, MaxTextLength),
                    Levels = levels is { IsEmpty: false } ? levels : null,
                    Checks = NormalizeChecks(r.Checks),
                });
            }

            if (criteria.Count > 0 || errors.Count == 0)
                errors.AddRange(ScoringRubric.Validate(criteria));

            // Chấm CV mà không nói "thế nào là tốt" thì model tự nghĩ ra chuẩn — đúng thứ ADR-060 đi
            // bỏ. Bắt buộc ít nhất một trong hai: chuẩn chấm hoặc mức neo.
            foreach (var c in criteria)
            {
                if (string.IsNullOrWhiteSpace(c.Description) && c.Levels == null)
                    errors.Add($"Tiêu chí \"{c.Name}\" cần chuẩn chấm hoặc ít nhất một mức neo.");
            }

            return new NormalizeResult(criteria, errors);
        }

        /// <summary>Đổi danh sách tiêu chí đã lưu về dạng input (để trình soạn tải lên và gửi lại).</summary>
        public static List<CvRubricCriterionInput> ToInput(IEnumerable<RubricCriterion> criteria)
            => criteria.Select(c => new CvRubricCriterionInput
            {
                Key = c.Key,
                Name = c.Name,
                Weight = c.Weight,
                Description = c.Description,
                Levels = c.Levels,
                Checks = c.Checks?.Select(x => new RubricCheck { Key = x.Key, Text = x.Text }).ToList(),
            }).ToList();

        /// <summary>
        /// Ý kiểm: bỏ ý trống và ý trùng chữ, cắt độ dài, giữ mã cũ hợp lệ, sinh <c>k1</c>, <c>k2</c>… cho ý mới.
        /// Vượt số ý tối đa KHÔNG cắt bớt ở đây — <see cref="ScoringRubric.Validate"/> báo lỗi để người khai tự chọn bỏ ý nào.
        /// </summary>
        public static List<RubricCheck>? NormalizeChecks(IEnumerable<RubricCheck>? input)
        {
            var rows = (input ?? Enumerable.Empty<RubricCheck>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Text))
                .Select(x => (Key: x.Key?.Trim().ToLowerInvariant(), Text: Clean(x.Text, ScoringRubric.MaxCheckLength)!))
                .GroupBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (rows.Count == 0) return null;

            var reserved = new HashSet<string>(
                rows.Where(r => ScoringRubric.IsValidCheckKey(r.Key)).Select(r => r.Key!), StringComparer.OrdinalIgnoreCase);
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<RubricCheck>();
            foreach (var r in rows)
            {
                string key;
                if (ScoringRubric.IsValidCheckKey(r.Key) && taken.Add(r.Key!))
                {
                    key = r.Key!;
                }
                else
                {
                    var n = 1;
                    while (reserved.Contains($"k{n}") || taken.Contains($"k{n}")) n++;
                    key = $"k{n}";
                    taken.Add(key);
                }
                result.Add(new RubricCheck { Key = key, Text = r.Text });
            }
            return result;
        }

        /// <summary>
        /// Chia lại trọng số cho tròn 100 (phương pháp phần dư lớn nhất) — dùng cho bản nháp AI gợi ý,
        /// vì model hay trả 33/33/33 hoặc tổng 95. Người dùng vẫn sửa được sau đó.
        /// </summary>
        public static void RebalanceWeights(List<CvRubricCriterionInput> rows)
        {
            if (rows.Count == 0) return;
            var raw = rows.Select(r => r.Weight > 0 ? r.Weight : 1m).ToList();
            var total = raw.Sum();
            var exact = raw.Select(w => w * 100m / total).ToList();
            var floors = exact.Select(Math.Floor).ToList();
            var remainder = 100 - (int)floors.Sum();
            var order = exact.Select((v, i) => (frac: v - Math.Floor(v), i))
                .OrderByDescending(x => x.frac).ThenBy(x => x.i).ToList();
            for (int k = 0; k < remainder && k < order.Count; k++)
                floors[order[k].i] += 1;
            for (int i = 0; i < rows.Count; i++)
                rows[i].Weight = floors[i];
        }

        /// <summary>"Kinh nghiệm .NET &amp; Cloud" → "kinh_nghiem_net_cloud". Luôn khớp mẫu mã của ScoringRubric.</summary>
        public static string Slugify(string name)
        {
            var normalized = name.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                var c = char.ToLowerInvariant(ch);
                if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
                else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
            }

            var slug = sb.ToString().Trim('_');
            if (slug.Length == 0 || !char.IsLetter(slug[0])) slug = "tc_" + slug;
            if (slug.Length > 32) slug = slug[..32].TrimEnd('_');
            return slug.Length < 2 ? "tieu_chi" : slug;
        }

        private static string UniqueKey(string baseKey, HashSet<string> used)
        {
            if (!used.Contains(baseKey)) return baseKey;
            for (int n = 2; ; n++)
            {
                var candidate = $"{baseKey}_{n}";
                if (!used.Contains(candidate)) return candidate;
            }
        }

        private static string? Clean(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var v = value.Trim();
            return v.Length > max ? v[..max] : v;
        }
    }
}
