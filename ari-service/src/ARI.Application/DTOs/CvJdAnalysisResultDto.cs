using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARI.Application.DTOs
{
    /// <summary>
    /// Kết quả AI chấm một CV theo bộ tiêu chí của tin (ADR-070).
    ///
    /// CỐ Ý không có điểm tổng: AI chỉ chấm TỪNG tiêu chí kèm bằng chứng, điểm tổng do backend cộng có
    /// trọng số. Trước đây schema có <c>match_score</c> và khi tin chưa khai bộ tiêu chí thì con số đó
    /// thành điểm của ứng viên — một phán đoán không giải thích được từ tiêu chí nào.
    /// </summary>
    public class CvJdAnalysisResultDto
    {
        [JsonPropertyName("is_valid_cv")]
        public bool IsValidCv { get; set; }

        [JsonPropertyName("analysis_reasoning")]
        public string AnalysisReasoning { get; set; } = string.Empty;

        [JsonPropertyName("seniority_alignment")]
        public string SeniorityAlignment { get; set; } = string.Empty;

        [JsonPropertyName("tech_depth_analysis")]
        public string TechDepthAnalysis { get; set; } = string.Empty;

        /// <summary>Điểm từng tiêu chí, đúng và đủ các mã trong bộ tiêu chí.</summary>
        [JsonPropertyName("criteria")]
        public List<CvCriterionAiResult> Criteria { get; set; } = new();

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("skills_matched")]
        public List<string> SkillsMatched { get; set; } = new();

        [JsonPropertyName("skills_gaps")]
        public List<string> SkillsGaps { get; set; } = new();

        [JsonPropertyName("red_flags")]
        public List<string> RedFlags { get; set; } = new();

        [JsonPropertyName("experience_relevance")]
        public string ExperienceRelevance { get; set; } = string.Empty;

        [JsonPropertyName("overall_recommendation")]
        public string OverallRecommendation { get; set; } = string.Empty;

        // --- Telemetry & System Fields (Do GeminiProvider tự điền, không lấy từ JSON của AI) ---
        [JsonIgnore]
        public string RawResponse { get; set; } = string.Empty;

        [JsonIgnore]
        public int PromptTokens { get; set; }

        [JsonIgnore]
        public int CompletionTokens { get; set; }

        [JsonIgnore]
        public int ProcessingTimeMs { get; set; }

        /// <summary>Nhà cung cấp AI thực sự tạo phân tích: "Gemini" | "GPT-4o-mini" (fallback). Đặt nội bộ, không từ JSON của AI.</summary>
        [JsonIgnore]
        public string Provider { get; set; } = "Gemini";
    }

    /// <summary>AI chấm một tiêu chí: điểm + trích dẫn từ CV + lý do theo chuẩn chấm.</summary>
    public class CvCriterionAiResult
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Dải điểm AI chọn: excellent | good | fair | poor. Với tiêu chí có ý kiểm, điểm trong dải do backend tính
        /// từ <see cref="Checks"/>; không có ý kiểm thì dùng <see cref="Score"/> (kẹp vào dải).
        /// </summary>
        [JsonPropertyName("band")]
        public string? Band { get; set; }

        /// <summary>
        /// Số điểm AI ước lượng — chỉ còn để đọc câu trả lời kiểu cũ (trước ADR-075). Prompt hiện hành không hỏi số:
        /// AI trả <see cref="Position"/>. <c>null</c> khi model không chấm được — tiêu chí bị loại khỏi phép tính.
        /// </summary>
        [JsonPropertyName("score")]
        [JsonConverter(typeof(LenientNullableDecimalConverter))]
        public decimal? Score { get; set; }

        /// <summary>
        /// Vị trí trong dải, 0..1 (ADR-075) — với tiêu chí KHÔNG có ý kiểm: 0 = vừa chạm lời neo của dải, 0,5 = đạt rõ
        /// với nhiều bằng chứng, 1 = sát lời neo của dải trên. Backend đổi ra điểm theo ngưỡng dải của tin, nên AI
        /// không cần (và không được) biết dải đó là bao nhiêu điểm.
        /// </summary>
        [JsonPropertyName("position")]
        [JsonConverter(typeof(LenientNullableDecimalConverter))]
        public decimal? Position { get; set; }

        /// <summary>Câu trả lời cho ĐIỀU KIỆN BẮT BUỘC (ADR-075): đạt / không đạt. Đạt mà không trích dẫn thì không tính là đạt.</summary>
        [JsonPropertyName("met")]
        [JsonConverter(typeof(LenientNullableBoolConverter))]
        public bool? Met { get; set; }

        /// <summary>Câu trả lời có/không cho từng ý kiểm của tiêu chí.</summary>
        [JsonPropertyName("checks")]
        public List<CvCheckAiResult>? Checks { get; set; }

        /// <summary>Trích nguyên văn từ CV làm căn cứ. Rỗng = CV không có bằng chứng.</summary>
        [JsonPropertyName("evidence")]
        public string Evidence { get; set; } = string.Empty;

        /// <summary>Vì sao điểm rơi vào dải này, đối chiếu chuẩn chấm/mức neo.</summary>
        [JsonPropertyName("reasoning")]
        public string Reasoning { get; set; } = string.Empty;
    }

    /// <summary>AI trả lời một ý kiểm. Đạt mà không trích được bằng chứng thì backend KHÔNG tính.</summary>
    public class CvCheckAiResult
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("met")]
        [JsonConverter(typeof(LenientNullableBoolConverter))]
        public bool? Met { get; set; }

        [JsonPropertyName("evidence")]
        public string? Evidence { get; set; }
    }

    /// <summary>
    /// Model đôi khi trả <c>"true"</c>, <c>"yes"</c>, <c>"có"</c> hay <c>1</c> thay cho boolean — một ý kiểm viết lệch
    /// kiểu không được làm hỏng cả lượt chấm. Không nhận ra thì coi như chưa trả lời (null).
    /// </summary>
    public sealed class LenientNullableBoolConverter : JsonConverter<bool?>
    {
        public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True: return true;
                case JsonTokenType.False: return false;
                case JsonTokenType.Null: return null;
                case JsonTokenType.Number: return reader.TryGetDecimal(out var d) ? d != 0 : null;
                case JsonTokenType.String:
                    return (reader.GetString() ?? string.Empty).Trim().ToLowerInvariant() switch
                    {
                        "true" or "yes" or "y" or "1" or "có" or "co" or "đạt" or "dat" => true,
                        "false" or "no" or "n" or "0" or "không" or "khong" or "chưa" or "chua" => false,
                        _ => null,
                    };
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
        {
            if (value is { } v) writer.WriteBooleanValue(v);
            else writer.WriteNullValue();
        }
    }

    /// <summary>
    /// Số viết lệch kiểu (<c>"0.5"</c>, <c>"75"</c>, <c>"0,5"</c>) không được làm hỏng cả lượt chấm; không đọc được thì
    /// coi như AI không trả lời (null) — tiêu chí bị loại khỏi phép tính thay vì cả lượt thất bại.
    /// </summary>
    public sealed class LenientNullableDecimalConverter : JsonConverter<decimal?>
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number: return reader.TryGetDecimal(out var d) ? d : null;
                case JsonTokenType.Null: return null;
                case JsonTokenType.String:
                    var s = (reader.GetString() ?? string.Empty).Trim().Replace("%", string.Empty).Replace(',', '.');
                    return decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value is { } v) writer.WriteNumberValue(v);
            else writer.WriteNullValue();
        }
    }

    /// <summary>Đầu vào để AI gợi ý bộ tiêu chí chấm CV từ phiếu / JD (ADR-070).</summary>
    public record CvRubricSuggestionInput(
        string Title,
        string? Description,
        string? Requirements,
        string? ExperienceLevel,
        IReadOnlyList<string>? Skills);

    /// <summary>Một tiêu chí AI gợi ý — bản nháp, người dùng sửa trước khi lưu.</summary>
    public class CvRubricSuggestionItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("weight")]
        public decimal Weight { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("excellent")]
        public string? Excellent { get; set; }

        [JsonPropertyName("good")]
        public string? Good { get; set; }

        [JsonPropertyName("fair")]
        public string? Fair { get; set; }

        [JsonPropertyName("poor")]
        public string? Poor { get; set; }

        /// <summary>3–5 ý kiểm có/không, kiểm được từ CV.</summary>
        [JsonPropertyName("checks")]
        public List<string>? Checks { get; set; }
    }
}
