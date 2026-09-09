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
    public List<(string FileName, string ContentType, StorageFolder Folder)> Saved { get; } = new();
    public List<string> Deleted { get; } = new();
    public bool ThrowOnSave { get; set; }

    /// <summary>Khi set: <see cref="DeleteAsync"/> ném lỗi (case "cleanup DeleteAsync throws" của test-plan).</summary>
    public Exception? DeleteThrows { get; set; }

    /// <summary>Khi set: <see cref="GetUrlAsync"/> ném lỗi (case "file storage throws URL error").</summary>
    public Exception? GetUrlThrows { get; set; }

    /// <summary>Khi set: <see cref="GetDownloadUrlAsync"/> ném lỗi (case "GetDownloadUrlAsync throws Download Error").</summary>
    public Exception? GetDownloadUrlThrows { get; set; }

    /// <summary>Khi set: <see cref="ReadAllBytesAsync"/> ném lỗi (case "profile CV read throws Read Error").</summary>
    public Exception? ReadThrows { get; set; }

    /// <summary>Giá trị URL trả về từ <see cref="GetUrlAsync"/> (mặc định "/files/{key}").</summary>
    public string? GetUrlResult { get; set; }

    /// <summary>Bytes trả về khi <see cref="ReadAllBytesAsync"/> được gọi (null = không tìm thấy file).</summary>
    public byte[]? FileBytes { get; set; }

    public Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, StorageFolder folder, CancellationToken ct = default)
    {
        if (ThrowOnSave) throw new InvalidOperationException("storage down");
        Saved.Add((originalFileName, contentType, folder));
        return Task.FromResult($"{folder.ToSegment()}/{originalFileName}");
    }

    public Task<string> GetUrlAsync(string storageKey, CancellationToken ct = default)
    {
        if (GetUrlThrows != null) throw GetUrlThrows;
        return Task.FromResult(GetUrlResult ?? $"/files/{storageKey}");
    }
    public Task<string> GetDownloadUrlAsync(string storageKey, string downloadFileName, CancellationToken ct = default)
    {
        if (GetDownloadUrlThrows != null) throw GetDownloadUrlThrows;
        return Task.FromResult($"/files/{storageKey}");
    }
    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        if (DeleteThrows != null) throw DeleteThrows;
        Deleted.Add(storageKey);
        return Task.CompletedTask;
    }
    public Task<byte[]?> ReadAllBytesAsync(string storageKey, CancellationToken ct = default)
    {
        if (ReadThrows != null) throw ReadThrows;
        return Task.FromResult(FileBytes);
    }
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
