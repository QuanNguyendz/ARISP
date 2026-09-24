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
    /// Upload playbook: kiểm quyền + tính hợp lệ → parse text → lưu file → tạo PlaybookDocument →
    /// chunk+embed vào RAG (ADR-039). Lỗi ingest/persist → xoá file đã lưu.
    ///
    /// Hai cửa gọi vào đây — màn Playbook công ty và màn tin (ADR-069) — nên mọi luật (ai được viết phạm
    /// vi nào, loại tài liệu, đuôi file, vòng có thật không) nằm trong handler, không nằm ở controller.
    /// </summary>
    public record UploadPlaybookCommand(
        Guid UserId, string? ActorRole, string Scope, Guid? ScopeRefId, int? RoundNumber, string DocumentType,
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
            var scope = PlaybookAccess.NormalizeScope(request.Scope);
            if (scope == null)
                return Result.Failure<UploadedPlaybookDto>("Phạm vi phải là 'org', 'job_posting' hoặc 'round'.");

            var documentType = (request.DocumentType ?? string.Empty).Trim().ToLowerInvariant();
            if (!PlaybookAccess.DocumentTypes.Contains(documentType))
                return Result.Failure<UploadedPlaybookDto>("Loại tài liệu playbook không hợp lệ.");

            if (request.Bytes == null || request.Bytes.Length == 0)
                return Result.Failure<UploadedPlaybookDto>("File playbook không được để trống.");
            if (request.Bytes.LongLength > PlaybookAccess.MaxFileBytes)
                return Result.Failure<UploadedPlaybookDto>("Kích thước file không được vượt quá 15MB.");

            var ext = (request.Ext ?? string.Empty).Trim().ToLowerInvariant();
            if (!PlaybookAccess.AllowedExtensions(documentType).Contains(ext))
                return Result.Failure<UploadedPlaybookDto>(ScoringRubric.IsRubricType(documentType)
                    ? "Bộ tiêu chí chấm điểm phải là file Excel (.xlsx) theo mẫu. Hãy tải file mẫu rồi điền vào."
                    : "Định dạng không hợp lệ. Chấp nhận .pdf, .docx, .txt, .md");

            // ADR-070: bộ tiêu chí chấm CV của TIN có vòng đời riêng (một bản sống, lưu bản mới là chấm lại mọi
            // hồ sơ) nên chỉ đi qua trình soạn ở màn tin. Ở cấp công ty, nó là MẪU để HM chép.
            if (documentType == ScoringRubric.TypeCvRubric && scope != PlaybookScope.ScopeOrg)
                return Result.Failure<UploadedPlaybookDto>(
                    "Bộ tiêu chí chấm CV của tin được khai trong mục \"Bộ tiêu chí chấm CV\" ở màn tin (nhập được cả file Excel ở đó).");

            // ADR-073: cùng lý lẽ cho bộ tiêu chí chấm PHỎNG VẤN — mỗi (tin, vòng) một bộ sống, lưu xong là các
            // buổi đang chờ được chấm; chỉ trình soạn ở màn tin giữ được hai luật đó. Ở cấp công ty nó là MẪU.
            if (documentType == ScoringRubric.TypeInterviewRubric && scope != PlaybookScope.ScopeOrg)
                return Result.Failure<UploadedPlaybookDto>(
                    "Bộ tiêu chí chấm phỏng vấn của tin được khai trong mục \"Bộ tiêu chí chấm phỏng vấn\" ở màn tin (nhập được cả file Excel ở đó).");

            // Playbook công ty không gắn tin nào; playbook vòng phải gắn đúng một vòng hội thoại có thật.
            var scopeRefId = scope == PlaybookScope.ScopeOrg ? null : request.ScopeRefId;
            var roundNumber = scope == PlaybookScope.ScopeRound ? request.RoundNumber : null;

            var (accessError, accessCode) = await PlaybookAccess.CheckWriteAsync(
                _unitOfWork, scope, scopeRefId, request.UserId, request.ActorRole, ct);
            if (accessError != null)
                return accessCode == null
                    ? Result.Failure<UploadedPlaybookDto>(accessError)
                    : Result.Failure<UploadedPlaybookDto>(accessError, accessCode);

            if (scope == PlaybookScope.ScopeRound)
            {
                var roundError = await PlaybookAccess.CheckRoundAsync(_unitOfWork, scopeRefId!.Value, roundNumber, ct);
                if (roundError != null) return Result.Failure<UploadedPlaybookDto>(roundError);
            }

            var isRubric = ScoringRubric.IsRubricType(documentType);

            // Bộ tiêu chí chấm điểm là DỮ LIỆU (bảng Excel), không phải văn bản tự do: parse thành
            // tiêu chí + trọng số và chặn ngay nếu tổng ≠ 100 — sai ở đây mà lọt xuống thì mọi điểm
            // chấm về sau đều sai mà không ai biết (ADR-060).
            string? rubricJson = null;
            string? scoringPolicyJson = null;
            string parsedText;
            int? criteriaCount = null;

            if (isRubric)
            {
                var parsed = RubricSheet.Parse(request.Bytes);
                var errors = parsed.Errors.Select(e => e.Row > 0 ? $"Dòng {e.Row}: {e.Message}" : e.Message).ToList();
                // Bộ CV được có điều kiện bắt buộc / điểm tối thiểu / trọng số ý + sheet công thức (ADR-075);
                // bộ phỏng vấn thì không — dòng khai điều kiện bắt buộc bị từ chối kèm lời giải thích.
                var forCv = string.Equals(documentType, ScoringRubric.TypeCvRubric, StringComparison.Ordinal);
                errors.AddRange(ScoringRubric.Validate(parsed.Criteria, forCv ? RubricPurpose.Cv : RubricPurpose.Interview));
                if (forCv) errors.AddRange(CvScoringPolicy.Validate(parsed.Policy));
                if (errors.Count > 0)
                    return Result.Failure<UploadedPlaybookDto>(string.Join(" | ", errors.Take(10)));

                rubricJson = ScoringRubric.Serialize(parsed.Criteria);
                scoringPolicyJson = forCv ? CvScoringPolicy.ToStorage(parsed.Policy) : null;
                criteriaCount = parsed.Criteria.Count;
                // Văn bản cho RAG: chuẩn chấm từng tiêu chí để AI truy hồi khi cần diễn giải.
                parsedText = forCv ? ScoringRubric.ToCvPromptText(parsed.Criteria) : ScoringRubric.ToPromptText(parsed.Criteria);
            }
            else
            {
                // Parse text (md/txt: parser xử lý như text; pdf/docx: trích xuất)
                try
                {
                    using var stream = new MemoryStream(request.Bytes);
                    parsedText = (await _documentParser.ParseDocumentAsync(stream, ext))?.Replace("\0", string.Empty) ?? string.Empty;
                }
                catch (Exception ex)
                {
                    return Result.Failure<UploadedPlaybookDto>($"Không thể đọc nội dung file: {ex.Message}");
                }
            }

            var contentType = ext switch
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

            var fileFormat = ext.TrimStart('.');

            try
            {
                var document = new PlaybookDocument
                {
                    Scope = scope,
                    ScopeRefId = scopeRefId,
                    RoundNumber = roundNumber,
                    DocumentType = documentType,
                    FileName = request.FileName,
                    FileUrl = storageKey,
                    FileFormat = fileFormat,
                    ParsedText = parsedText,
                    RubricJson = rubricJson,
                    ScoringPolicyJson = scoringPolicyJson,
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
                        scope: scope,
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
