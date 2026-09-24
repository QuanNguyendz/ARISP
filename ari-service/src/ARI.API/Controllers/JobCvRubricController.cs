using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CvScoring;
using ARI.Application.Interfaces;
using ARI.Application.Playbooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Bộ tiêu chí chấm CV của MỘT tin (ADR-070). Mọi thành viên đội đọc được; chỉ Hiring Manager chính
    /// (hoặc quản trị viên) lưu được — quyền thật do command quyết định. Lưu bản mới là mọi hồ sơ của tin
    /// được chấm lại ở nền.
    /// </summary>
    [ApiController]
    [Route("api/jobs/{jobId:guid}/cv-rubric")]
    [Authorize(Policy = "InternalStaff")]
    public class JobCvRubricController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public JobCvRubricController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> Get(Guid jobId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetJobCvRubricQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }

        [HttpPut]
        public async Task<IActionResult> Save(Guid jobId, [FromBody] SaveCvRubricRequest body, CancellationToken ct)
        {
            var result = await _sender.Send(new SaveJobCvRubricCommand(
                jobId, body?.Criteria ?? new List<CvRubricCriterionInput>(), _currentUser.UserId, _currentUser.Role,
                body?.Policy), ct);
            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }

        /// <summary>
        /// Xem trước tác động của bản nháp bộ tiêu chí / công thức lên mọi hồ sơ của tin (ADR-075) — tính trong bộ nhớ,
        /// không ghi, không gọi AI. Cùng quyền với lưu.
        /// </summary>
        [HttpPost("preview")]
        public async Task<IActionResult> Preview(Guid jobId, [FromBody] SaveCvRubricRequest body, CancellationToken ct)
        {
            var result = await _sender.Send(new PreviewJobCvRubricQuery(
                jobId, body?.Criteria ?? new List<CvRubricCriterionInput>(), body?.Policy,
                _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }

        /// <summary>Tải bộ tiêu chí đang dùng về dạng Excel (kèm sheet công thức).</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(Guid jobId, CancellationToken ct)
        {
            var current = await _sender.Send(new GetJobCvRubricQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            if (current.IsFailure)
                return PlaybookUpload.MapFailure(this, current.ErrorCode, current.Error);

            var file = await _sender.Send(new ExportCvRubricSheetQuery(current.Value!.Criteria, current.Value.Policy), ct);
            return file.IsFailure
                ? BadRequest(new { message = file.Error, code = file.ErrorCode })
                : File(file.Value!, RubricSheet.XlsxContentType, "bo-tieu-chi-cham-cv.xlsx");
        }
    }

    public class SaveCvRubricRequest
    {
        public List<CvRubricCriterionInput> Criteria { get; set; } = new();

        /// <summary>Công thức cấp tin (ADR-075); bỏ trống = mặc định.</summary>
        public CvScoringPolicy? Policy { get; set; }

        /// <summary>Chỉ dùng ở <c>export-sheet</c>: <c>interview</c> = bộ tiêu chí phỏng vấn (không cột J–K, không sheet công thức).</summary>
        public string? Mode { get; set; }
    }
}
