using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;

namespace ARISP.Application.Services
{
    /// <summary>
    /// Fallback ingestion CHẠY TRONG TIẾN TRÌNH .NET — dùng khi AI:Provider != "rag"
    /// (vd "openai" | "local"). Giữ nguyên hành vi chunk + embed + INSERT document_chunks như
    /// trước đây để hệ thống không khoá cứng vào service Python (xem verification fallback).
    /// Khi AI:Provider = "rag", DI map IRagIngestionService sang RagServiceProvider (gọi /ingest).
    /// </summary>
    public class LocalRagIngestionService : IRagIngestionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmbeddingProvider _embeddingProvider;

        public LocalRagIngestionService(IUnitOfWork unitOfWork, IEmbeddingProvider embeddingProvider)
        {
            _unitOfWork = unitOfWork;
            _embeddingProvider = embeddingProvider;
        }

        public async Task<int> IngestAsync(string sourceType, Guid sourceId, string text,
            string? scope = null, string? documentType = null, bool replaceExisting = true,
            CancellationToken ct = default)
        {
            if (replaceExisting)
            {
                await _unitOfWork.ExecuteSqlRawAsync(
                    "DELETE FROM document_chunks WHERE source_type = {0} AND source_id = {1}",
                    new object[] { sourceType, sourceId }, ct);
            }

            if (string.IsNullOrEmpty(text)) return 0;

            var chunks = ChunkText(text);
            var metadata = scope == null && documentType == null
                ? "{}"
                : JsonSerializer.Serialize(new { scope, document_type = documentType });

            int chunkIndex = 0;
            foreach (var chunkText in chunks)
            {
                var embedding = await _embeddingProvider.EmbedAsync(chunkText, ct);
                var embeddingString = $"[{string.Join(",", embedding)}]";

                await _unitOfWork.ExecuteSqlRawAsync(
                    "INSERT INTO document_chunks (id, source_type, source_id, chunk_index, chunk_text, embedding, metadata, created_at) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, {5}::vector, {6}::jsonb, {7})",
                    new object[] { Guid.NewGuid(), sourceType, sourceId, chunkIndex++, chunkText, embeddingString, metadata, DateTimeOffset.UtcNow },
                    ct);
            }
            return chunks.Count;
        }

        // Chunk theo đoạn (double newline); đoạn < 1000 ký tự giữ nguyên, ngược lại cắt ~500 ký tự.
        private static List<string> ChunkText(string text)
        {
            var chunks = new List<string>();
            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var para in paragraphs)
            {
                var trimmed = para.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.Length < 1000)
                {
                    chunks.Add(trimmed);
                }
                else
                {
                    int index = 0;
                    while (index < trimmed.Length)
                    {
                        int length = Math.Min(500, trimmed.Length - index);
                        var seg = trimmed.Substring(index, length).Trim();
                        if (seg.Length > 0) chunks.Add(seg);
                        index += length;
                    }
                }
            }
            return chunks;
        }
    }
}
