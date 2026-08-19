using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Playbooks.Commands.UploadPlaybook
{
    /// <summary>
    /// Upload playbook: parse text → lưu file → tạo PlaybookDocument → chunk+embed vào RAG (ADR-039).
    /// Absorb từ PlaybookService cũ (1 consumer duy nhất). Lỗi ingest/persist → xoá file đã lưu.
    /// </summary>
    public record UploadPlaybookCommand(
        Guid UserId, string Scope, Guid? ScopeRefId, int? RoundNumber, string DocumentType,
        string FileName, byte[] Bytes, string Ext) : IRequest<Result<UploadedPlaybookDto>>;

    public class UploadPlaybookCommandHandler : IRequestHandler<UploadPlaybookCommand, Result<UploadedPlaybookDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDocumentParserService _documentParser;
        private readonly IFileStorageService _fileStorage;
        private readonly IRagIngestionService _ragIngestion;

        public UploadPlaybookCommandHandler(
            IUnitOfWork unitOfWork,
            IDocumentParserService documentParser,
            IFileStorageService fileStorage,
            IRagIngestionService ragIngestion)
        {
            _unitOfWork = unitOfWork;
            _documentParser = documentParser;
            _fileStorage = fileStorage;
            _ragIngestion = ragIngestion;
        }

        public async Task<Result<UploadedPlaybookDto>> Handle(UploadPlaybookCommand request, CancellationToken ct)
        {
            var isRubric = ScoringRubric.IsRubricType(request.DocumentType);

            // Bộ tiêu chí chấm điểm là DỮ LIỆU (bảng Excel), không phải văn bản tự do: parse thành
            // tiêu chí + trọng số và chặn ngay nếu tổng ≠ 100 — sai ở đây mà lọt xuống thì mọi điểm
            // chấm về sau đều sai mà không ai biết (ADR-060).
            string? rubricJson = null;
            string parsedText;
            int? criteriaCount = null;

            if (isRubric)
            {
                if (!string.Equals(request.Ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
                    return Result.Failure<UploadedPlaybookDto>(
                        "Bộ tiêu chí chấm điểm phải là file Excel (.xlsx) theo mẫu. Hãy tải file mẫu rồi điền vào.");

                var parsed = RubricSheet.Parse(request.Bytes);
                var errors = parsed.Errors.Select(e => e.Row > 0 ? $"Dòng {e.Row}: {e.Message}" : e.Message).ToList();
                errors.AddRange(ScoringRubric.Validate(parsed.Criteria));
                if (errors.Count > 0)
                    return Result.Failure<UploadedPlaybookDto>(string.Join(" | ", errors.Take(10)));

                rubricJson = ScoringRubric.Serialize(parsed.Criteria);
                criteriaCount = parsed.Criteria.Count;
                // Văn bản cho RAG: chuẩn chấm từng tiêu chí để AI truy hồi khi cần diễn giải.
                parsedText = ScoringRubric.ToPromptText(parsed.Criteria);
            }
            else
            {
                // Parse text (md/txt: parser xử lý như text; pdf/docx: trích xuất)
                try
                {
                    using var stream = new MemoryStream(request.Bytes);
                    parsedText = (await _documentParser.ParseDocumentAsync(stream, request.Ext))?.Replace("\0", string.Empty) ?? string.Empty;
                }
                catch (Exception ex)
                {
                    return Result.Failure<UploadedPlaybookDto>($"Không thể đọc nội dung file: {ex.Message}");
                }
            }

            var contentType = request.Ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".md" => "text/markdown",
                ".xlsx" => RubricSheet.XlsxContentType,
                _ => "text/plain"
            };

            string storageKey;
            try
            {
                storageKey = await _fileStorage.SaveAsync(request.Bytes, request.FileName, contentType, StorageFolder.Playbook, ct);
            }
            catch (Exception ex)
            {
                return Result.Failure<UploadedPlaybookDto>($"Không thể lưu file: {ex.Message}", CommonErrorCodes.ServerError);
            }

            var fileFormat = request.Ext.TrimStart('.');

            try
            {
                var document = new PlaybookDocument
                {
                    Scope = request.Scope,
                    ScopeRefId = request.ScopeRefId,
                    RoundNumber = request.RoundNumber,
                    DocumentType = request.DocumentType.Trim(),
                    FileName = request.FileName,
                    FileUrl = storageKey,
                    FileFormat = fileFormat,
                    ParsedText = parsedText,
                    RubricJson = rubricJson,
                    Status = "ready",
                    UploadedByUserId = request.UserId
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
                        scope: request.Scope,
                        documentType: document.DocumentType,
                        ct: ct);
                }

                return Result.Success(new UploadedPlaybookDto(
                    document.Id, document.Scope, document.ScopeRefId, document.RoundNumber, document.DocumentType,
                    document.FileName, document.FileFormat, document.Status, document.CreatedAt,
                    criteriaCount));
            }
            catch (Exception ex)
            {
                await _fileStorage.DeleteAsync(storageKey, ct);
                return Result.Failure<UploadedPlaybookDto>($"Xử lý playbook thất bại: {ex.Message}", CommonErrorCodes.ServerError);
            }
        }
    }
}
