using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks.Commands.DeletePlaybook;
using ARI.Application.Playbooks.Commands.UploadPlaybook;
using ARI.Application.Playbooks.Queries.GetPlaybooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
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
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public PlaybooksController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        /// <summary>Danh sách playbook (lọc theo scope nếu có). Không trả parsedText.</summary>
        [HttpGet]
        public async Task<IActionResult> GetPlaybooks([FromQuery] string? scope, CancellationToken ct)
        {
            var result = await _sender.Send(new GetPlaybooksQuery(scope), ct);
            return Ok(result.Value);
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

            var userId = _currentUser.UserId ?? Guid.Empty;

            var result = await _sender.Send(new UploadPlaybookCommand(
                userId, scope, scopeRefId, roundNumber, documentType, file.FileName, bytes, ext), ct);

            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.ServerError
                    ? StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        /// <summary>Xoá mềm một playbook.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeletePlaybook(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new DeletePlaybookCommand(id), ct);
            if (result.IsFailure)
                return NotFound(new { message = result.Error });

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
