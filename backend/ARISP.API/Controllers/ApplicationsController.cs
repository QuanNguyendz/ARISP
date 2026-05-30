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
        public async Task<IActionResult> SubmitApplication([FromBody] SubmitApplicationRequest request)
        {
            var result = await _applicationService.SubmitApplicationAsync(request, "job_board");
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
