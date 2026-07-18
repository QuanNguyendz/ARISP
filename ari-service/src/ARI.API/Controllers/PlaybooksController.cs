using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using ARISP.Application.Services;
using ARISP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARISP.API.Controllers
{
    /// <summary>
    /// Quản lý Interview Playbook (tài liệu phỏng vấn nội bộ) — upload, liệt kê, xoá.
    /// Khi upload: parse text → lưu file → chunk + embed vào document_chunks cho RAG.
    /// </summary>
    [ApiController]
    [Route("api/playbooks")]
    [Authorize(Policy = "HrManagement")]
    public class PlaybooksController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly PlaybookService _playbookService;
        private readonly IDocumentParserService _documentParser;
        private readonly IFileStorageService _fileStorage;
        private readonly ICurrentUserService _currentUser;

        public PlaybooksController(
            IUnitOfWork unitOfWork,
            PlaybookService playbookService,
            IDocumentParserService documentParser,
            IFileStorageService fileStorage,
            ICurrentUserService currentUser)
        {
            _unitOfWork = unitOfWork;
            _playbookService = playbookService;
            _documentParser = documentParser;
            _fileStorage = fileStorage;
            _currentUser = currentUser;
        }

        /// <summary>Danh sách playbook (lọc theo scope nếu có). Không trả parsedText.</summary>
        [HttpGet]
        public async Task<IActionResult> GetPlaybooks([FromQuery] string? scope, CancellationToken ct)
        {
            // Projection ở tầng SQL — KHÔNG kéo cột parsedText (nội dung playbook rất lớn).
            var scopeLower = scope?.ToLowerInvariant();
            var docs = await _unitOfWork.Repository<PlaybookDocument>().QueryAsync(q =>
                (string.IsNullOrEmpty(scopeLower) ? q : q.Where(d => d.Scope.ToLower() == scopeLower))
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new
                    {
                        d.Id, d.Scope, d.ScopeRefId, d.RoundNumber, d.DocumentType,
                        d.FileName, d.FileFormat, d.Status, d.CreatedAt, d.UploadedByUserId,
                    }), ct);

            var uploaderIds = docs.Select(d => d.UploadedByUserId).Distinct().ToList();
            var uploaderNames = (await _unitOfWork.Repository<User>()
                    .QueryAsync(q => q.Where(u => uploaderIds.Contains(u.Id)).Select(u => new { u.Id, u.FullName, u.Email }), ct))
                .ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName);

            var items = docs.Select(d => new
            {
                d.Id,
                d.Scope,
                d.ScopeRefId,
                d.RoundNumber,
                d.DocumentType,
                d.FileName,
                d.FileFormat,
                d.Status,
                d.CreatedAt,
                UploadedBy = uploaderNames.TryGetValue(d.UploadedByUserId, out var n) ? n : null,
            });

            return Ok(items);
        }

        /// <summary>Upload một tài liệu playbook (PDF/DOCX/TXT/MD).</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPlaybook([FromForm] UploadPlaybookForm form, CancellationToken ct)
        {
            var file = form.File;
            var scope = form.Scope;
            var documentType = form.DocumentType;
            var scopeRefId = form.ScopeRefId;
            var roundNumber = form.RoundNumber;

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File playbook không được để trống." });
            if (file.Length > 15 * 1024 * 1024)
                return BadRequest(new { message = "Kích thước file không được vượt quá 15MB." });

            var allowedScopes = new[] { "org", "job_posting", "round" };
            scope = (scope ?? "org").Trim().ToLowerInvariant();
            if (!allowedScopes.Contains(scope))
                return BadRequest(new { message = "Scope phải là 'org', 'job_posting' hoặc 'round'." });

            if (string.IsNullOrWhiteSpace(documentType))
                return BadRequest(new { message = "documentType là bắt buộc." });

            if (scope == "job_posting" && (scopeRefId == null || scopeRefId == Guid.Empty))
                return BadRequest(new { message = "scopeRefId (JobPostingId) là bắt buộc khi scope = 'job_posting'." });
            if (scope == "round" && roundNumber == null)
                return BadRequest(new { message = "roundNumber là bắt buộc khi scope = 'round'." });

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowedExt = new[] { ".pdf", ".docx", ".txt", ".md" };
            if (string.IsNullOrEmpty(ext) || Array.IndexOf(allowedExt, ext) < 0)
                return BadRequest(new { message = "Định dạng không hợp lệ. Chấp nhận .pdf, .docx, .txt, .md" });

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            // Parse text (md/txt: parser xử lý như text; pdf/docx: trích xuất)
            string parsedText;
            try
            {
                using var stream = new MemoryStream(bytes);
                parsedText = (await _documentParser.ParseDocumentAsync(stream, ext))?.Replace("\0", string.Empty) ?? string.Empty;
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Không thể đọc nội dung file: {ex.Message}" });
            }

            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".md" => "text/markdown",
                _ => "text/plain"
            };

            string storageKey;
            try
            {
                storageKey = await _fileStorage.SaveAsync(bytes, file.FileName, contentType, ct);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Không thể lưu file: {ex.Message}" });
            }

            var userId = _currentUser.UserId ?? Guid.Empty;
            var fileFormat = ext.TrimStart('.');

            try
            {
                var doc = await _playbookService.UploadPlaybookAsync(
                    userId, scope, scopeRefId, roundNumber, documentType.Trim(), file.FileName, storageKey, fileFormat, parsedText, ct);

                return Ok(new
                {
                    doc.Id,
                    doc.Scope,
                    doc.ScopeRefId,
                    doc.RoundNumber,
                    doc.DocumentType,
                    doc.FileName,
                    doc.FileFormat,
                    doc.Status,
                    doc.CreatedAt
                });
            }
            catch (Exception ex)
            {
                await _fileStorage.DeleteAsync(storageKey, ct);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Xử lý playbook thất bại: {ex.Message}" });
            }
        }

        /// <summary>Xoá mềm một playbook.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeletePlaybook(Guid id, CancellationToken ct)
        {
            var doc = await _unitOfWork.Repository<PlaybookDocument>().GetByIdAsync(id, ct);
            if (doc == null)
                return NotFound(new { message = "Không tìm thấy playbook." });

            doc.DeletedAt = DateTimeOffset.UtcNow;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<PlaybookDocument>().Update(doc);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new { message = "Đã xoá playbook.", id });
        }
    }

    /// <summary>Form upload playbook (gói chung file + metadata để Swagger sinh được schema multipart).</summary>
    public class UploadPlaybookForm
    {
        public IFormFile File { get; set; } = null!;
        public string Scope { get; set; } = "org";
        public string DocumentType { get; set; } = string.Empty;
        public Guid? ScopeRefId { get; set; }
        public int? RoundNumber { get; set; }
    }
}
