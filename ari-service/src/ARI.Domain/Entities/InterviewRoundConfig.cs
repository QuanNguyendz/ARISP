using System;

namespace ARI.Domain.Entities
{
    public class InterviewRoundConfig
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid JobPostingId { get; set; }
        public int RoundNumber { get; set; }
        public string RoundType { get; set; } = "screening"; // screening | technical | online_test | hr | culture_fit
        public string? InterviewLanguage { get; set; }
        public int InterviewCodeTtlHours { get; set; } = 2;

        /// <summary>
        /// Thời lượng của vòng (phút). Với vòng <c>online_test</c> đây là NGUỒN DUY NHẤT của thời lượng bài
        /// thi — bài đóng lúc giờ hẹn + số phút này (ADR-072, xem <c>OnlineTestWindow</c>).
        /// </summary>
        public int MaxDurationMinutes { get; set; } = 45;
    }
}
