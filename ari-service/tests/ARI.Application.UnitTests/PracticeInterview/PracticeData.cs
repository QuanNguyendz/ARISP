using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;

namespace ARI.Application.UnitTests.PracticeInterview;

/// <summary>
/// Factory + fake AI/TTS cho test Luồng 6 — Practice Interview (UC-42/43): tiến hành buổi thử
/// (StartSession/SaveAnswer/GenerateNextQuestion/EndSession/Timeout) và xem lại (PortalPracticeFeature).
/// </summary>
internal static class PracticeData
{
    /// <summary>Job KHÔNG set persona → StartSessionAsync bỏ qua nhánh avatar (không chạm stub media).</summary>
    public static JobPosting Job(string language = "vi", string title = "Backend Developer") => new()
    {
        Title = title,
        JobDescription = "Mô tả công việc backend",
        DetectedLanguage = language,
        ScoringRubric = "{}",
        Status = "active",
    };

    public static ARI.Domain.Entities.Application App(
        Guid jobId, Guid? accountId = null, string status = "interview", string email = "cand@example.io",
        string name = "Nguyen Van A", string? cvText = "Kinh nghiệm C#") => new()
    {
        JobPostingId = jobId,
        CandidateAccountId = accountId,
        CandidateEmail = email,
        CandidateName = name,
        CvText = cvText,
        Status = status,
    };

    public static InterviewSession Session(
        Guid appId, int round = 1, string type = "practice", string status = "active",
        string language = "vi", string? reportLanguage = null, DateTimeOffset? startedAt = null,
        string roundType = "screening") => new()
    {
        ApplicationId = appId,
        RoundNumber = round,
        RoundType = roundType,
        SessionType = type,
        InterviewLanguage = language,
        ReportLanguage = reportLanguage,
        Status = status,
        StartedAt = startedAt ?? DateTimeOffset.UtcNow,
    };

    public static Question Question(Guid sessionId, int seq, string text = "Câu hỏi?") => new()
    {
        SessionId = sessionId,
        SequenceNumber = seq,
        QuestionText = text,
        QuestionType = "ai_generated",
        DifficultyLevel = 3,
        Source = "ai_generated",
    };

    public static Answer Answer(Guid sessionId, Guid questionId, string transcript = "Câu trả lời của tôi") => new()
    {
        SessionId = sessionId,
        QuestionId = questionId,
        Transcript = transcript,
    };

    public static Evaluation Eval(Guid sessionId, Guid appId, int round = 1, string type = "practice") => new()
    {
        SessionId = sessionId,
        ApplicationId = appId,
        RoundNumber = round,
        SessionType = type,
        OverallScore = 72m,
    };

    public static CandidateAccount Account(Guid id, string email = "cand@example.io") => new()
    {
        Id = id,
        Email = email,
        FullName = "Nguyen Van A",
    };

    public static PlaybookDocument MustAsk(Guid jobId, string text = "Bắt buộc hỏi điều này") => new()
    {
        Scope = "job_posting",
        ScopeRefId = jobId,
        DocumentType = "must_ask",
        ParsedText = text,
    };
}

/// <summary>AI provider điều khiển được: câu hỏi/đánh giá/phân tích/ngôn ngữ trả về theo cấu hình, đếm số lần chấm.</summary>
internal sealed class StubAiProvider : IAIProvider
{
    public string QuestionText { get; set; } = "Câu hỏi tiếp theo là gì?";
    public EvaluationReport Evaluation { get; set; } = new() { Verdict = "pass", Score = 80m, Reasoning = "Ổn", RecommendedNextStep = "next" };
    public AnswerAnalysis Analysis { get; set; } = new() { DifficultyLevel = 4, Feedback = "Tốt" };
    public LanguageAssessment Language { get; set; } = new() { OverallScore = 75m, CefrLevel = "B2" };
    public int EvaluationCallCount { get; private set; }
    public int QuestionCallCount { get; private set; }

    public async IAsyncEnumerable<string> StreamQuestionAsync(QuestionContext ctx, [EnumeratorCancellation] CancellationToken ct)
    {
        QuestionCallCount++;
        await Task.CompletedTask;
        yield return QuestionText;
    }

    public Task<AnswerAnalysis> AnalyzeAnswerAsync(AnswerContext ctx, CancellationToken ct) => Task.FromResult(Analysis);
    public Task<EvaluationReport> GenerateEvaluationAsync(SessionContext ctx, CancellationToken ct)
    {
        EvaluationCallCount++;
        return Task.FromResult(Evaluation);
    }
    public Task<string> DetectLanguageRequirementAsync(string jdText, CancellationToken ct) => Task.FromResult("vi");
    public Task<LanguageAssessment> AssessLanguageProficiencyAsync(SessionContext ctx, CancellationToken ct) => Task.FromResult(Language);
    public Task<string> CompleteJsonAsync(string systemInstruction, string userContent, CancellationToken ct = default) => Task.FromResult("{}");
}

/// <summary>TTS điều khiển được: trả base64 cấu hình sẵn + đếm số lần gọi.</summary>
internal sealed class RecordingTtsService : ITTSService
{
    public string Base64 { get; set; } = "AUDIO64";
    public int CallCount { get; private set; }

    public Task<Stream> TextToSpeechAsync(string text, string voiceId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<string> TextToSpeechBase64PcmAsync(string text, string voiceId, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(Base64);
    }
}
