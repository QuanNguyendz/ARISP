using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ARI.Application.Interfaces;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>Email service giả — ghi lại thư đã gửi để assert; công tắc ném lỗi để test đường bắt lỗi gửi email.</summary>
public sealed class RecordingEmailService : IEmailService
{
    public List<(string To, string Subject, string Html)> Sent { get; } = new();

    /// <summary>Nếu true: <see cref="SendEmailAsync"/> ném lỗi (mô phỏng SMTP chết).</summary>
    public bool ThrowOnSend { get; set; }

    public Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        if (ThrowOnSend) throw new InvalidOperationException("email down");
        Sent.Add((toEmail, subject, htmlMessage));
        return Task.CompletedTask;
    }
}

/// <summary>RAG ingestion giả — ghi lại tài liệu đã đẩy (sourceType/sourceId) để assert, không gọi service Python.</summary>
public sealed class RecordingRagIngestionService : IRagIngestionService
{
    public List<(string SourceType, Guid SourceId, string Text)> Ingested { get; } = new();

    public Task<int> IngestAsync(string sourceType, Guid sourceId, string text, string? scope = null,
        string? documentType = null, bool replaceExisting = true, System.Threading.CancellationToken ct = default)
    {
        Ingested.Add((sourceType, sourceId, text));
        return Task.FromResult(1);
    }
}
