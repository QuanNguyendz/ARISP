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

        public async Task<PlaybookDocument> UploadPlaybookAsync(Guid organizationId, Guid uploadedByUserId, string scope, Guid? scopeRefId, int? roundNumber, string documentType, string fileName, string fileUrl, string fileFormat, string parsedText, CancellationToken ct = default)
        {
            var document = new PlaybookDocument
            {
                OrganizationId = organizationId,
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
                    var chunk = new DocumentChunk
                    {
                        OrganizationId = organizationId,
                        SourceType = "playbook",
                        SourceId = document.Id,
                        ChunkIndex = chunkIndex++,
                        ChunkText = chunkText,
                        Embedding = embedding,
                        Metadata = $"{{\"scope\": \"{scope}\", \"document_type\": \"{documentType}\"}}"
                    };
                    await _unitOfWork.Repository<DocumentChunk>().AddAsync(chunk, ct);
                }

                // If document type is "must_ask", seed must_ask tracking logic (for future interview sessions)
                // In actual deployment, sessions pull from here to generate must_ask_tracking
                await _unitOfWork.SaveChangesAsync(ct);
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
