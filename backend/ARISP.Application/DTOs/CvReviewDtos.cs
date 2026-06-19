using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ARISP.Application.DTOs
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

        [JsonIgnore] public int PromptTokens { get; set; }
        [JsonIgnore] public int CompletionTokens { get; set; }
    }

    /// <summary>Kết quả CV review trả về FE và lưu trong CandidateAccount.CvReviewJson.</summary>
    public class CvReviewResponse
    {
        public bool IsValidCv { get; set; }
        public int OverallScore { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> Improvements { get; set; } = new();
        public List<string> MissingSections { get; set; } = new();
        public string? ReviewedAt { get; set; }
    }
}
