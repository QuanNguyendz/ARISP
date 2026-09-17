using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ARI.Application.Playbooks
{
    /// <summary>Một tiêu chí chấm điểm do doanh nghiệp khai (ADR-060).</summary>
    public class RubricCriterion
    {
        /// <summary>Khoá máy đọc, snake_case — dùng làm khoá trong JSON điểm.</summary>
        public string Key { get; set; } = string.Empty;
        /// <summary>Tên hiển thị cho HR và ứng viên (tiếng Việt hoặc tiếng Anh tuỳ doanh nghiệp).</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Trọng số (%), tổng mọi tiêu chí phải bằng 100.</summary>
        public decimal Weight { get; set; }
        /// <summary>Chuẩn chấm: thế nào là tốt, thế nào là kém. Đưa vào prompt + RAG.</summary>
        public string? Description { get; set; }

        /// <summary>
        /// Mức neo (ADR-070): mô tả cụ thể từng dải điểm. Không bắt buộc, nhưng có neo thì hai lần chấm
        /// cùng một CV ra cùng một dải — thiếu neo, "7/10" của model là cảm tính.
        /// </summary>
        public RubricLevels? Levels { get; set; }

        /// <summary>
        /// Ý kiểm: vài dấu hiệu CÓ/KHÔNG kiểm được từ CV (vd "Có ≥ 4 năm .NET production"). AI chỉ chọn DẢI điểm
        /// và trả lời từng ý; vị trí điểm TRONG dải do backend tính theo số ý đạt — nên hai CV cùng dải khác nhau
        /// vài điểm luôn chỉ ra được là khác nhau ở ý nào. Không có ý kiểm thì AI tự ước lượng vị trí trong dải.
        /// </summary>
        public List<RubricCheck>? Checks { get; set; }
    }

    /// <summary>Một ý kiểm của tiêu chí. <see cref="Key"/> do hệ thống sinh (<c>k1</c>, <c>k2</c>…) và giữ nguyên khi sửa chữ.</summary>
    public class RubricCheck
    {
        public string Key { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Bốn dải điểm cố định — người khai chỉ viết lời, không tự đặt ngưỡng.</summary>
    public class RubricLevels
    {
        /// <summary>90–100.</summary>
        public string? Excellent { get; set; }
        /// <summary>70–89.</summary>
        public string? Good { get; set; }
        /// <summary>40–69.</summary>
        public string? Fair { get; set; }
        /// <summary>0–39.</summary>
        public string? Poor { get; set; }

        [JsonIgnore]
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Excellent) && string.IsNullOrWhiteSpace(Good)
            && string.IsNullOrWhiteSpace(Fair) && string.IsNullOrWhiteSpace(Poor);
    }

    /// <summary>
    /// Bộ tiêu chí chấm điểm (ADR-060) — thay cho việc để LLM tự nghĩ ra tiêu chí rồi tự cho điểm tổng.
    ///
    /// Nguyên tắc: <b>AI chỉ chấm TỪNG tiêu chí; điểm cuối do backend cộng có trọng số.</b> Trước đây
    /// prompt vừa ép một danh sách 8 tiêu chí tiếng Anh viết cứng, vừa hỏi luôn <c>score</c> tổng — con
    /// số tổng ấy không phải trung bình có trọng số của gì cả, nên "chấm điểm" chỉ là cảm tính của model.
    /// </summary>
    public static class ScoringRubric
    {
        /// <summary>Loại playbook: bộ tiêu chí chấm CV so với JD.</summary>
        public const string TypeCvRubric = "cv_rubric";
        /// <summary>Loại playbook: bộ tiêu chí chấm buổi phỏng vấn.</summary>
        public const string TypeInterviewRubric = "interview_rubric";

        /// <summary>Trọng số phải cộng tròn 100 — cho sai số nhỏ vì HR hay nhập số thập phân.</summary>
        public const decimal WeightTolerance = 0.01m;

        public const int MaxCriteria = 20;

        /// <summary>Tối đa ý kiểm mỗi tiêu chí — nhiều hơn thì mỗi ý nặng quá nhẹ và HM khó giữ các ý độc lập.</summary>
        public const int MaxChecks = 8;
        public const int MaxCheckLength = 200;

        private static readonly Regex KeyPattern = new("^[a-z][a-z0-9_]{1,39}$", RegexOptions.Compiled);
        private static readonly Regex CheckKeyPattern = new("^[a-z][a-z0-9_]{0,19}$", RegexOptions.Compiled);

        public static bool IsValidCheckKey(string? key) => !string.IsNullOrWhiteSpace(key) && CheckKeyPattern.IsMatch(key);

        // ---------- Dải điểm cố định (mức neo) ----------
        public const string BandExcellent = "excellent";
        public const string BandGood = "good";
        public const string BandFair = "fair";
        public const string BandPoor = "poor";

        /// <summary>Khoảng điểm của một dải: excellent 90–100 · good 70–89 · fair 40–69 · poor 0–39.</summary>
        public static (decimal Min, decimal Max)? BandRange(string? band) => band switch
        {
            BandExcellent => (90m, 100m),
            BandGood => (70m, 89m),
            BandFair => (40m, 69m),
            BandPoor => (0m, 39m),
            _ => null,
        };

        public static string BandOf(decimal score) => score switch
        {
            >= 90 => BandExcellent,
            >= 70 => BandGood,
            >= 40 => BandFair,
            _ => BandPoor,
        };

        /// <summary>Tên dải AI trả về → một trong bốn dải chuẩn (chấp nhận viết hoa, "90-100"…). Không nhận ra → null.</summary>
        public static string? NormalizeBand(string? band)
        {
            var b = band?.Trim().ToLowerInvariant().Replace('–', '-').Replace(" ", string.Empty);
            return b switch
            {
                BandExcellent or "90-100" => BandExcellent,
                BandGood or "70-89" => BandGood,
                BandFair or "40-69" => BandFair,
                BandPoor or "0-39" => BandPoor,
                _ => null,
            };
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static bool IsRubricType(string? documentType)
        {
            var t = documentType?.Trim();
            return string.Equals(t, TypeCvRubric, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, TypeInterviewRubric, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm bộ tiêu chí. Trả về danh sách lỗi (rỗng = hợp lệ) — trả HẾT lỗi một lượt để HR sửa file
        /// một lần, thay vì mỗi lần upload lại phát hiện thêm một lỗi.
        /// </summary>
        public static List<string> Validate(IReadOnlyList<RubricCriterion> criteria)
        {
            var errors = new List<string>();
            if (criteria.Count == 0)
            {
                errors.Add("Bộ tiêu chí trống — cần ít nhất 1 tiêu chí.");
                return errors;
            }
            if (criteria.Count > MaxCriteria)
                errors.Add($"Tối đa {MaxCriteria} tiêu chí, file đang có {criteria.Count}.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in criteria)
            {
                if (string.IsNullOrWhiteSpace(c.Key) || !KeyPattern.IsMatch(c.Key))
                    errors.Add($"Mã tiêu chí '{c.Key}' không hợp lệ: chỉ dùng chữ thường, số và dấu gạch dưới (bắt đầu bằng chữ).");
                else if (!seen.Add(c.Key))
                    errors.Add($"Mã tiêu chí '{c.Key}' bị trùng.");

                if (string.IsNullOrWhiteSpace(c.Name))
                    errors.Add($"Tiêu chí '{c.Key}' thiếu tên hiển thị.");

                if (c.Weight <= 0)
                    errors.Add($"Tiêu chí '{c.Key}' phải có trọng số lớn hơn 0.");

                if (c.Checks is { Count: > 0 } checks)
                {
                    if (checks.Count > MaxChecks)
                        errors.Add($"Tiêu chí '{c.Key}' có {checks.Count} ý kiểm — tối đa {MaxChecks}.");
                    var seenChecks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var chk in checks)
                    {
                        if (string.IsNullOrWhiteSpace(chk.Text))
                            errors.Add($"Tiêu chí '{c.Key}' có ý kiểm để trống.");
                        if (!IsValidCheckKey(chk.Key) || !seenChecks.Add(chk.Key))
                            errors.Add($"Ý kiểm '{chk.Key}' của tiêu chí '{c.Key}' sai mã hoặc trùng mã.");
                    }
                }
            }

            var total = criteria.Sum(c => c.Weight);
            if (Math.Abs(total - 100m) > WeightTolerance)
                errors.Add($"Tổng trọng số phải bằng 100, hiện là {total:0.##}.");

            return errors;
        }

        public static string Serialize(IReadOnlyList<RubricCriterion> criteria)
            => JsonSerializer.Serialize(criteria, JsonOpts);

        public static List<RubricCriterion> Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<RubricCriterion>();
            try { return JsonSerializer.Deserialize<List<RubricCriterion>>(json, JsonOpts) ?? new List<RubricCriterion>(); }
            catch { return new List<RubricCriterion>(); }
        }

        /// <summary>
        /// Điểm cuối = Σ(điểm tiêu chí × trọng số) / Σ(trọng số), làm tròn 2 chữ số.
        ///
        /// Tiêu chí AI không chấm (thiếu khoá) bị <b>loại khỏi cả tử lẫn mẫu</b> thay vì tính 0 điểm:
        /// thiếu dữ liệu không phải là điểm kém, và tính 0 sẽ đánh trượt oan người mà AI quên chấm một mục.
        /// </summary>
        public static decimal? ComputeOverall(
            IReadOnlyList<RubricCriterion> criteria, IReadOnlyDictionary<string, decimal> scores)
        {
            if (criteria.Count == 0 || scores.Count == 0) return null;

            decimal weighted = 0, totalWeight = 0;
            foreach (var c in criteria)
            {
                if (!TryGetScore(scores, c.Key, out var score)) continue;
                var clamped = Math.Clamp(score, 0m, 100m);
                weighted += clamped * c.Weight;
                totalWeight += c.Weight;
            }

            if (totalWeight <= 0) return null;
            return Math.Round(weighted / totalWeight, 2, MidpointRounding.AwayFromZero);
        }

        private static bool TryGetScore(IReadOnlyDictionary<string, decimal> scores, string key, out decimal score)
        {
            if (scores.TryGetValue(key, out score)) return true;
            // Model đôi khi trả khoá viết hoa/khoảng trắng — chấp nhận, đừng vứt điểm vì hình thức.
            foreach (var kv in scores)
            {
                if (string.Equals(kv.Key.Trim().Replace(' ', '_'), key, StringComparison.OrdinalIgnoreCase))
                {
                    score = kv.Value;
                    return true;
                }
            }
            score = 0;
            return false;
        }

        /// <summary>
        /// JSON điểm lưu vào <c>Evaluation.CriterionScores</c>: kèm luôn NHÃN và TRỌNG SỐ tại thời điểm
        /// chấm. Rubric có thể được sửa về sau, nhưng một bản đánh giá cũ vẫn phải giải thích được điểm
        /// của nó ra từ đâu — nên đây là ảnh chụp, không phải tham chiếu.
        /// Giữ tương thích ngược: dạng cũ <c>{"technical": 85}</c> vẫn đọc được ở nơi hiển thị.
        /// </summary>
        public static string SerializeScoreSnapshot(
            IReadOnlyList<RubricCriterion> criteria, IReadOnlyDictionary<string, decimal> scores)
        {
            var snapshot = new Dictionary<string, object>();
            foreach (var c in criteria)
            {
                if (!TryGetScore(scores, c.Key, out var score)) continue;
                snapshot[c.Key] = new
                {
                    score = Math.Clamp(score, 0m, 100m),
                    label = c.Name,
                    weight = c.Weight,
                };
            }
            return JsonSerializer.Serialize(snapshot, JsonOpts);
        }

        /// <summary>
        /// Mô tả rubric cho prompt AI: mỗi dòng một tiêu chí kèm trọng số + chuẩn chấm, và nếu có thì
        /// các mức neo ở dòng dưới — model chấm theo đúng lời doanh nghiệp viết cho từng dải điểm.
        /// </summary>
        public static string ToPromptText(IReadOnlyList<RubricCriterion> criteria)
            => string.Join("\n", criteria.Select(c =>
            {
                var line = $"- {c.Key} | {c.Name} | trọng số {c.Weight:0.##}%"
                           + (string.IsNullOrWhiteSpace(c.Description) ? string.Empty : $" | chuẩn chấm: {c.Description}");
                if (c.Levels is { IsEmpty: false } lv)
                {
                    if (!string.IsNullOrWhiteSpace(lv.Excellent)) line += $"\n    90–100: {lv.Excellent}";
                    if (!string.IsNullOrWhiteSpace(lv.Good)) line += $"\n    70–89: {lv.Good}";
                    if (!string.IsNullOrWhiteSpace(lv.Fair)) line += $"\n    40–69: {lv.Fair}";
                    if (!string.IsNullOrWhiteSpace(lv.Poor)) line += $"\n    0–39: {lv.Poor}";
                }
                if (c.Checks is { Count: > 0 } checks)
                {
                    line += "\n    ý kiểm (trả lời có/không từng ý):";
                    foreach (var chk in checks) line += $"\n      [{chk.Key}] {chk.Text}";
                }
                return line;
            }));
    }
}
