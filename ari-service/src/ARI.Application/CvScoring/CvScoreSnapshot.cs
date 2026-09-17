using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARI.Application.DTOs;
using ARI.Application.Playbooks;

namespace ARI.Application.CvScoring
{
    /// <summary>
    /// Ảnh chụp bảng điểm một CV (<c>cv_jd_analyses.criterion_scores</c>) — ADR-070.
    ///
    /// Lưu ĐỦ để giải thích lại con số mà không cần bộ tiêu chí sống: tên, trọng số, chuẩn chấm, mức neo
    /// tại thời điểm chấm, cùng điểm + trích dẫn + lý do của AI. Tiêu chí AI bỏ sót cũng được lưu (điểm
    /// <c>null</c>) để màn hình nói được "tiêu chí này không được tính".
    ///
    /// Có trường <c>order</c> vì cột là <c>jsonb</c>: Postgres sắp lại khoá của object, thứ tự tiêu chí
    /// doanh nghiệp khai sẽ mất nếu dựa vào thứ tự khoá.
    /// </summary>
    public static class CvScoreSnapshot
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public sealed class Item
        {
            public int Order { get; set; }
            public decimal? Score { get; set; }
            public string? Label { get; set; }
            public decimal? Weight { get; set; }
            public string? Description { get; set; }
            public RubricLevels? Levels { get; set; }
            public string? Evidence { get; set; }
            public string? Reasoning { get; set; }
            /// <summary>Dải AI chọn (excellent | good | fair | poor).</summary>
            public string? Band { get; set; }
            /// <summary><c>checklist</c> = vị trí trong dải tính từ ý kiểm · <c>ai</c> = AI ước lượng.</summary>
            public string? ScoreSource { get; set; }
            public List<CheckItem>? Checks { get; set; }
        }

        public sealed class CheckItem
        {
            public string Key { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            /// <summary>true = đạt (có trích dẫn) · false = không đạt · null = AI không trả lời.</summary>
            public bool? Met { get; set; }
            public string? Evidence { get; set; }
            /// <summary>AI đánh "đạt" nhưng không trích được bằng chứng — không tính.</summary>
            public bool? Unsupported { get; set; }
        }

        public sealed record View(
            string Key, decimal? Score, string? Label, decimal? Weight, string? Description,
            RubricLevels? Levels, string? Evidence, string? Reasoning,
            string? Band = null, string? ScoreSource = null, IReadOnlyList<CheckItem>? Checks = null);

        /// <summary>Điểm AI theo mã tiêu chí (khớp không phân biệt hoa thường / khoảng trắng).</summary>
        public static Dictionary<string, CvCriterionAiResult> IndexAiResults(
            IReadOnlyList<RubricCriterion> criteria, IEnumerable<CvCriterionAiResult>? results)
        {
            var map = new Dictionary<string, CvCriterionAiResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in results ?? Enumerable.Empty<CvCriterionAiResult>())
            {
                if (r == null || string.IsNullOrWhiteSpace(r.Key)) continue;
                var normalized = r.Key.Trim().Replace(' ', '_');
                var match = criteria.FirstOrDefault(c => string.Equals(c.Key, normalized, StringComparison.OrdinalIgnoreCase));
                if (match == null || map.ContainsKey(match.Key)) continue; // mã lạ / chấm trùng → bỏ
                map[match.Key] = r;
            }
            return map;
        }

        /// <summary>
        /// Điểm hợp lệ (đã ra được) theo mã — đầu vào của <see cref="ScoringRubric.ComputeOverall"/>. Điểm từng tiêu chí
        /// đi qua <see cref="CvCriterionScoring.Resolve"/>: có ý kiểm thì vị trí trong dải do backend tính.
        /// </summary>
        public static Dictionary<string, decimal> Scores(
            IReadOnlyList<RubricCriterion> criteria, IReadOnlyDictionary<string, CvCriterionAiResult> indexed)
        {
            var scores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in criteria)
            {
                indexed.TryGetValue(c.Key, out var ai);
                if (CvCriterionScoring.Resolve(c, ai).Score is { } s) scores[c.Key] = s;
            }
            return scores;
        }

        public static string Serialize(
            IReadOnlyList<RubricCriterion> criteria, IReadOnlyDictionary<string, CvCriterionAiResult> indexed)
        {
            var snapshot = new Dictionary<string, Item>();
            for (int i = 0; i < criteria.Count; i++)
            {
                var c = criteria[i];
                indexed.TryGetValue(c.Key, out var ai);
                var outcome = CvCriterionScoring.Resolve(c, ai);
                snapshot[c.Key] = new Item
                {
                    Order = i,
                    Score = outcome.Score,
                    Label = c.Name,
                    Weight = c.Weight,
                    Description = c.Description,
                    Levels = c.Levels,
                    Evidence = string.IsNullOrWhiteSpace(ai?.Evidence) ? null : ai!.Evidence.Trim(),
                    Reasoning = string.IsNullOrWhiteSpace(ai?.Reasoning) ? null : ai!.Reasoning.Trim(),
                    Band = outcome.Band,
                    ScoreSource = outcome.Source,
                    Checks = outcome.Checks.Count == 0
                        ? null
                        : outcome.Checks.Select(x => new CheckItem
                        {
                            Key = x.Key,
                            Text = x.Text,
                            Met = x.Met,
                            Evidence = x.Evidence,
                            Unsupported = x.Unsupported ? true : null,
                        }).ToList(),
                };
            }
            return JsonSerializer.Serialize(snapshot, JsonOpts);
        }

        /// <summary>
        /// Đọc ảnh chụp để hiển thị. Đọc được cả hai dạng cũ của ADR-060 (<c>{"k": 85}</c> và
        /// <c>{"k": {"score":85,"label":..,"weight":..}}</c>) — bản chấm cũ vẫn hiện ra được.
        /// </summary>
        public static List<View> Parse(string? json)
        {
            var result = new List<(int Order, View View)>();
            if (string.IsNullOrWhiteSpace(json)) return new List<View>();

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return new List<View>();

                var position = 0;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    position++;
                    switch (prop.Value.ValueKind)
                    {
                        case JsonValueKind.Number when prop.Value.TryGetDecimal(out var flat):
                            result.Add((1000 + position, new View(prop.Name, flat, null, null, null, null, null, null)));
                            break;
                        case JsonValueKind.Object:
                            var item = prop.Value.Deserialize<Item>(JsonOpts);
                            if (item == null) break;
                            var hasOrder = prop.Value.TryGetProperty("order", out _);
                            result.Add((hasOrder ? item.Order : 1000 + position, new View(
                                prop.Name, item.Score, item.Label, item.Weight, item.Description,
                                item.Levels, item.Evidence, item.Reasoning, item.Band, item.ScoreSource, item.Checks)));
                            break;
                    }
                }
            }
            catch (JsonException)
            {
                // JSON hỏng → không hiện bảng điểm, nhưng không làm sập cả trang hồ sơ.
                return new List<View>();
            }

            return result.OrderBy(r => r.Order).Select(r => r.View).ToList();
        }

        // ---------- Khuyến nghị suy ra từ ĐIỂM (không hỏi AI) ----------

        public const int StrongHireFrom = 80;
        public const int HireFrom = 65;
        public const int CautionFrom = 50;

        /// <summary>
        /// Nhãn khuyến nghị suy thẳng từ điểm có trọng số. Trước ADR-070 AI tự chọn nhãn này, nên có lúc
        /// điểm 45 đi kèm "Hire" — hai phán đoán tổng thể không cùng một gốc.
        /// </summary>
        public static string Recommendation(int score) => score switch
        {
            >= StrongHireFrom => "Strong Hire",
            >= HireFrom => "Hire",
            >= CautionFrom => "Proceed with caution",
            _ => "Reject",
        };
    }
}
