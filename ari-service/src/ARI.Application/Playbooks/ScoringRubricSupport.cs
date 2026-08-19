using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ARI.Application.Playbooks
{
    /// <summary>
    /// Đọc điểm từng tiêu chí do AI trả về. Chấp nhận CẢ HAI dạng vì hai đường chấm (rag-service và
    /// OpenAI fallback) cùng tồn tại, và các bản đánh giá cũ vẫn phải đọc được:
    /// <list type="bullet">
    /// <item><c>{"technical": 85}</c> — dạng phẳng (mọi bản trước ADR-060)</item>
    /// <item><c>{"technical": {"score": 85, "label": "Chuyên môn", "weight": 40}}</c> — dạng có ảnh chụp</item>
    /// </list>
    /// </summary>
    public static class ScoringRubricSupport
    {
        public static Dictionary<string, decimal> ParseScores(string? json)
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (TryReadScore(prop.Value, out var score))
                        result[prop.Name] = score;
                }
            }
            catch (JsonException)
            {
                // Model trả JSON hỏng — trả rỗng để nơi gọi giữ nguyên điểm của AI thay vì cho 0.
            }

            return result;
        }

        /// <summary>Một dòng điểm để hiển thị: khoá, điểm, và (nếu có ảnh chụp) nhãn + trọng số.</summary>
        public record CriterionScoreView(string Key, decimal Score, string? Label, decimal? Weight);

        /// <summary>
        /// Đọc điểm để HIỂN THỊ. Bản ghi cũ (dạng phẳng) không có nhãn/trọng số → trả null, nơi hiển
        /// thị tự rơi về từ điển nhãn cũ. Nhờ vậy đánh giá cũ vẫn hiện đủ bảng điểm sau khi đổi định dạng.
        /// </summary>
        public static List<CriterionScoreView> ParseForDisplay(string? json)
        {
            var result = new List<CriterionScoreView>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (!TryReadScore(prop.Value, out var score)) continue;

                    string? label = null;
                    decimal? weight = null;
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (prop.Value.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String)
                            label = l.GetString();
                        if (prop.Value.TryGetProperty("weight", out var w) && w.TryGetDecimal(out var wv))
                            weight = wv;
                    }

                    result.Add(new CriterionScoreView(prop.Name, score, label, weight));
                }
            }
            catch (JsonException)
            {
                // JSON hỏng → không hiện bảng điểm, nhưng KHÔNG làm sập cả trang đánh giá.
            }

            return result;
        }

        private static bool TryReadScore(JsonElement value, out decimal score)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    return value.TryGetDecimal(out score);

                case JsonValueKind.String:
                    return decimal.TryParse(value.GetString(), out score);

                case JsonValueKind.Object:
                    if (value.TryGetProperty("score", out var inner)) return TryReadScore(inner, out score);
                    break;
            }

            score = 0;
            return false;
        }
    }
}
