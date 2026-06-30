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
        private readonly INotificationService _notificationService;

        public AdminController(IUnitOfWork unitOfWork, IEmailService emailService, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _notificationService = notificationService;
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
            await SendStaffWelcomeEmailAsync(newUser, tempPassword);

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
                    u.LockReason,
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

        // ============================================================
        // USER ACTIVATE / DEACTIVATE / REJECT
        // ============================================================

        [HttpPost("users/{id}/deactivate")]
        public async Task<IActionResult> DeactivateUser(Guid id, [FromBody] DeactivateUserRequest? request = null)
        {
            if (GetActorId() == id)
                return BadRequest(new { message = "Bạn không thể khóa chính tài khoản của mình." });

            var reason = request?.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest(new { message = "Vui lòng nhập lý do khóa tài khoản." });

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found." });

            if (!user.IsActive)
                return BadRequest(new { message = "Tài khoản đã bị khóa." });

            user.IsActive = false;
            user.LockReason = reason;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);
            await WriteAuditAsync("user_deactivated", "User", user.Id,
                $"{{\"email\":\"{user.Email}\",\"reason\":{System.Text.Json.JsonSerializer.Serialize(reason)}}}");
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "Đã khóa tài khoản." });
        }

        [HttpPost("users/{id}/activate")]
        public async Task<IActionResult> ActivateUser(Guid id)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found." });

            if (user.IsActive)
                return BadRequest(new { message = "Tài khoản đã đang hoạt động." });

            user.IsActive = true;
            user.LockReason = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);
            await WriteAuditAsync("user_activated", "User", user.Id, $"{{\"email\":\"{user.Email}\"}}");
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "Đã mở khóa tài khoản." });
        }

        /// <summary>Từ chối hoặc xóa tài khoản staff (soft delete). Dùng cho việc từ chối user chờ duyệt.</summary>
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            if (GetActorId() == id)
                return BadRequest(new { message = "Bạn không thể xóa chính tài khoản của mình." });

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found." });

            _unitOfWork.Repository<User>().Delete(user);
            await WriteAuditAsync("user_deleted", "User", user.Id, $"{{\"email\":\"{user.Email}\",\"role\":\"{user.Role}\"}}");
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "Đã xóa tài khoản." });
        }

        // ============================================================
        // DASHBOARD STATS
        // ============================================================

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(CancellationToken ct)
        {
            // Chỉ kéo cột cần cho thống kê (Role, IsActive) thay vì cả entity User;
            // ứng viên & yêu cầu chờ duyệt đếm bằng SQL COUNT (không nạp rows).
            var userRoles = await _unitOfWork.Repository<User>()
                .QueryAsync(q => q.Select(u => new { u.Role, u.IsActive }), ct);
            var candidateCount = await _unitOfWork.Repository<CandidateAccount>().CountAsync(_ => true, ct);
            var pendingCount = await _unitOfWork.Repository<AccountRequest>().CountAsync(r => r.Status == "pending", ct);

            string Role(string? r) => (r ?? string.Empty).Trim().ToLowerInvariant();

            return Ok(new
            {
                TotalUsers = userRoles.Count,
                ActiveUsers = userRoles.Count(u => u.IsActive),
                LockedUsers = userRoles.Count(u => !u.IsActive),
                PendingRequests = pendingCount,
                SuperAdmins = userRoles.Count(u => Role(u.Role) == "super_admin"),
                HrAdmins = userRoles.Count(u => Role(u.Role) == "hr_admin"),
                Recruiters = userRoles.Count(u => Role(u.Role) == "recruiter"),
                Candidates = candidateCount
            });
        }

        // ============================================================
        // AUDIT LOGS
        // ============================================================

        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? action = null,
            [FromQuery] string? entityType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var normalizedAction = !string.IsNullOrWhiteSpace(action) ? action.Trim().ToLower() : null;
            var normalizedEntity = !string.IsNullOrWhiteSpace(entityType) ? entityType.Trim().ToLower() : null;

            var logs = (await _unitOfWork.Repository<AuditLog>().FindAsync(l =>
                (normalizedAction == null || l.Action.ToLower() == normalizedAction) &&
                (normalizedEntity == null || (l.EntityType != null && l.EntityType.ToLower() == normalizedEntity)), ct)).ToList();

            var totalCount = logs.Count;

            // Resolve actor display names
            var actorIds = logs.Where(l => l.ActorUserId.HasValue).Select(l => l.ActorUserId!.Value).Distinct().ToList();
            var nameById = new Dictionary<Guid, string>();
            if (actorIds.Count > 0)
            {
                var actors = await _unitOfWork.Repository<User>().FindAsync(u => actorIds.Contains(u.Id), ct);
                nameById = actors.ToDictionary(u => u.Id, u => u.FullName ?? u.Email);
            }

            var items = logs
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id,
                    l.Action,
                    l.EntityType,
                    l.EntityId,
                    l.Metadata,
                    ActorName = l.ActorUserId.HasValue && nameById.TryGetValue(l.ActorUserId.Value, out var n) ? n : "Hệ thống",
                    l.CreatedAt
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

        // ============================================================
        // SYSTEM SETTINGS (key/value: allowed_email_domains, webhooks...)
        // ============================================================

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings(CancellationToken ct)
        {
            var settings = (await _unitOfWork.Repository<SystemSetting>().GetAllAsync(ct)).ToList();
            return Ok(settings
                .OrderBy(s => s.Key)
                .Select(s => new { s.Key, s.Value, s.Description, s.UpdatedAt }));
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] List<UpdateSettingItem> items, CancellationToken ct)
        {
            if (items == null || items.Count == 0)
                return BadRequest(new { message = "Danh sách cài đặt trống." });

            var existing = (await _unitOfWork.Repository<SystemSetting>().GetAllAsync(ct)).ToList();

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Key)) continue;
                var key = item.Key.Trim();
                var current = existing.FirstOrDefault(s => s.Key == key);
                if (current != null)
                {
                    current.Value = item.Value ?? string.Empty;
                    if (item.Description != null) current.Description = item.Description;
                    current.UpdatedAt = DateTimeOffset.UtcNow;
                    _unitOfWork.Repository<SystemSetting>().Update(current);
                }
                else
                {
                    await _unitOfWork.Repository<SystemSetting>().AddAsync(new SystemSetting
                    {
                        Id = Guid.NewGuid(),
                        Key = key,
                        Value = item.Value ?? string.Empty,
                        Description = item.Description,
                        UpdatedAt = DateTimeOffset.UtcNow
                    }, ct);
                }
            }

            await WriteAuditAsync("system_settings_updated", "SystemSetting", null,
                $"{{\"keys\":\"{string.Join(",", items.Where(i => !string.IsNullOrWhiteSpace(i.Key)).Select(i => i.Key.Trim()))}\"}}");
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "Đã lưu cài đặt hệ thống." });
        }

        // ============================================================
        // ACCOUNT CREATION REQUESTS (Super Admin duyệt yêu cầu của HR)
        // ============================================================

        [HttpGet("account-requests")]
        public async Task<IActionResult> GetAccountRequests([FromQuery] string status = "pending", CancellationToken ct = default)
        {
            var normalized = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLower();

            var requests = normalized == "all"
                ? (await _unitOfWork.Repository<AccountRequest>().GetAllAsync(ct)).ToList()
                : (await _unitOfWork.Repository<AccountRequest>().FindAsync(r => r.Status == normalized, ct)).ToList();

            // Resolve tên người gửi yêu cầu (HR Leader)
            var requesterIds = requests.Select(r => r.RequestedByUserId).Distinct().ToList();
            var nameById = new Dictionary<Guid, string>();
            if (requesterIds.Count > 0)
            {
                var requesters = await _unitOfWork.Repository<User>().FindAsync(u => requesterIds.Contains(u.Id), ct);
                nameById = requesters.ToDictionary(u => u.Id, u => u.FullName ?? u.Email);
            }

            var items = requests
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.BatchId,
                    r.Email,
                    r.FullName,
                    r.Role,
                    r.Department,
                    r.Status,
                    r.ReviewReason,
                    RequestedBy = nameById.TryGetValue(r.RequestedByUserId, out var n) ? n : "—",
                    r.CreatedAt,
                    r.ReviewedAt
                })
                .ToList();

            return Ok(items);
        }

        [HttpPost("account-requests/{id}/approve")]
        public async Task<IActionResult> ApproveAccountRequest(Guid id)
        {
            var req = await _unitOfWork.Repository<AccountRequest>().GetByIdAsync(id);
            if (req == null)
                return NotFound(new { message = "Không tìm thấy yêu cầu." });

            if (req.Status != "pending")
                return BadRequest(new { message = "Yêu cầu này đã được xử lý." });

            var email = req.Email.Trim().ToLower();
            var existing = (await _unitOfWork.Repository<User>().FindAsync(u => u.Email == email)).FirstOrDefault();
            if (existing != null)
                return Conflict(new { message = "Email này đã có tài khoản. Hãy từ chối yêu cầu." });

            // Tạo tài khoản staff active
            var tempPw = GenerateTemporaryPassword();
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPw),
                Role = req.Role,
                FullName = req.FullName.Trim(),
                Department = req.Department?.Trim(),
                IsActive = true
            };
            await _unitOfWork.Repository<User>().AddAsync(newUser);

            req.Status = "approved";
            req.ReviewedByUserId = GetActorId();
            req.ReviewedAt = DateTimeOffset.UtcNow;
            req.CreatedUserId = newUser.Id;
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<AccountRequest>().Update(req);

            await WriteAuditAsync("account_request_approved", "AccountRequest", req.Id,
                $"{{\"email\":\"{newUser.Email}\",\"role\":\"{newUser.Role}\"}}");
            await _unitOfWork.SaveChangesAsync();

            await SendStaffWelcomeEmailAsync(newUser, tempPw);

            // Notify HR Leader (requester)
            await _notificationService.PublishUserEventAsync(req.RequestedByUserId, "ReceiveAccountRequestUpdate", new { RequestId = req.Id, Status = "approved", Email = req.Email });

            return Ok(new { message = "Đã duyệt yêu cầu và tạo tài khoản." });
        }

        [HttpPost("account-requests/{id}/reject")]
        public async Task<IActionResult> RejectAccountRequest(Guid id, [FromBody] RejectRequest? request = null)
        {
            var reason = request?.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối." });

            var req = await _unitOfWork.Repository<AccountRequest>().GetByIdAsync(id);
            if (req == null)
                return NotFound(new { message = "Không tìm thấy yêu cầu." });

            if (req.Status != "pending")
                return BadRequest(new { message = "Yêu cầu này đã được xử lý." });

            req.Status = "rejected";
            req.ReviewReason = reason;
            req.ReviewedByUserId = GetActorId();
            req.ReviewedAt = DateTimeOffset.UtcNow;
            req.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<AccountRequest>().Update(req);

            await WriteAuditAsync("account_request_rejected", "AccountRequest", req.Id,
                $"{{\"email\":\"{req.Email}\",\"reason\":{System.Text.Json.JsonSerializer.Serialize(reason)}}}");
            await _unitOfWork.SaveChangesAsync();

            // Notify HR Leader (requester)
            await _notificationService.PublishUserEventAsync(req.RequestedByUserId, "ReceiveAccountRequestUpdate", new { RequestId = req.Id, Status = "rejected", Email = req.Email, Reason = reason });

            return Ok(new { message = "Đã từ chối yêu cầu." });
        }

        // ============================================================
        // HELPERS
        // ============================================================

        /// <summary>Gửi email chào mừng kèm thông tin đăng nhập cho staff mới (best-effort, không fail request).</summary>
        private async Task SendStaffWelcomeEmailAsync(User newUser, string pw)
        {
            var roleName = (newUser.Role ?? string.Empty).ToLower() == "hr_admin" ? "HR Admin" : "Recruiter";
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
                                <td style='padding: 8px 0; font-weight: 600; font-family: monospace; font-size: 15px; letter-spacing: 1px;'>{pw}</td>
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
                // Best-effort: tài khoản đã tạo thành công, lỗi gửi email không làm fail request.
            }
        }

        private Guid? GetActorId()
        {
            var actorClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            if (actorClaim != null && Guid.TryParse(actorClaim.Value, out var parsed))
                return parsed;
            return null;
        }

        private async Task WriteAuditAsync(string action, string entityType, Guid? entityId, string metadata)
        {
            var audit = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = GetActorId(),
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Metadata = metadata,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<AuditLog>().AddAsync(audit);
        }
    }

    public class UpdateSettingItem
    {
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Description { get; set; }
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

    public class DeactivateUserRequest
    {
        public string? Reason { get; set; }
    }

    public class RejectRequest
    {
        public string? Reason { get; set; }
    }
}
