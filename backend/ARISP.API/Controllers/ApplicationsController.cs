using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ARISP.Application.DTOs;
using ARISP.Application.Services;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly ApplicationService _applicationService;

        public ApplicationsController(ApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitApplication([FromHeader(Name = "X-Organization-Id")] string orgIdStr, [FromBody] SubmitApplicationRequest request)
        {
            if (!Guid.TryParse(orgIdStr, out var orgId))
            {
                // If orgId is not provided in header, let's use a default organization
                orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            }

            var result = await _applicationService.SubmitApplicationAsync(orgId, request, "job_board");
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Value);
        }

        [HttpGet("{id}/practice-eligibility")]
        public async Task<IActionResult> GetPracticeEligibility(Guid id)
        {
            var result = await _applicationService.CheckPracticeEligibilityAsync(id);
            if (result.IsFailure)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { eligible = result.Value });
        }
    }
}
