using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;

namespace ARI.Application.UnitTests.CvAnalysis;

/// <summary>
/// Gemini provider giả: đếm số lần gọi phân tích (để chứng minh cache/reuse KHÔNG gọi lại AI)
/// và cho phép nạp sẵn kết quả (valid / invalid CV / lỗi). Các phương thức khác không dùng trong test này.
/// </summary>
internal sealed class FakeGeminiProvider : IGeminiProvider
{
    public int AnalyzeCallCount { get; private set; }

    public Result<CvJdAnalysisResultDto> AnalyzeResult { get; set; } =
        Result.Success(new CvJdAnalysisResultDto { IsValidCv = true, MatchScore = 75, Summary = "ok", Provider = "Gemini" });

    public Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(
        string jdText, byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText, CancellationToken ct = default)
    {
        AnalyzeCallCount++;
        return Task.FromResult(AnalyzeResult);
    }

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
}

/// <summary>Parser tài liệu giả — trả text cố định, không đọc file thật.</summary>
internal sealed class FakeDocumentParser : IDocumentParserService
{
    public string Text { get; set; } = "parsed cv text";
    public Task<string> ParseDocumentAsync(Stream stream, string fileExtension) => Task.FromResult(Text);
}
