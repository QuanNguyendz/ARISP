using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin;
using ARI.Application.Admin.Commands.ActivateUser;
using ARI.Application.Admin.Commands.ApproveAccountRequest;
using ARI.Application.Admin.Commands.ApproveUser;
using ARI.Application.Admin.Commands.CreateStaffUser;
using ARI.Application.Admin.Commands.DeactivateUser;
using ARI.Application.Admin.Commands.DeleteUser;
using ARI.Application.Admin.Commands.RejectAccountRequest;
using ARI.Application.Admin.Commands.UpdateSystemSettings;
using ARI.Application.Admin.Commands.UpdateUserDepartment;
using ARI.Application.Admin.Commands.UpdateUserRole;
using ARI.Application.Admin.Queries.GetAccountRequests;
using ARI.Application.Admin.Queries.GetAdminStats;
using ARI.Application.Admin.Queries.GetAuditLogs;
using ARI.Application.Admin.Queries.GetPendingUsers;
using ARI.Application.Admin.Queries.GetSystemSettings;
using ARI.Application.Admin.Queries.GetUsers;
using ARI.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class AdminController : ControllerBase
    {
        private readonly ISender _sender;

        public AdminController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("users/pending")]
        public async Task<IActionResult> GetPendingUsers()
        {
            var result = await _sender.Send(new GetPendingUsersQuery());
            return Ok(result.Value);
        }

        [HttpPost("users/{id}/approve")]
        public async Task<IActionResult> ApproveUser(Guid id)
        {
            var result = await _sender.Send(new ApproveUserCommand(id, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }
            return Ok(new { message = "User approved successfully." });
        }

        /// <summary>
        /// Super Admin tạo tài khoản cho HR Admin hoặc Recruiter.
        /// Hệ thống sinh mật khẩu tạm và gửi email thông báo cho staff mới.
        /// </summary>
        [HttpPost("users")]
        public async Task<IActionResult> CreateStaffUser([FromBody] CreateStaffUserRequest request)
        {
            var result = await _sender.Send(new CreateStaffUserCommand(
                request.Email, request.FullName, request.Role, request.DepartmentId, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.Conflict
                    ? Conflict(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }

            return Ok(new
            {
                message = "Tài khoản staff đã được tạo thành công. Email thông báo đã được gửi.",
                user = result.Value
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] string? search = null,
            [FromQuery] string? role = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _sender.Send(new GetUsersQuery(search, role, isActive, page, pageSize));
            return Ok(result.Value);
        }

        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateRoleRequest request)
        {
            var result = await _sender.Send(new UpdateUserRoleCommand(id, request?.Role, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }
            return Ok(new { message = "User role updated successfully." });
        }

        /// <summary>
        /// Gán/đổi đội của một tài khoản (ADR-065). Đây là đường DUY NHẤT đổi được đội — nhân viên
        /// không còn tự sửa được ở trang Cài đặt cá nhân.
        /// </summary>
        [HttpPut("users/{id}/department")]
        public async Task<IActionResult> UpdateUserDepartment(Guid id, [FromBody] UpdateDepartmentRequest? request)
        {
            var result = await _sender.Send(new UpdateUserDepartmentCommand(id, request?.DepartmentId, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }
            return Ok(new { message = "User department updated successfully." });
        }

        [HttpPost("users/{id}/deactivate")]
        public async Task<IActionResult> DeactivateUser(Guid id, [FromBody] DeactivateUserRequest? request = null)
        {
            var result = await _sender.Send(new DeactivateUserCommand(id, request?.Reason, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }
            return Ok(new { message = "Đã khóa tài khoản." });
        }

        [HttpPost("users/{id}/activate")]
        public async Task<IActionResult> ActivateUser(Guid id)
        {
            var result = await _sender.Send(new ActivateUserCommand(id, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }
            return Ok(new { message = "Đã mở khóa tài khoản." });
        }

        /// <summary>Từ chối hoặc xóa tài khoản staff (soft delete). Dùng cho việc từ chối user chờ duyệt.</summary>
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var result = await _sender.Send(new DeleteUserCommand(id, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }
            return Ok(new { message = "Đã xóa tài khoản." });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(CancellationToken ct)
        {
            var result = await _sender.Send(new GetAdminStatsQuery(), ct);
            return Ok(result.Value);
        }

        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? action = null,
            [FromQuery] string? entityType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetAuditLogsQuery(action, entityType, page, pageSize), ct);
            return Ok(result.Value);
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings(CancellationToken ct)
        {
            var result = await _sender.Send(new GetSystemSettingsQuery(), ct);
            return Ok(result.Value);
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] List<UpdateSettingItem> items, CancellationToken ct)
        {
            var result = await _sender.Send(new UpdateSystemSettingsCommand(items, GetActorId()), ct);
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Đã lưu cài đặt hệ thống." });
        }

        [HttpGet("account-requests")]
        public async Task<IActionResult> GetAccountRequests([FromQuery] string status = "pending", CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetAccountRequestsQuery(status), ct);
            return Ok(result.Value);
        }

        [HttpPost("account-requests/{id}/approve")]
        public async Task<IActionResult> ApproveAccountRequest(Guid id)
        {
            var result = await _sender.Send(new ApproveAccountRequestCommand(id, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                    CommonErrorCodes.Conflict => Conflict(new { message = result.Error }),
                    _ => BadRequest(new { message = result.Error }),
                };
            }
            return Ok(new { message = "Đã duyệt yêu cầu và tạo tài khoản." });
        }

        [HttpPost("account-requests/{id}/reject")]
        public async Task<IActionResult> RejectAccountRequest(Guid id, [FromBody] RejectRequest? request = null)
        {
            var result = await _sender.Send(new RejectAccountRequestCommand(id, request?.Reason, GetActorId()));
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }
            return Ok(new { message = "Đã từ chối yêu cầu." });
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
