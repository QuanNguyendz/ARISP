using System;

namespace ARI.Domain.Entities
{
    public class OnlineTestQuestion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string Options { get; set; } = "[]"; // JSONB – e.g. ["A", "B", "C", "D"]
        /// <summary>single = chọn 1 đáp án | multiple = chọn nhiều đáp án.</summary>
        public string QuestionType { get; set; } = "single"; // single | multiple
        /// <summary>Chỉ số các đáp án đúng (0-based), JSONB – e.g. [0] hoặc [0,2]. Nguồn chấm điểm.</summary>
        public string CorrectOptions { get; set; } = "[]";
        public int CorrectOption { get; set; }       // legacy (0-based) — giữ đồng bộ = phần tử đầu của CorrectOptions
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
