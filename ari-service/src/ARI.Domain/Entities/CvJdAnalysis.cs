using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ARISP.Domain.Entities
{
    public class CvJdAnalysis
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public string CvHash { get; set; } = string.Empty;   // để cache, không chạy lại Gemini
        public int MatchScore { get; set; }                  // 0–100
        public string Summary { get; set; } = string.Empty;
        public string SkillsMatched { get; set; } = "[]";    // JSONB
        public string SkillsGaps { get; set; } = "[]";       // JSONB
        public string RedFlags { get; set; } = "[]";         // JSONB
        public string ExperienceRelevance { get; set; } = string.Empty;
        public string OverallRecommendation { get; set; } = string.Empty;
        
        // System & Telemetry
        public string AiModel { get; set; } = "gemini-2.5-flash";
        public string Status { get; set; } = "completed";
        public string? ErrorMessage { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int ProcessingTimeMs { get; set; }
        
        public string RawResponse { get; set; } = "{}";      // JSONB - lưu raw để debug
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [NotMapped]
        [JsonPropertyName("analysis_reasoning")]
        public string? AnalysisReasoning { get; set; }

        [NotMapped]
        [JsonPropertyName("seniority_alignment")]
        public string? SeniorityAlignment { get; set; }

        [NotMapped]
        [JsonPropertyName("tech_depth_analysis")]
        public string? TechDepthAnalysis { get; set; }
    }
}
