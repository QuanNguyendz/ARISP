using System;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ARISP.Application.Interfaces;
using ARISP.Domain.Constants;
using ARISP.Domain.Entities;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class AdminController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;

        public AdminController(IUnitOfWork unitOfWork, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        [HttpGet("users/pending")]
        public async Task<IActionResult> GetPendingUsers()
        {
            var users = await _unitOfWork.Repository<User>().FindAsync(u => !u.IsActive);
            var usersResponse = users
                .Select(u => new {
                    u.Id,
                    u.Email,
                    u.Role,
                    u.FullName,
                    u.CreatedAt
                })
                .ToList();

            return Ok(usersResponse);
        }

        [HttpPost("users/{id}/approve")]
        public async Task<IActionResult> ApproveUser(Guid id)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found." });

            if (user.IsActive)
                return BadRequest(new { message = "User already active." });

            user.IsActive = true;
            _unitOfWork.Repository<User>().Update(user);

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

            await _unitOfWork.Repository<AuditLog>().AddAsync(audit);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "User approved successfully." });
        }

        // ============================================================
        // STAFF ACCOUNT PROVISIONING (Pre-provisioning)
        // ============================================================

        /// <summary>
        /// Super Admin tạo tài khoản cho HR Admin hoặc Recruiter.
        /// Hệ thống sinh mật khẩu tạm và gửi email thông báo cho staff mới.
        /// Sau khi nhận email, staff có thể đăng nhập bằng Email+Password tại /api/auth/staff/login
        /// hoặc đăng nhập qua OAuth2 (nếu email đã có trong DB).
        /// </summary>
        [HttpPost("users")]
        public async Task<IActionResult> CreateStaffUser([FromBody] CreateStaffUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email là bắt buộc." });

            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new { message = "Họ và tên là bắt buộc." });

            var normalizedRole = request.Role?.Trim().ToLower() ?? "";
            if (normalizedRole != "hr_admin" && normalizedRole != "recruiter")
                return BadRequest(new { message = "Role phải là 'hr_admin' hoặc 'recruiter'." });

            // Kiểm tra email đã tồn tại chưa
            var existingUsers = await _unitOfWork.Repository<User>().FindAsync(u => u.Email == request.Email.Trim().ToLower());
            var existingUser = existingUsers.FirstOrDefault();

            if (existingUser != null)
                return Conflict(new { message = "Email này đã được sử dụng bởi tài khoản khác." });

            // Sinh mật khẩu tạm (12 ký tự, bao gồm chữ hoa, thường, số, ký tự đặc biệt)
            var tempPassword = GenerateTemporaryPassword();

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email.Trim().ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
                Role = normalizedRole,
                FullName = request.FullName.Trim(),
                Department = request.Department?.Trim(),
                IsActive = true
            };

            await _unitOfWork.Repository<User>().AddAsync(newUser);

            // Audit log
            Guid? actorId = null;
            var actorClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            if (actorClaim != null && Guid.TryParse(actorClaim.Value, out var parsed))
                actorId = parsed;

            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorId,
                Action = "staff_account_created",
                EntityType = "User",
                EntityId = newUser.Id,
                Metadata = $"{{\"email\":\"{newUser.Email}\",\"role\":\"{newUser.Role}\"}}",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<AuditLog>().AddAsync(audit);
            await _unitOfWork.SaveChangesAsync();

            // Gửi email thông báo tài khoản cho staff mới
            var roleName = normalizedRole == "hr_admin" ? "HR Admin" : "Recruiter";
            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px; background-color: #f8fafc;'>
                    <div style='text-align: center; margin-bottom: 24px;'>
                        <h2 style='color: #1e293b; margin: 0;'>Chào mừng bạn đến với ARISP</h2>
                        <p style='color: #64748b; margin: 4px 0 0;'>AI-Powered Recruitment & Interview Support Platform</p>
                    </div>
                    <p>Xin chào <strong>{newUser.FullName}</strong>,</p>
                    <p>Tài khoản <strong>{roleName}</strong> của bạn đã được tạo thành công trên hệ thống ARISP. Dưới đây là thông tin đăng nhập:</p>
                    <div style='background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin: 20px 0;'>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr>
                                <td style='padding: 8px 0; color: #64748b; width: 120px;'>Email:</td>
                                <td style='padding: 8px 0; font-weight: 600;'>{newUser.Email}</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px 0; color: #64748b;'>Mật khẩu:</td>
                                <td style='padding: 8px 0; font-weight: 600; font-family: monospace; font-size: 15px; letter-spacing: 1px;'>{tempPassword}</td>
                            </tr>
                            <tr>
                                <td style='padding: 8px 0; color: #64748b;'>Vai trò:</td>
                                <td style='padding: 8px 0; font-weight: 600;'>{roleName}</td>
                            </tr>
                        </table>
                    </div>
                    <div style='background-color: #fef3c7; border: 1px solid #f59e0b; border-radius: 6px; padding: 12px; margin: 16px 0;'>
                        <p style='margin: 0; color: #92400e; font-size: 14px;'>⚠️ Vui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên để đảm bảo an toàn tài khoản.</p>
                    </div>
                    <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;'/>
                    <p style='font-size: 12px; color: #94a3b8; text-align: center;'>Email này được gửi tự động từ hệ thống ARISP. Vui lòng không trả lời.</p>
                </div>";

            try
            {
                await _emailService.SendEmailAsync(newUser.Email, $"[ARISP] Tài khoản {roleName} đã được tạo", emailBody);
            }
            catch
            {
                // Ghi log lỗi nhưng không fail toàn bộ request vì tài khoản đã tạo thành công
            }

            return Ok(new
            {
                message = "Tài khoản staff đã được tạo thành công. Email thông báo đã được gửi.",
                user = new
                {
                    newUser.Id,
                    newUser.Email,
                    newUser.FullName,
                    newUser.Role,
                    newUser.Department,
                    newUser.IsActive,
                    newUser.CreatedAt
                }
            });
        }

        /// <summary>
        /// Sinh mật khẩu tạm thời 12 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt.
        /// </summary>
        private static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghjkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string special = "@#$%&!";
            const string all = upper + lower + digits + special;

            var password = new char[12];
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[12];
            rng.GetBytes(bytes);

            // Đảm bảo ít nhất 1 ký tự mỗi loại
            password[0] = upper[bytes[0] % upper.Length];
            password[1] = lower[bytes[1] % lower.Length];
            password[2] = digits[bytes[2] % digits.Length];
            password[3] = special[bytes[3] % special.Length];

            // Phần còn lại lấy ngẫu nhiên từ tất cả
            for (int i = 4; i < 12; i++)
                password[i] = all[bytes[i] % all.Length];

            // Xáo trộn vị trí
            var result = password.OrderBy(_ => RandomNumberGenerator.GetInt32(1000)).ToArray();
            return new string(result);
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

            var cleanSearch = !string.IsNullOrWhiteSpace(search) ? search.Trim().ToLower() : null;
            var normalizedRole = !string.IsNullOrWhiteSpace(role) ? role.Trim().ToLower() : null;

            var users = await _unitOfWork.Repository<User>().FindAsync(u =>
                (cleanSearch == null || (u.FullName != null && u.FullName.ToLower().Contains(cleanSearch)) || (u.Email != null && u.Email.ToLower().Contains(cleanSearch))) &&
                (normalizedRole == null || (u.Role != null && u.Role.ToLower() == normalizedRole)) &&
                (!isActive.HasValue || u.IsActive == isActive.Value)
            );

            var filteredUsers = users.ToList();
            var totalCount = filteredUsers.Count;

            var items = filteredUsers
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
                .ToList();

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

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            user.Role = normalizedRole;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<User>().Update(user);

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

            await _unitOfWork.Repository<AuditLog>().AddAsync(audit);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "User role updated successfully." });
        }
    }

    public class UpdateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public class CreateStaffUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "recruiter"; // hr_admin | recruiter
        public string? Department { get; set; }
    }
}
