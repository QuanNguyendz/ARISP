using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    [ApiController]
    [Route("api/staff/profile")]
    [Authorize]
    public class StaffProfileController : ControllerBase
    {
        private readonly ISender _sender;

        public StaffProfileController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var userId = GetActorId();
            if (userId == null) return Unauthorized();

            var result = await _sender.Send(new GetStaffSettingsQuery(userId.Value));
            if (result.IsFailure) return NotFound(new { message = result.Error });

            return Ok(result.Value);
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] StaffSettingsDto settings)
        {
            var userId = GetActorId();
            if (userId == null) return Unauthorized();

            var result = await _sender.Send(new UpdateStaffSettingsCommand(userId.Value, settings));
            if (result.IsFailure) return NotFound(new { message = result.Error });

            return Ok(result.Value);
        }

        private Guid? GetActorId()
        {
            var actorClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            if (actorClaim != null && Guid.TryParse(actorClaim.Value, out var parsed))
                return parsed;
            return null;
        }
    }
}
