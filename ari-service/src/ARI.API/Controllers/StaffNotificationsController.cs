using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.StaffNotifications.Commands.DeleteAllStaffNotifications;
using ARI.Application.StaffNotifications.Commands.DeleteStaffNotification;
using ARI.Application.StaffNotifications.Commands.MarkAllStaffNotificationsRead;
using ARI.Application.StaffNotifications.Commands.MarkStaffNotificationRead;
using ARI.Application.StaffNotifications.Queries.GetStaffNotifications;
using ARI.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
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
        private readonly ISender _sender;

        public StaffNotificationsController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>GET /api/staff/notifications — sync từ sự kiện thực rồi trả danh sách + số chưa đọc.</summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var result = await _sender.Send(new GetStaffNotificationsQuery(
                userId, User.IsInRole(AppRoles.Recruiter), User.IsInRole(AppRoles.HrAdmin)), ct);

            return Ok(result.Value);
        }

        /// <summary>POST /api/staff/notifications/read-all — đánh dấu tất cả đã đọc.</summary>
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var result = await _sender.Send(new MarkAllStaffNotificationsReadCommand(userId), ct);
            return Ok(new { updated = result.Value });
        }

        /// <summary>POST /api/staff/notifications/{id}/read — đánh dấu một thông báo đã đọc.</summary>
        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var result = await _sender.Send(new MarkStaffNotificationReadCommand(userId, id), ct);
            if (result.IsFailure)
                return NotFound(new { message = result.Error });
            return Ok(new { read = true });
        }

        /// <summary>DELETE /api/staff/notifications/{id} — xóa (soft delete) một thông báo.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var result = await _sender.Send(new DeleteStaffNotificationCommand(userId, id), ct);
            if (result.IsFailure)
                return NotFound(new { message = result.Error });
            return Ok(new { deleted = true });
        }

        /// <summary>DELETE /api/staff/notifications — xóa (soft delete) toàn bộ thông báo của người dùng.</summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteAllNotifications(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

            var result = await _sender.Send(new DeleteAllStaffNotificationsCommand(userId), ct);
            return Ok(new { deleted = result.Value });
        }

        private bool TryGetUserId(out Guid userId)
        {
            userId = Guid.Empty;
            var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            return claim != null && Guid.TryParse(claim.Value, out userId);
        }
    }
}
