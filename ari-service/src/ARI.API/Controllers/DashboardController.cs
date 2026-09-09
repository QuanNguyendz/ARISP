using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Dashboard.Queries.GetHrDashboard;
using ARI.Application.Interfaces;
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
        private readonly ICurrentUserService _currentUserService;

        public DashboardController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// GET /api/dashboard/hr
        /// Tổng quan tuyển dụng cho HR: KPI, phễu tuyển dụng, ứng viên gần đây.
        /// </summary>
        [HttpGet("hr")]
        public async Task<IActionResult> GetHrOverview(CancellationToken ct)
        {
            var result = await _sender.Send(
                new GetHrDashboardQuery(_currentUserService.UserId, _currentUserService.Role), ct);
            return Ok(result.Value);
        }
    }
}
