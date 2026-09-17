using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks.Commands.DeletePlaybook;
using ARI.Application.Playbooks;
using ARI.Application.Playbooks.Commands.UploadPlaybook;
using ARI.Application.Playbooks.Queries.GetPlaybooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Màn Playbook của HR Leader — nơi quản lý playbook CÔNG TY và nhìn toàn cảnh playbook các tin.
    /// Playbook THEO TIN do Hiring Manager thêm/xoá ngay trong màn tin (<see cref="JobPlaybooksController"/>,
    /// ADR-069). Mọi luật (ai được viết phạm vi nào, loại tài liệu, đuôi file) nằm trong command, dùng
    /// chung cho cả hai cửa.
    /// </summary>
    [ApiController]
    [Route("api/playbooks")]
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
        [Authorize(Policy = "HrManagement")]
        public async Task<IActionResult> GetPlaybooks([FromQuery] string? scope, CancellationToken ct)
        {
            var result = await _sender.Send(new GetPlaybooksQuery(scope), ct);
            return Ok(result.Value);
        }

        /// <summary>
        /// Tải file Excel mẫu để khai bộ tiêu chí chấm điểm (ADR-060).
        /// <c>type</c> = <c>cv_rubric</c> (chấm CV) hoặc <c>interview_rubric</c> (chấm phỏng vấn) —
        /// mẫu khác nhau vì tiêu chí chấm hồ sơ khác hẳn tiêu chí chấm buổi phỏng vấn.
        /// Mở cho mọi nhân sự nội bộ: file mẫu không chứa dữ liệu, và Hiring Manager cần nó để khai bộ
        /// tiêu chí cho tin của mình (ADR-069) — trước đây endpoint nằm sau policy của HR nên HM nhận 403.
        /// </summary>
        [HttpGet("rubric-template")]
        [Authorize(Policy = "InternalStaff")]
        public IActionResult GetRubricTemplate([FromQuery] string? type)
        {
            var forCv = string.Equals(type?.Trim(), ScoringRubric.TypeCvRubric, StringComparison.OrdinalIgnoreCase);
            var bytes = RubricSheet.BuildTemplate(forCv);
            var fileName = forCv ? "mau-tieu-chi-cham-cv.xlsx" : "mau-tieu-chi-cham-phong-van.xlsx";
            return File(bytes, RubricSheet.XlsxContentType, fileName);
        }

        // ---------------- Trình soạn bộ tiêu chí chấm CV (ADR-070) — công cụ điền nhanh, không lưu ----------------

        /// <summary>Đọc file Excel thành bản nháp cho trình soạn.</summary>
        [HttpPost("cv-rubric/parse-sheet")]
        [Authorize(Policy = "InternalStaff")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ParseCvRubricSheet(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Hãy chọn file Excel." });
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Bộ tiêu chí phải là file Excel (.xlsx)." });
            if (file.Length > PlaybookAccess.MaxFileBytes)
                return BadRequest(new { message = "Kích thước file không được vượt quá 15MB." });

            var bytes = await PlaybookUpload.ReadAsync(file, ct);
            var result = await _sender.Send(new ParseCvRubricSheetCommand(bytes), ct);
            return result.IsFailure ? BadRequest(new { message = result.Error, code = result.ErrorCode }) : Ok(result.Value);
        }

        /// <summary>Xuất bản nháp đang soạn ra file Excel.</summary>
        [HttpPost("cv-rubric/export-sheet")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> ExportCvRubricSheet([FromBody] SaveCvRubricRequest body, CancellationToken ct)
        {
            var result = await _sender.Send(new ExportCvRubricSheetQuery(body?.Criteria ?? new()), ct);
            return result.IsFailure
                ? BadRequest(new { message = result.Error, code = result.ErrorCode })
                : File(result.Value!, RubricSheet.XlsxContentType, "bo-tieu-chi-cham-cv.xlsx");
        }

        /// <summary>Mẫu bộ tiêu chí của công ty (HR Leader tải lên ở màn Playbook) để HM chép.</summary>
        [HttpGet("cv-rubric/templates")]
        [Authorize(Policy = "InternalStaff")]
        public async Task<IActionResult> GetCvRubricTemplates(CancellationToken ct)
        {
            var result = await _sender.Send(new GetCvRubricTemplatesQuery(), ct);
            return Ok(result.Value);
        }

        /// <summary>AI gợi ý bản nháp bộ tiêu chí từ nội dung phiếu / tin.</summary>
        // Mỗi lần gọi là một lượt Gemini có tính phí (cùng lý lẽ với analyze-jd), nên chỉ mở cho vai SOẠN được
        // bộ tiêu chí: HM (phiếu + màn tin) và quản trị viên. Recruiter chỉ đọc bộ tiêu chí.
        [HttpPost("cv-rubric/suggest")]
        [Authorize(Policy = "HiringDecision")]
        public async Task<IActionResult> SuggestCvRubric([FromBody] CvRubricSuggestionInput body, CancellationToken ct)
        {
            if (body == null) return BadRequest(new { message = "Thiếu nội dung để gợi ý." });
            var result = await _sender.Send(new SuggestCvRubricCommand(body), ct);
            return result.IsFailure ? BadRequest(new { message = result.Error, code = result.ErrorCode }) : Ok(result.Value);
        }

        /// <summary>Upload một tài liệu playbook (PDF/DOCX/TXT/MD; riêng bộ tiêu chí chấm điểm là .xlsx).</summary>
        [HttpPost]
        [Authorize(Policy = "HrManagement")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPlaybook([FromForm] UploadPlaybookForm form, CancellationToken ct)
        {
            var file = form.File;
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File playbook không được để trống." });
            if (file.Length > PlaybookAccess.MaxFileBytes)
                return BadRequest(new { message = "Kích thước file không được vượt quá 15MB." });

            var bytes = await PlaybookUpload.ReadAsync(file, ct);
            var result = await _sender.Send(new UploadPlaybookCommand(
                _currentUser.UserId ?? Guid.Empty, _currentUser.Role, form.Scope, form.ScopeRefId, form.RoundNumber,
                form.DocumentType, file.FileName, bytes, Path.GetExtension(file.FileName) ?? string.Empty), ct);

            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }

        /// <summary>Xoá mềm một playbook (và gỡ nội dung khỏi kho tri thức của AI).</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "HrManagement")]
        public async Task<IActionResult> DeletePlaybook(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new DeletePlaybookCommand(id, _currentUser.UserId, _currentUser.Role), ct);
            if (result.IsFailure)
                return PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error);

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

    /// <summary>Phần HTTP dùng chung của hai cửa upload playbook: đọc file và đổi mã lỗi thành status.</summary>
    internal static class PlaybookUpload
    {
        public static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            return ms.ToArray();
        }

        public static IActionResult MapFailure(ControllerBase c, string? errorCode, string? message) => errorCode switch
        {
            CommonErrorCodes.Forbidden => c.StatusCode(StatusCodes.Status403Forbidden, new { message }),
            CommonErrorCodes.NotFound => c.NotFound(new { message }),
            CommonErrorCodes.Conflict => c.Conflict(new { message }),
            CommonErrorCodes.ServerError => c.StatusCode(StatusCodes.Status500InternalServerError, new { message }),
            _ => c.BadRequest(new { message }),
        };
    }
}
