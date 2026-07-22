using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Dashboard.Queries.GetHrDashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(Policy = "InternalStaff")]
    public class DashboardController : ControllerBase
    {
        private readonly ISender _sender;

        public DashboardController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// GET /api/dashboard/hr
        /// Tổng quan tuyển dụng cho HR: KPI, phễu tuyển dụng, ứng viên gần đây.
        /// </summary>
        [HttpGet("hr")]
        public async Task<IActionResult> GetHrOverview(CancellationToken ct)
        {
            var result = await _sender.Send(new GetHrDashboardQuery(), ct);
            return Ok(result.Value);
        }
    }
}
