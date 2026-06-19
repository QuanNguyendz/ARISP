using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.Application.Services
{
    public class PlaybookService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmbeddingProvider _embeddingProvider;

        public PlaybookService(IUnitOfWork unitOfWork, IEmbeddingProvider embeddingProvider)
        {
            _unitOfWork = unitOfWork;
            _embeddingProvider = embeddingProvider;
        }

        public async Task<PlaybookDocument> UploadPlaybookAsync(Guid uploadedByUserId, string scope, Guid? scopeRefId, int? roundNumber, string documentType, string fileName, string fileUrl, string fileFormat, string parsedText, CancellationToken ct = default)
        {
            var document = new PlaybookDocument
            {
                Scope = scope,
                ScopeRefId = scopeRefId,
                RoundNumber = roundNumber,
                DocumentType = documentType,
                FileName = fileName,
                FileUrl = fileUrl,
                FileFormat = fileFormat,
                ParsedText = parsedText,
                Status = "ready",
                UploadedByUserId = uploadedByUserId
            };

            await _unitOfWork.Repository<PlaybookDocument>().AddAsync(document, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Chunk & Embed text for RAG search
            if (!string.IsNullOrEmpty(parsedText))
            {
                var chunks = ChunkText(parsedText);
                int chunkIndex = 0;
                foreach (var chunkText in chunks)
                {
                    var embedding = await _embeddingProvider.EmbedAsync(chunkText, ct);
                    var embeddingString = $"[{string.Join(",", embedding)}]";
                    var chunkId = Guid.NewGuid();
                    var createdAt = DateTimeOffset.UtcNow;
                    var metadataJson = $"{{\"scope\": \"{scope}\", \"document_type\": \"{documentType}\"}}";

                    await _unitOfWork.ExecuteSqlRawAsync(
                        "INSERT INTO document_chunks (id, source_type, source_id, chunk_index, chunk_text, embedding, metadata, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}::vector, {6}::jsonb, {7})",
                        new object[] { chunkId, "playbook", document.Id, chunkIndex++, chunkText, embeddingString, metadataJson, createdAt },
                        ct
                    );
                }
            }

            return document;
        }

        private List<string> ChunkText(string text)
        {
            // Simple chunking by paragraph or length for prototype
            var chunks = new List<string>();
            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var para in paragraphs)
            {
                if (para.Length < 1000)
                {
                    chunks.Add(para.Trim());
                }
                else
                {
                    // split into chunks of ~500 chars
                    int index = 0;
                    while (index < para.Length)
                    {
                        int length = Math.Min(500, para.Length - index);
                        chunks.Add(para.Substring(index, length).Trim());
                        index += length;
                    }
                }
            }
            return chunks;
        }
    }
}
