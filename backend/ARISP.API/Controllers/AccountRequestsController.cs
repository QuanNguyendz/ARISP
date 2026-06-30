using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.API.Controllers
{
    /// <summary>
    /// HR Leader gửi yêu cầu tạo tài khoản staff (lẻ hoặc hàng loạt) lên Super Admin phê duyệt.
    /// </summary>
    [ApiController]
    [Route("api/hr/account-requests")]
    [Authorize(Policy = "HrManagement")]
    public class AccountRequestsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public AccountRequestsController(IUnitOfWork unitOfWork, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        /// <summary>Danh sách yêu cầu do chính HR Leader hiện tại đã gửi (theo dõi trạng thái).</summary>
        [HttpGet]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var actorId = GetActorId();
            if (actorId == null) return Unauthorized();

            var requests = await _unitOfWork.Repository<AccountRequest>()
                .FindAsync(r => r.RequestedByUserId == actorId.Value, ct);

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
                    r.CreatedAt,
                    r.ReviewedAt
                })
                .ToList();

            return Ok(items);
        }

        /// <summary>Tạo một hoặc nhiều yêu cầu tạo tài khoản. Nhiều mục → cùng một BatchId.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] List<AccountRequestItem> items, CancellationToken ct)
        {
            if (items == null || items.Count == 0)
                return BadRequest(new { message = "Danh sách yêu cầu trống." });

            var actorId = GetActorId();
            if (actorId == null) return Unauthorized();

            // Chuẩn hóa + validate
            var cleaned = new List<AccountRequestItem>();
            foreach (var item in items)
            {
                var email = item.Email?.Trim().ToLower() ?? string.Empty;
                var fullName = item.FullName?.Trim() ?? string.Empty;
                var role = item.Role?.Trim().ToLower() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                    return BadRequest(new { message = $"Email không hợp lệ: '{item.Email}'." });
                if (string.IsNullOrWhiteSpace(fullName))
                    return BadRequest(new { message = $"Thiếu họ tên cho '{email}'." });
                if (role != "hr_admin" && role != "recruiter")
                    return BadRequest(new { message = $"Vai trò phải là 'hr_admin' hoặc 'recruiter' (email {email})." });

                cleaned.Add(new AccountRequestItem { Email = email, FullName = fullName, Role = role, Department = item.Department?.Trim() });
            }

            // Chặn trùng email trong cùng request
            var dup = cleaned.GroupBy(c => c.Email).FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
                return BadRequest(new { message = $"Email bị lặp trong yêu cầu: {dup.Key}." });

            // Chặn email đã có tài khoản hoặc đã có yêu cầu đang chờ
            var emails = cleaned.Select(c => c.Email).ToList();
            var existingUsers = await _unitOfWork.Repository<User>().FindAsync(u => emails.Contains(u.Email), ct);
            var takenByUser = existingUsers.Select(u => u.Email).ToHashSet();
            var pendingReqs = await _unitOfWork.Repository<AccountRequest>()
                .FindAsync(r => r.Status == "pending" && emails.Contains(r.Email), ct);
            var takenByPending = pendingReqs.Select(r => r.Email).ToHashSet();

            var conflicts = cleaned
                .Where(c => takenByUser.Contains(c.Email) || takenByPending.Contains(c.Email))
                .Select(c => c.Email)
                .ToList();
            if (conflicts.Count > 0)
                return Conflict(new { message = $"Các email sau đã có tài khoản hoặc đang chờ duyệt: {string.Join(", ", conflicts)}." });

            Guid? batchId = cleaned.Count > 1 ? Guid.NewGuid() : null;
            var now = DateTimeOffset.UtcNow;

            foreach (var c in cleaned)
            {
                await _unitOfWork.Repository<AccountRequest>().AddAsync(new AccountRequest
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    RequestedByUserId = actorId.Value,
                    Email = c.Email,
                    FullName = c.FullName,
                    Role = c.Role,
                    Department = c.Department,
                    Status = "pending",
                    CreatedAt = now,
                    UpdatedAt = now
                }, ct);
            }

            // Audit
            await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorId,
                Action = "account_request_created",
                EntityType = "AccountRequest",
                EntityId = null,
                Metadata = $"{{\"count\":{cleaned.Count},\"batch\":\"{batchId}\"}}",
                CreatedAt = now
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            
            // Notify Super Admin
            await _notificationService.PublishGroupEventAsync("super_admin", "ReceiveAccountRequest", new { BatchId = batchId, Count = cleaned.Count }, ct);

            // Real-time Notification for super admin
            await _notificationService.PublishGroupEventAsync("super_admin", "ReceiveNewAccountRequest", new { Count = cleaned.Count, BatchId = batchId }, ct);

            return Ok(new { message = $"Đã gửi {cleaned.Count} yêu cầu tạo tài khoản chờ Super Admin duyệt.", count = cleaned.Count, batchId });
        }

        private Guid? GetActorId()
        {
            var actorClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            if (actorClaim != null && Guid.TryParse(actorClaim.Value, out var parsed))
                return parsed;
            return null;
        }
    }

    public class AccountRequestItem
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "recruiter";
        public string? Department { get; set; }
    }
}
