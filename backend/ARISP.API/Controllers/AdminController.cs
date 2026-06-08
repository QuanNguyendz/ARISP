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

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] string? search = null,
            [FromQuery] string? role = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _dbContext.Users.IgnoreQueryFilters().AsQueryable();

            // 1. Search term filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var cleanSearch = search.Trim().ToLower();
                query = query.Where(u => 
                    (u.FullName != null && u.FullName.ToLower().Contains(cleanSearch)) || 
                    (u.Email != null && u.Email.ToLower().Contains(cleanSearch))
                );
            }

            // 2. Role filter
            if (!string.IsNullOrWhiteSpace(role))
            {
                var normalizedRole = role.Trim().ToLower();
                query = query.Where(u => u.Role != null && u.Role.ToLower() == normalizedRole);
            }

            // 3. Status filter
            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                Items = items
            });
        }

        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateRoleRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest(new { message = "Role is required." });
            }

            var normalizedRole = request.Role.Trim().ToLower();
            if (normalizedRole != "hr_admin" && normalizedRole != "recruiter")
            {
                return BadRequest(new { message = "Invalid role. Role must be 'hr_admin' or 'recruiter'." });
            }

            // Get current actor user from claims
            var actorClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            if (actorClaim != null && Guid.TryParse(actorClaim.Value, out var actorId))
            {
                if (actorId == id)
                {
                    return BadRequest(new { message = "You cannot change your own role." });
                }
            }

            var user = await _dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            user.Role = normalizedRole;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            _dbContext.Users.Update(user);

            // create audit log
            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorClaim != null && Guid.TryParse(actorClaim.Value, out var parsedActorId) ? parsedActorId : null,
                Action = "user_role_updated",
                EntityType = "User",
                EntityId = user.Id,
                Metadata = $"{{\"email\":\"{user.Email}\",\"new_role\":\"{user.Role}\"}}",
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _dbContext.AuditLogs.AddAsync(audit);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "User role updated successfully." });
        }
    }

    public class UpdateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }
}
