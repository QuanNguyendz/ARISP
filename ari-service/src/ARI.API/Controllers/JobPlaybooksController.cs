using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using ARI.Application.Playbooks.Commands.DeletePlaybook;
using ARI.Application.Playbooks.Commands.UploadPlaybook;
using ARI.Application.Playbooks.Queries.GetJobPlaybooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Playbook THEO TIN, quản lý ngay trong màn tin (ADR-069): Hiring Manager chính thêm/xoá tài liệu cho
    /// cả tin hoặc cho từng vòng; mọi thành viên đội đọc được. Policy chỉ chặn ngoài cửa (nhân sự nội bộ);
    /// quyền thật trên TỪNG tin do command/query quyết định.
    /// </summary>
    [ApiController]
    [Route("api/jobs/{jobId:guid}/playbooks")]
    [Authorize(Policy = "InternalStaff")]
    public class JobPlaybooksController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public JobPlaybooksController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        /// <summary>Playbook của tin (cả tin + từng vòng) kèm cờ <c>canManage</c> của người gọi.</summary>
        [HttpGet]
        public async Task<IActionResult> Get(Guid jobId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetJobPlaybooksQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }

        /// <summary>
        /// Thêm playbook cho tin. <c>scope</c> = <c>job_posting</c> (áp mọi vòng của tin) hoặc <c>round</c>
        /// (kèm <c>roundNumber</c>). Playbook công ty không đi qua đây.
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload(Guid jobId, [FromForm] JobPlaybookUploadForm form, CancellationToken ct)
        {
            var file = form.File;
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File playbook không được để trống." });
            if (file.Length > PlaybookAccess.MaxFileBytes)
                return BadRequest(new { message = "Kích thước file không được vượt quá 15MB." });

            var scope = PlaybookAccess.NormalizeScope(form.Scope);
            if (scope is not (PlaybookScope.ScopeJobPosting or PlaybookScope.ScopeRound))
                return BadRequest(new { message = "Playbook trong màn tin chỉ áp cho cả tin ('job_posting') hoặc một vòng ('round')." });

            var bytes = await PlaybookUpload.ReadAsync(file, ct);
            var result = await _sender.Send(new UploadPlaybookCommand(
                _currentUser.UserId ?? Guid.Empty, _currentUser.Role, scope, jobId, form.RoundNumber,
                form.DocumentType, file.FileName, bytes, Path.GetExtension(file.FileName) ?? string.Empty), ct);

            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }

        /// <summary>Xoá một playbook của tin — tài liệu phải thuộc đúng tin trên URL.</summary>
        [HttpDelete("{docId:guid}")]
        public async Task<IActionResult> Delete(Guid jobId, Guid docId, CancellationToken ct)
        {
            var result = await _sender.Send(
                new DeletePlaybookCommand(docId, _currentUser.UserId, _currentUser.Role, jobId), ct);
            if (result.IsFailure)
                return PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error);

            return Ok(new { message = "Đã xoá playbook.", id = docId });
        }
    }

    /// <summary>Form upload playbook của tin — tin lấy từ URL, không nhận <c>scopeRefId</c> từ client.</summary>
    public class JobPlaybookUploadForm
    {
        public IFormFile File { get; set; } = null!;
        public string Scope { get; set; } = "job_posting";
        public string DocumentType { get; set; } = string.Empty;
        public int? RoundNumber { get; set; }
    }
}
