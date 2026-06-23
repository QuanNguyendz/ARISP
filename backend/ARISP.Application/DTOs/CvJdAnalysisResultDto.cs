using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ARISP.Application.DTOs
{
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

        [JsonPropertyName("match_score")]
        public int MatchScore { get; set; }

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
}
