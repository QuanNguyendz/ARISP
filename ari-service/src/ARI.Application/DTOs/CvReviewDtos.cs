using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ARI.Application.DTOs
{
    /// <summary>Kết quả Gemini đánh giá CV độc lập (không gắn JD) — output JSON thô.</summary>
    public class CvReviewResultDto
    {
        [JsonPropertyName("is_valid_cv")]
        public bool IsValidCv { get; set; }

        [JsonPropertyName("overall_score")]
        public int OverallScore { get; set; }

        [JsonPropertyName("verdict")]
        public string Verdict { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("strengths")]
        public List<string> Strengths { get; set; } = new();

        [JsonPropertyName("improvements")]
        public List<string> Improvements { get; set; } = new();

        [JsonPropertyName("missing_sections")]
        public List<string> MissingSections { get; set; } = new();

        [JsonPropertyName("suggested_positions")]
        public List<string> SuggestedPositions { get; set; } = new();

        [JsonIgnore] public int PromptTokens { get; set; }
        [JsonIgnore] public int CompletionTokens { get; set; }

        /// <summary>Nhà cung cấp AI thực sự tạo đánh giá: "Gemini" hoặc "GPT-4o-mini" (fallback). Đặt nội bộ, không từ JSON của AI.</summary>
        [JsonIgnore] public string Provider { get; set; } = "Gemini";
    }

    /// <summary>Kết quả CV review trả về FE và lưu trong CandidateAccount.CvReviewJson.</summary>
    public class CvReviewResponse
    {
        public bool IsValidCv { get; set; }
        public int OverallScore { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> SuggestedPositions { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> Improvements { get; set; } = new();
        public List<string> MissingSections { get; set; } = new();
        public string? ReviewedAt { get; set; }

        /// <summary>Nhà cung cấp AI đã tạo đánh giá ("Gemini" | "GPT-4o-mini") — hiển thị trên UI.</summary>
        public string? ReviewedBy { get; set; }
    }

    /// <summary>Kết quả xác minh thông tin liên hệ trong CV so với Form</summary>
    public class CvContactVerificationResultDto
    {
        [JsonPropertyName("is_match")]
        public bool IsMatch { get; set; }

        [JsonPropertyName("mismatch_details")]
        public string? MismatchDetails { get; set; }
    }
}
