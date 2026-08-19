using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Dịch vụ đóng dấu duyệt JD giả (UC-80): đếm số lần đóng dấu PDF/từ-text và trả bytes cố định;
/// công tắc ném lỗi để chứng minh đóng dấu thất bại KHÔNG chặn việc duyệt tin.
/// </summary>
internal sealed class RecordingJdStampService : IJdStampService
{
    public int StampPdfCallCount { get; private set; }
    public int StampFromTextCallCount { get; private set; }
    public bool ThrowOnStamp { get; set; }

    private static readonly byte[] Stamped = { 9, 9, 9 };

    public Task<byte[]> StampApprovalAsync(byte[] pdfBytes, string approverName, DateTimeOffset approvedAt, CancellationToken ct = default)
    {
        if (ThrowOnStamp) throw new InvalidOperationException("stamp failed");
        StampPdfCallCount++;
        return Task.FromResult(Stamped);
    }

    public Task<byte[]> StampApprovalFromTextAsync(string title, string bodyText, string approverName, DateTimeOffset approvedAt, CancellationToken ct = default)
    {
        if (ThrowOnStamp) throw new InvalidOperationException("stamp failed");
        StampFromTextCallCount++;
        return Task.FromResult(Stamped);
    }
}

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

    /// <summary>Khi set: <see cref="ExtractJobFromJdAsync"/> ném lỗi (case "Gemini throws" của test-plan).</summary>
    public Exception? ExtractThrows { get; set; }

    public Task<Result<JdExtractionResultDto>> ExtractJobFromJdAsync(
        byte[]? jdFileBytes, string? jdMimeType, string? fallbackJdText, CancellationToken ct = default)
    {
        ExtractCallCount++;
        LastPdfBytes = jdFileBytes;
        LastMimeType = jdMimeType;
        LastFallbackText = fallbackJdText;
        if (ExtractThrows != null) throw ExtractThrows;
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
