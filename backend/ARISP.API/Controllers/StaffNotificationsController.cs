using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using ARISP.Domain.Constants;
using ARISP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARISP.API.Controllers
{
    /// <summary>
    /// Thông báo cho nhân sự nội bộ (HR Admin / Recruiter / Super Admin).
    /// Tách riêng khỏi cổng ứng viên (<c>CandidatePortalController</c>): cùng bảng <c>notifications</c>
    /// nhưng người nhận là <c>User</c> (cột <c>recipient_user_id</c>), policy và logic sync khác hẳn.
    /// </summary>
    [ApiController]
    [Route("api/staff/notifications")]
    [Authorize(Policy = "InternalStaff")]
    public class StaffNotificationsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public StaffNotificationsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>GET /api/staff/notifications — sync từ sự kiện thực rồi trả danh sách + số chưa đọc.</summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            await SyncNotificationsAsync(userId, ct);

            var items = (await _unitOfWork.Repository<Notification>()
                    .FindAsync(n => n.RecipientUserId == userId, ct))
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new { n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt })
                .ToList();

            return Ok(new { items, unreadCount = items.Count(i => !i.IsRead) });
        }

        /// <summary>POST /api/staff/notifications/read-all — đánh dấu tất cả đã đọc.</summary>
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var list = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.RecipientUserId == userId && !n.IsRead, ct)).ToList();
            foreach (var n in list)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Notification>().Update(n);
            }
            if (list.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Ok(new { updated = list.Count });
        }

        /// <summary>POST /api/staff/notifications/{id}/read — đánh dấu một thông báo đã đọc.</summary>
        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(id, ct);
            if (n == null || n.RecipientUserId != userId)
                return NotFound(new { message = "Không tìm thấy thông báo." });
            if (!n.IsRead)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<Notification>().Update(n);
                await _unitOfWork.SaveChangesAsync();
            }
            return Ok(new { read = true });
        }

        /// <summary>DELETE /api/staff/notifications/{id} — xóa (soft delete) một thông báo.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(id, ct);
            if (n == null || n.RecipientUserId != userId)
                return NotFound(new { message = "Không tìm thấy thông báo." });

            _unitOfWork.Repository<Notification>().Delete(n);
            await _unitOfWork.SaveChangesAsync();
            return Ok(new { deleted = true });
        }

        /// <summary>DELETE /api/staff/notifications — xóa (soft delete) toàn bộ thông báo của người dùng.</summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteAllNotifications(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var list = (await _unitOfWork.Repository<Notification>()
                .FindAsync(n => n.RecipientUserId == userId, ct)).ToList();
            foreach (var n in list)
                _unitOfWork.Repository<Notification>().Delete(n);
            if (list.Count > 0) await _unitOfWork.SaveChangesAsync();
            return Ok(new { deleted = list.Count });
        }

        /// <summary>
        /// Sinh thông báo từ sự kiện thực của nhân sự, idempotent theo DedupKey (giữ trạng thái đã đọc).
        /// Phạm vi theo vai trò: Recruiter chỉ thấy tin mình tạo; HR Admin thấy toàn bộ tin.
        /// </summary>
        private async Task SyncNotificationsAsync(Guid userId, CancellationToken ct)
        {
            var isRecruiter = User.IsInRole(AppRoles.Recruiter);
            var isHrAdmin = User.IsInRole(AppRoles.HrAdmin);
            // Super Admin không gắn với phễu tuyển dụng theo tin → không sinh thông báo recruitment.
            if (!isRecruiter && !isHrAdmin) return;

            // Tin trong phạm vi của người dùng (projection nhẹ).
            var jobs = await _unitOfWork.Repository<JobPosting>()
                .QueryAsync(q => (isHrAdmin
                        ? q
                        : q.Where(j => j.CreatedByUserId == userId))
                    .Select(j => new { j.Id, j.Title, j.Status }), ct);
            if (jobs.Count == 0) return;

            var jobIds = jobs.Select(j => j.Id).ToList();
            var jobTitle = jobs.ToDictionary(j => j.Id, j => j.Title);
            string Title(Guid jid) => jobTitle.TryGetValue(jid, out var t) ? t : "Vị trí tuyển dụng";

            var linkBase = isRecruiter && !isHrAdmin ? "/recruiter" : "/hr";
            var nowUtc = DateTimeOffset.UtcNow;
            var since = nowUtc.AddDays(-30);

            // Tính cả bản đã soft-delete (IgnoreQueryFilters) để thông báo người dùng đã xóa
            // KHÔNG bị sync tạo lại ở lần mở sau.
            var existing = (await _unitOfWork.Repository<Notification>()
                    .QueryAsync(q => q.IgnoreQueryFilters()
                        .Where(n => n.RecipientUserId == userId)
                        .Select(n => n.DedupKey), ct))
                .ToHashSet();
            var toAdd = new List<Notification>();

            void Add(string key, string type, string title, string? body, string? link, DateTimeOffset createdAt)
            {
                if (existing.Contains(key)) return;
                existing.Add(key);
                toAdd.Add(new Notification
                {
                    RecipientUserId = userId,
                    DedupKey = key,
                    Type = type,
                    Title = title,
                    Body = body,
                    Link = link,
                    CreatedAt = createdAt,
                    UpdatedAt = nowUtc,
                });
            }

            // 1. Ứng viên mới ứng tuyển (30 ngày gần nhất) vào tin trong phạm vi.
            var apps = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
                .QueryAsync(q => q
                    .Where(a => jobIds.Contains(a.JobPostingId) && a.CreatedAt >= since)
                    .Select(a => new { a.Id, a.JobPostingId, a.CandidateName, a.CreatedAt }), ct);
            foreach (var a in apps)
                Add($"applied:{a.Id}", "applied", "Ứng viên mới ứng tuyển",
                    $"{a.CandidateName} · {Title(a.JobPostingId)}", $"{linkBase}/candidates", a.CreatedAt);

            // 2. Đánh giá AI chờ HR xác nhận (chưa có HrReview) cho tin trong phạm vi.
            var appIds = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>()
                .QueryAsync(q => q.Where(a => jobIds.Contains(a.JobPostingId)).Select(a => a.Id), ct);
            if (appIds.Count > 0)
            {
                var evals = await _unitOfWork.Repository<Evaluation>()
                    .QueryAsync(q => q.Where(e => appIds.Contains(e.ApplicationId))
                        .Select(e => new { e.Id, e.ApplicationId, e.RoundNumber, e.CreatedAt }), ct);
                var evalIds = evals.Select(e => e.Id).ToList();
                var reviewed = evalIds.Count > 0
                    ? (await _unitOfWork.Repository<HrReview>()
                        .FindAsync(r => evalIds.Contains(r.EvaluationId), ct)).Select(r => r.EvaluationId).ToHashSet()
                    : new HashSet<Guid>();
                var appToJob = apps.ToDictionary(a => a.Id, a => a.JobPostingId);
                foreach (var ev in evals.Where(e => !reviewed.Contains(e.Id)))
                {
                    var jid = appToJob.TryGetValue(ev.ApplicationId, out var j) ? j : Guid.Empty;
                    Add($"review:{ev.Id}", "pending", $"Đánh giá vòng {ev.RoundNumber} chờ xác nhận",
                        jid != Guid.Empty ? Title(jid) : null, $"{linkBase}/evaluations", ev.CreatedAt);
                }
            }

            // 3. Tin chờ duyệt — chỉ HR Admin.
            if (isHrAdmin)
            {
                foreach (var j in jobs.Where(j => j.Status == "pending"))
                    Add($"jobapproval:{j.Id}", "approval", "Tin tuyển dụng chờ duyệt",
                        Title(j.Id), "/hr/jobs/pending", nowUtc);
            }

            if (toAdd.Count > 0)
            {
                foreach (var n in toAdd) await _unitOfWork.Repository<Notification>().AddAsync(n, ct);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        private bool TryGetUserId(out Guid userId)
        {
            userId = Guid.Empty;
            var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            return claim != null && Guid.TryParse(claim.Value, out userId);
        }
    }
}
