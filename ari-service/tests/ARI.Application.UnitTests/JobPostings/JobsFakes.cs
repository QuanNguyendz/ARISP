using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Gemini giả cho trích xuất JD (UC-45): nạp sẵn kết quả + ghi lại tham số lần gọi cuối
/// (để kiểm tra PDF gửi inline còn DOCX dùng fallback text). Các phương thức khác không dùng.
/// </summary>
internal sealed class FakeJdGeminiProvider : IGeminiProvider
{
    public Result<JdExtractionResultDto> ExtractResult { get; set; } =
        Result.Success(new JdExtractionResultDto { IsValidJd = true, Title = "Backend Engineer" });

    public byte[]? LastPdfBytes { get; private set; }
    public string? LastMimeType { get; private set; }
    public string? LastFallbackText { get; private set; }
    public int ExtractCallCount { get; private set; }

    public Task<Result<JdExtractionResultDto>> ExtractJobFromJdAsync(
        byte[]? jdFileBytes, string? jdMimeType, string? fallbackJdText, CancellationToken ct = default)
    {
        ExtractCallCount++;
        LastPdfBytes = jdFileBytes;
        LastMimeType = jdMimeType;
        LastFallbackText = fallbackJdText;
        return Task.FromResult(ExtractResult);
    }

    public Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(
        string jdText, byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result<CvReviewResultDto>> ReviewCvAsync(
        byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result<CvContactVerificationResultDto>> VerifyCvContactInfoAsync(
        byte[]? cvFileBytes, string? cvMimeType, string? fallbackCvText,
        string formName, string formPhone, string formEmail, CancellationToken ct = default)
        => throw new NotImplementedException();
}
