using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    public class QuestionContext
    {
        public Guid SessionId { get; set; }
        public Guid JobPostingId { get; set; }
        public Guid ApplicationId { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string CandidateCv { get; set; } = string.Empty;
        public string SessionType { get; set; } = "real"; // practice | real
        public List<QuestionAnswerDto> ChatHistory { get; set; } = new();
        public List<string> MustAskQuestions { get; set; } = new();
        public List<string> PlaybookStyleGuides { get; set; } = new();
        /// <summary>Ngôn ngữ phỏng vấn (ISO code, ADR-018) — AI hỏi + nhắc ứng viên theo ngôn ngữ này.</summary>
        public string? Language { get; set; }
        /// <summary>Buộc kết thúc NGAY (đạt cap số câu): AI chỉ sinh lời cảm ơn, không hỏi thêm.</summary>
        public bool ForceClosing { get; set; }
    }

    public class AnswerContext
    {
        public string QuestionText { get; set; } = string.Empty;
        public string AnswerTranscript { get; set; } = string.Empty;
    }

    public class SessionContext
    {
        public Guid SessionId { get; set; }
        public string JobDescription { get; set; } = string.Empty;
        public string CandidateCv { get; set; } = string.Empty;
        public string SessionType { get; set; } = "real";
        public List<QuestionAnswerDto> ChatHistory { get; set; } = new();
        public string ScoringRubric { get; set; } = "{}";
        /// <summary>Ngôn ngữ phỏng vấn yêu cầu — đánh giá mức tuân thủ trong Language Assessment.</summary>
        public string? Language { get; set; }
    }

    public class QuestionAnswerDto
    {
        public int SequenceNumber { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string AnswerText { get; set; } = string.Empty;
    }

    public class AnswerAnalysis
    {
        public int DifficultyLevel { get; set; } = 3;
        public string Feedback { get; set; } = string.Empty;
    }

    public class EvaluationReport
    {
        public string Verdict { get; set; } = "not_pass"; // pass | not_pass
        public decimal Score { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public string RecommendedNextStep { get; set; } = string.Empty;
        public string CriterionScoresJson { get; set; } = "{}";
        public string QuestionAnalysesJson { get; set; } = "[]";
    }

    public class LanguageAssessment
    {
        public decimal Fluency { get; set; }
        public decimal Grammar { get; set; }
        public decimal Vocabulary { get; set; }
        public decimal Comprehension { get; set; }
        public decimal OverallScore { get; set; }
        /// <summary>Nhận xét ngắn về mức tuân thủ ngôn ngữ phỏng vấn (ứng viên có trả lời đúng ngôn ngữ yêu cầu không).</summary>
        public string LanguageAdherence { get; set; } = string.Empty;
    }

    public interface IAIProvider
    {
        IAsyncEnumerable<string> StreamQuestionAsync(QuestionContext ctx, CancellationToken ct);
        Task<AnswerAnalysis> AnalyzeAnswerAsync(AnswerContext ctx, CancellationToken ct);
        Task<EvaluationReport> GenerateEvaluationAsync(SessionContext ctx, CancellationToken ct);
        Task<string> DetectLanguageRequirementAsync(string jdText, CancellationToken ct);
        Task<LanguageAssessment> AssessLanguageProficiencyAsync(SessionContext ctx, CancellationToken ct);

        /// <summary>
        /// Sinh JSON có cấu trúc một lần theo schema mô tả trong <paramref name="systemInstruction"/>.
        /// Dùng làm fallback khi nhà cung cấp chính (Gemini) lỗi/quá tải. Trả về chuỗi JSON thô.
        /// </summary>
        Task<string> CompleteJsonAsync(string systemInstruction, string userContent, CancellationToken ct = default);
    }
}
