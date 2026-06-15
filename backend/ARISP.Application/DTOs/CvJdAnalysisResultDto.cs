using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ARISP.Application.DTOs
{
    public class CvJdAnalysisResultDto
    {
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
    }
}
