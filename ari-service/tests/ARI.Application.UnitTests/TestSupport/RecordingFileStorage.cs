using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// File storage giả — ghi lại file đã lưu và trả về storageKey ổn định; công tắc ném lỗi để test
/// đường lỗi lưu file. Không đụng đĩa/S3.
/// </summary>
public sealed class RecordingFileStorage : IFileStorageService
{
    public List<(string FileName, string ContentType)> Saved { get; } = new();
    public List<string> Deleted { get; } = new();
    public bool ThrowOnSave { get; set; }

    /// <summary>Bytes trả về khi <see cref="ReadAllBytesAsync"/> được gọi (null = không tìm thấy file).</summary>
    public byte[]? FileBytes { get; set; }

    public Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        if (ThrowOnSave) throw new InvalidOperationException("storage down");
        Saved.Add((originalFileName, contentType));
        return Task.FromResult($"stored/{originalFileName}");
    }

    public Task<string> GetUrlAsync(string storageKey, CancellationToken ct = default) => Task.FromResult($"/files/{storageKey}");
    public Task<string> GetDownloadUrlAsync(string storageKey, string downloadFileName, CancellationToken ct = default) => Task.FromResult($"/files/{storageKey}");
    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        Deleted.Add(storageKey);
        return Task.CompletedTask;
    }
    public Task<byte[]?> ReadAllBytesAsync(string storageKey, CancellationToken ct = default) => Task.FromResult(FileBytes);
}

/// <summary>Parser tài liệu giả (dùng chung) — trả text cố định; công tắc ném lỗi để test đường parse fail.</summary>
public sealed class StubDocumentParser : IDocumentParserService
{
    public string Text { get; set; } = "parsed document text";
    public bool ThrowOnParse { get; set; }

    public Task<string> ParseDocumentAsync(System.IO.Stream stream, string fileExtension)
    {
        if (ThrowOnParse) throw new InvalidOperationException("parse failed");
        return Task.FromResult(Text);
    }
}
