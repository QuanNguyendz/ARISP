using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Scheduling;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Ứng viên xem lịch phỏng vấn đã được nhân sự xếp (read-only).
    /// Từ ADR-048, ứng viên KHÔNG tự chọn lịch — HR gán trực tiếp qua /api/schedules/assign.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class CandidateScheduleController : ControllerBase
    {
        private readonly ISender _sender;

        public CandidateScheduleController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lịch phỏng vấn của ứng viên đang đăng nhập (sắp tới / đã qua).</summary>
        [HttpGet("candidate/schedule")]
        [Authorize(Policy = "CandidateOnly")]
        public async Task<IActionResult> GetMySchedule(CancellationToken ct)
        {
            var subClaim = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
            Guid.TryParse(subClaim, out var accId);

            var result = await _sender.Send(new GetCandidateScheduleQuery(accId, emailClaim), ct);
            var value = result.Value;
            return Ok(new { upcomingSlots = value.UpcomingSlots, pastSlots = value.PastSlots });
        }
    }
}
