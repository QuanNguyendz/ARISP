using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ARI.Domain.Entities
{
    /// <summary>
    /// Một lần chấm một file CV theo một tin và MỘT phiên bản bộ tiêu chí của tin đó (ADR-070).
    ///
    /// Khoá duy nhất là (tin, băm file CV, bộ tiêu chí): cùng file nộp lại thì dùng lại kết quả, nhưng
    /// Hiring Manager lưu bộ tiêu chí mới thì mọi hồ sơ được chấm lại — dòng cũ giữ nguyên làm lịch sử,
    /// hồ sơ chuyển sang trỏ vào dòng mới.
    /// </summary>
    public class CvJdAnalysis
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public string CvHash { get; set; } = string.Empty;   // để cache, không chạy lại AI

        /// <summary>
        /// Bộ tiêu chí (tài liệu playbook <c>cv_rubric</c> của tin) đã dùng để chấm. <c>null</c> chỉ có ở
        /// các bản chấm trước ADR-070 — thời AI còn tự cho điểm tổng; chúng được coi là lỗi thời.
        /// </summary>
        public Guid? RubricDocumentId { get; set; }

        /// <summary>
        /// 0–100, do BACKEND cộng có trọng số từ điểm từng tiêu chí. Không bao giờ là con số AI tự đưa ra.
        /// Bằng 0 và vô nghĩa khi <see cref="Status"/> là <c>invalid_cv</c>.
        /// </summary>
        public int MatchScore { get; set; }
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Ảnh chụp từng tiêu chí lúc chấm:
        /// <c>{key:{score,label,weight,description,levels,evidence,reasoning}}</c> (ADR-060/070).
        /// </summary>
        public string CriterionScores { get; set; } = "{}";  // JSONB

        public string SkillsMatched { get; set; } = "[]";    // JSONB
        public string SkillsGaps { get; set; } = "[]";       // JSONB
        public string RedFlags { get; set; } = "[]";         // JSONB
        public string ExperienceRelevance { get; set; } = string.Empty;
        public string OverallRecommendation { get; set; } = string.Empty;

        /// <summary>Phân tích khoảng cách cấp bậc JD ↔ CV — lưu để màn giải thích điểm hiện lại được.</summary>
        public string? SeniorityAlignment { get; set; }

        // System & Telemetry
        public string AiModel { get; set; } = "gemini-2.5-flash";

        /// <summary><c>completed</c> | <c>invalid_cv</c> — xem <see cref="Constants.CvAnalysisStatuses"/>.</summary>
        public string Status { get; set; } = Constants.CvAnalysisStatuses.Completed;
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
        [JsonPropertyName("tech_depth_analysis")]
        public string? TechDepthAnalysis { get; set; }
    }
}
