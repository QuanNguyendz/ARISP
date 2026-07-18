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
        private readonly IRagIngestionService _ragIngestion;

        public PlaybookService(IUnitOfWork unitOfWork, IRagIngestionService ragIngestion)
        {
            _unitOfWork = unitOfWork;
            _ragIngestion = ragIngestion;
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

            // Chunk + embed + lưu pgvector — do RAG service (Python) sở hữu (ADR-039).
            if (!string.IsNullOrEmpty(parsedText))
            {
                await _ragIngestion.IngestAsync(
                    sourceType: "playbook",
                    sourceId: document.Id,
                    text: parsedText,
                    scope: scope,
                    documentType: documentType,
                    ct: ct);
            }

            return document;
        }
    }
}
