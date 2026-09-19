using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CvScoring;
using ARI.Application.Interfaces;
using ARI.Application.InterviewRubrics;
using ARI.Application.Playbooks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Bộ tiêu chí chấm PHỎNG VẤN của MỘT tin (ADR-073): bộ chung + bộ riêng theo vòng. Mọi thành viên đội đọc
    /// được; chỉ Hiring Manager chính (hoặc quản trị viên) lưu được — quyền thật do command quyết định. Lưu xong
    /// là các buổi phỏng vấn đang chờ bộ tiêu chí tự được chấm ở nền.
    /// </summary>
    [ApiController]
    [Route("api/jobs/{jobId:guid}/interview-rubric")]
    [Authorize(Policy = "InternalStaff")]
    public class JobInterviewRubricController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public JobInterviewRubricController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> Get(Guid jobId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetJobInterviewRubricQuery(jobId, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }

        /// <summary>Lưu bộ chung (không có <c>roundNumber</c>) hoặc bộ riêng của một vòng.</summary>
        [HttpPut]
        public async Task<IActionResult> Save(Guid jobId, [FromBody] SaveInterviewRubricRequest body, CancellationToken ct)
        {
            var result = await _sender.Send(new SaveJobInterviewRubricCommand(
                jobId, body?.RoundNumber, body?.Criteria ?? new List<CvRubricCriterionInput>(),
                _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }

        /// <summary>Bỏ bộ riêng của một vòng — vòng quay về dùng bộ chung.</summary>
        [HttpDelete("rounds/{round:int}")]
        public async Task<IActionResult> RemoveRound(Guid jobId, int round, CancellationToken ct)
        {
            var result = await _sender.Send(new RemoveJobInterviewRubricRoundCommand(
                jobId, round, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? PlaybookUpload.MapFailure(this, result.ErrorCode, result.Error) : Ok(result.Value);
        }
    }

    public class SaveInterviewRubricRequest
    {
        public int? RoundNumber { get; set; }
        public List<CvRubricCriterionInput> Criteria { get; set; } = new();
    }
}
