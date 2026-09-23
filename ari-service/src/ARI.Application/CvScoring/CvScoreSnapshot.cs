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
            /// <summary><c>checklist</c> = vị trí trong dải tính từ ý kiểm · <c>ai</c> = từ vị trí / số AI cho.</summary>
            public string? ScoreSource { get; set; }
            public List<CheckItem>? Checks { get; set; }

            // ---- ADR-075 ----
            /// <summary><c>knockout</c> = điều kiện bắt buộc; bỏ trống = tiêu chí chấm điểm.</summary>
            public string? Kind { get; set; }
            /// <summary>Điểm tối thiểu của tiêu chí lúc chấm.</summary>
            public int? MinScore { get; set; }
            /// <summary>Điều kiện bắt buộc: đạt (có trích dẫn) / không đạt / null = AI không trả lời.</summary>
            public bool? Met { get; set; }
            /// <summary>Điều kiện bắt buộc bị đánh "đạt" mà không có trích dẫn.</summary>
            public bool? Unsupported { get; set; }
            /// <summary>Kết quả cổng: <c>pass</c> | <c>fail</c> | <c>unknown</c>.</summary>
            public string? Gate { get; set; }
            public string? GateReason { get; set; }
            /// <summary>Vị trí 0..1 AI cho trong dải (tiêu chí không có ý kiểm).</summary>
            public decimal? Position { get; set; }
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
            /// <summary>Trọng số ý lúc chấm (ADR-075); bỏ trống = ×1.</summary>
            public int? Weight { get; set; }
        }

        public sealed record View(
            string Key, decimal? Score, string? Label, decimal? Weight, string? Description,
            RubricLevels? Levels, string? Evidence, string? Reasoning,
            string? Band = null, string? ScoreSource = null, IReadOnlyList<CheckItem>? Checks = null,
            string? Kind = null, int? MinScore = null, bool? Met = null, bool? Unsupported = null,
            string? Gate = null, string? GateReason = null, decimal? Position = null)
        {
            public bool IsKnockout => string.Equals(Kind, RubricCriterionKinds.Knockout, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Ảnh chụp của một kết quả áp công thức — đủ để giải thích lại con số mà không cần bộ tiêu chí sống.</summary>
        public static string Serialize(CvScoreResult result)
        {
            var snapshot = new Dictionary<string, Item>();
            for (int i = 0; i < result.Criteria.Count; i++)
            {
                var r = result.Criteria[i];
                var c = r.Criterion;
                var outcome = r.Outcome;
                snapshot[c.Key] = c.IsKnockout
                    ? new Item
                    {
                        Order = i,
                        Label = c.Name,
                        Weight = 0,
                        Description = c.Description,
                        Evidence = r.Evidence,
                        Reasoning = r.Reasoning,
                        Kind = RubricCriterionKinds.Knockout,
                        Met = r.KnockoutMet,
                        Unsupported = r.KnockoutUnsupported ? true : null,
                        Gate = r.Gate,
                        GateReason = r.GateReason,
                    }
                    : new Item
                    {
                        Order = i,
                        Score = outcome.Score,
                        Label = c.Name,
                        Weight = c.Weight,
                        Description = c.Description,
                        Levels = c.Levels,
                        Evidence = r.Evidence,
                        Reasoning = r.Reasoning,
                        Band = outcome.Band,
                        ScoreSource = outcome.Source,
                        Position = outcome.Position,
                        MinScore = c.MinScore,
                        Gate = r.Gate,
                        GateReason = r.GateReason,
                        Checks = outcome.Checks.Count == 0
                            ? null
                            : outcome.Checks.Select(x => new CheckItem
                            {
                                Key = x.Key,
                                Text = x.Text,
                                Met = x.Met,
                                Evidence = x.Evidence,
                                Unsupported = x.Unsupported ? true : null,
                                Weight = x.Weight == 1 ? null : x.Weight,
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
                                item.Levels, item.Evidence, item.Reasoning, item.Band, item.ScoreSource, item.Checks,
                                item.Kind, item.MinScore, item.Met, item.Unsupported, item.Gate, item.GateReason, item.Position)));
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

        // Nhãn khuyến nghị suy thẳng từ điểm (không hỏi AI — trước ADR-070 AI tự chọn nhãn, nên có lúc điểm 45 đi kèm
        // "Hire"). Ngưỡng nay là công thức của tin: CvScoringPolicy.Tier + cổng của CvScoreCalculator (ADR-075).
    }
}
