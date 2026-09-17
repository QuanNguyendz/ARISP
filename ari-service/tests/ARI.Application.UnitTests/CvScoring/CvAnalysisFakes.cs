using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// Gemini provider giả: đếm số lần chấm (để chứng minh dùng lại kết quả / không chấm khi thiếu bộ tiêu
/// chí), ghi lại yêu cầu cuối cùng, và cho nạp sẵn kết quả chấm / gợi ý.
/// </summary>
internal sealed class FakeGeminiProvider : IGeminiProvider
{
    public int AnalyzeCallCount { get; private set; }
    public CvScoringAiRequest? LastRequest { get; private set; }

    public Result<CvJdAnalysisResultDto> AnalyzeResult { get; set; } = Result.Success(new CvJdAnalysisResultDto
    {
        IsValidCv = true,
        Summary = "ok",
        Provider = "Gemini",
        RawResponse = "{}",
    });

    public Result<List<CvRubricSuggestionItem>> SuggestResult { get; set; } =
        Result.Failure<List<CvRubricSuggestionItem>>("not configured");

    public Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(CvScoringAiRequest request, CancellationToken ct = default)
    {
        AnalyzeCallCount++;
        LastRequest = request;
        return Task.FromResult(AnalyzeResult);
    }

    public Task<Result<List<CvRubricSuggestionItem>>> SuggestCvRubricAsync(CvRubricSuggestionInput input, CancellationToken ct = default)
        => Task.FromResult(SuggestResult);

    public Task<Result<CvReviewResultDto>> ReviewCvAsync(
        byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result<JdExtractionResultDto>> ExtractJobFromJdAsync(
        byte[]? jdFileBytes, string? jdMimeType, string? fallbackJdText, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result<CvContactVerificationResultDto>> VerifyCvContactInfoAsync(
        byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText,
        string formName, string formPhone, string formEmail, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>Kết quả hợp lệ với điểm từng tiêu chí (key → score).</summary>
    public static Result<CvJdAnalysisResultDto> Scored(params (string Key, decimal? Score)[] scores)
        => Result.Success(new CvJdAnalysisResultDto
        {
            IsValidCv = true,
            Summary = "Ứng viên phù hợp",
            SkillsMatched = { "C#", "PostgreSQL" },
            SkillsGaps = { "Kubernetes" },
            SeniorityAlignment = "Đúng cấp bậc",
            Provider = "Gemini",
            RawResponse = "{}",
            Criteria = scores.Select(s => new CvCriterionAiResult
            {
                Key = s.Key,
                Score = s.Score,
                Evidence = $"bằng chứng {s.Key}",
                Reasoning = $"lý do {s.Key}",
            }).ToList(),
        });
}

/// <summary>Parser tài liệu giả — trả text cố định, không đọc file thật.</summary>
internal sealed class FakeDocumentParser : IDocumentParserService
{
    public string Text { get; set; } = "parsed cv text";
    public int Calls { get; private set; }
    public Task<string> ParseDocumentAsync(Stream stream, string fileExtension)
    {
        Calls++;
        return Task.FromResult(Text);
    }
}

/// <summary>Hàng đợi chấm CV giả — chỉ ghi lại việc được đưa vào.</summary>
internal sealed class RecordingCvScoringQueue : ICvScoringQueue
{
    public List<Guid> Applications { get; } = new();
    public List<Guid> Jobs { get; } = new();

    public void EnqueueApplication(Guid applicationId) => Applications.Add(applicationId);
    public void EnqueueJob(Guid jobPostingId) => Jobs.Add(jobPostingId);
    public ValueTask<CvScoringWorkItem> DequeueAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    public void Complete(Guid applicationId) { }
}

/// <summary>Dựng dữ liệu + dịch vụ cho test chấm CV (ADR-070).</summary>
internal static class CvScoringKit
{
    public static JobPosting Job() => new() { CreatedByUserId = Guid.NewGuid(), Title = "Backend Developer", Status = "active" };

    public static PlaybookDocument Rubric(Guid jobId, params (string Key, string Name, decimal Weight)[] rows)
        => new()
        {
            Scope = PlaybookScope.ScopeJobPosting,
            ScopeRefId = jobId,
            DocumentType = ScoringRubric.TypeCvRubric,
            FileName = "cv-rubric.xlsx",
            UploadedByUserId = Guid.NewGuid(),
            Status = "ready",
            RubricJson = ScoringRubric.Serialize(rows.Select(r => new RubricCriterion
            {
                Key = r.Key,
                Name = r.Name,
                Weight = r.Weight,
                Description = $"chuẩn {r.Key}",
            }).ToList()),
        };

    /// <summary>Bộ tiêu chí mặc định 70/30 của tin.</summary>
    public static PlaybookDocument DefaultRubric(Guid jobId)
        => Rubric(jobId, ("experience", "Kinh nghiệm", 70), ("education", "Học vấn", 30));

    public static CvScoringService Service(
        InMemoryUnitOfWork uow, FakeGeminiProvider gemini,
        FakeDocumentParser? parser = null, RecordingFileStorage? storage = null, CvScoringInFlight? inFlight = null)
        => new(uow, gemini, parser ?? new FakeDocumentParser(), storage ?? new RecordingFileStorage(),
            inFlight ?? new CvScoringInFlight(), new MemoryCache(new MemoryCacheOptions()),
            NullLogger<CvScoringService>.Instance);

    public static CvRubricService RubricService(
        InMemoryUnitOfWork uow, RecordingCvScoringQueue? queue = null,
        RecordingRagIngestionService? rag = null, RecordingFileStorage? storage = null)
        => new(uow, storage ?? new RecordingFileStorage(), rag ?? new RecordingRagIngestionService(),
            queue ?? new RecordingCvScoringQueue(), NullLogger<CvRubricService>.Instance);

    public static List<CvRubricCriterionInput> Inputs(params (string Name, decimal Weight)[] rows)
        => rows.Select(r => new CvRubricCriterionInput
        {
            Name = r.Name,
            Weight = r.Weight,
            Description = $"chuẩn chấm {r.Name}",
        }).ToList();

    /// <summary>
    /// ADR-070 — tin không có bộ tiêu chí chấm CV thì không gửi duyệt / đăng được. Test nào không nói về
    /// luật đó thì gieo sẵn cho mọi tin, để nó va vào đúng luật đang kiểm chứ không phải chốt chặn này.
    /// </summary>
    public static void EnsureRubricsForAllJobs(InMemoryUnitOfWork uow)
    {
        foreach (var job in uow.Repo<JobPosting>().Items.ToList())
        {
            var has = uow.Repo<PlaybookDocument>().Items.Any(p =>
                p.ScopeRefId == job.Id && p.DeletedAt == null
                && p.Scope == PlaybookScope.ScopeJobPosting && p.DocumentType == ScoringRubric.TypeCvRubric);
            if (!has) uow.Seed(DefaultRubric(job.Id));
        }
    }

    /// <summary>Bộ tiêu chí hợp lệ cho phiếu (ADR-070 — bắt buộc khi lập phiếu).</summary>
    public static List<CvRubricCriterionInput> SampleRubric() => Inputs(("Kinh nghiệm", 60), ("Kỹ năng", 40));

    public static string SampleRubricJson()
        => ScoringRubric.Serialize(CvRubricEditing.Normalize(SampleRubric()).Criteria);

    public static byte[] CvBytes(string content = "PDF-BINARY-CONTENT") => System.Text.Encoding.UTF8.GetBytes(content);
}
