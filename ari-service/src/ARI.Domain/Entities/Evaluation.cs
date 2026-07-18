using System;

namespace ARISP.Domain.Entities
{
    public class Evaluation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public Guid ApplicationId { get; set; }
        public int RoundNumber { get; set; }
        public string SessionType { get; set; } = "real"; // practice | real
        public string AiVerdict { get; set; } = "not_pass"; // pass | not_pass
        public decimal? OverallScore { get; set; }
        public string? CriterionScores { get; set; } // JSON
        public string? Reasoning { get; set; }
        public string? RecommendedNextStep { get; set; }
        public string? QuestionAnalyses { get; set; } // JSON array
        public decimal? CheatScore { get; set; }
        public string? CheatSignals { get; set; } // JSON array
        public string? LanguageAssessment { get; set; } // JSON
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
