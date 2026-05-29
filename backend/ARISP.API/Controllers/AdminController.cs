using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ARISP.Infrastructure.Data;
using ARISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class AdminController : ControllerBase
    {
        private readonly ARISPDbContext _dbContext;

        public AdminController(ARISPDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("users/pending")]
        public async Task<IActionResult> GetPendingUsers()
        {
            var users = await _dbContext.Users
                .IgnoreQueryFilters()
                .Where(u => !u.IsActive)
                .Select(u => new {
                    u.Id,
                    u.Email,
                    u.Role,
                    u.FullName,
                    u.OrganizationId,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("users/{id}/approve")]
        public async Task<IActionResult> ApproveUser(Guid id)
        {
            var user = await _dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound(new { message = "User not found." });

            if (user.IsActive)
                return BadRequest(new { message = "User already active." });

            user.IsActive = true;
            _dbContext.Users.Update(user);

            // create audit log
            Guid? actorId = null;
            var actorClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            if (actorClaim != null && Guid.TryParse(actorClaim.Value, out var parsed))
                actorId = parsed;

            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                ActorUserId = actorId,
                Action = "user_approved",
                EntityType = "User",
                EntityId = user.Id,
                Metadata = $"{{\"email\":\"{user.Email}\",\"role\":\"{user.Role}\"}}",
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _dbContext.AuditLogs.AddAsync(audit);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "User approved successfully." });
        }
    }
}
