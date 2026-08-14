using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Queries.GetRecruiters;
using ARI.Application.Common;
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

        /// <summary>GET /api/staff/profile — hồ sơ của chính người đang đăng nhập.</summary>
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetActorId();
            if (userId == null) return Unauthorized();

            var result = await _sender.Send(new GetStaffProfileQuery(userId.Value));
            if (result.IsFailure) return NotFound(new { message = result.Error });

            return Ok(result.Value);
        }

        /// <summary>PUT /api/staff/profile — đổi họ tên / phòng ban. Email và vai trò không sửa được.</summary>
        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateStaffProfileRequest request)
        {
            var userId = GetActorId();
            if (userId == null) return Unauthorized();

            var result = await _sender.Send(new UpdateStaffProfileCommand(userId.Value, request.FullName, request.Department));
            if (result.IsFailure)
                return result.ErrorCode == CommonErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });

            return Ok(result.Value);
        }

        /// <summary>POST /api/staff/profile/change-password — đổi, hoặc đặt lần đầu với tài khoản Google.</summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangeStaffPasswordRequest request)
        {
            var userId = GetActorId();
            if (userId == null) return Unauthorized();

            var result = await _sender.Send(new ChangeStaffPasswordCommand(userId.Value, request.CurrentPassword, request.NewPassword));
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                    "wrong_current_password" => BadRequest(new { message = result.Error, code = "wrong_current_password" }),
                    _ => BadRequest(new { message = result.Error }),
                };
            }

            return Ok(new { message = result.Value, hasPassword = true });
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

        /// <summary>
        /// GET /api/staff/recruiters — danh sách Recruiter kèm khối lượng công việc, cho màn
        /// "Quản lý Recruiter" của HR Lead. `HrManagement` = HR Admin hoặc Super Admin; Recruiter
        /// không xem được danh sách đồng nghiệp. Chỉ đọc — mọi thao tác vòng đời tài khoản vẫn
        /// nằm ở AdminController với policy SuperAdminOnly (ADR-023/041).
        /// </summary>
        [HttpGet("/api/staff/recruiters")]
        [Authorize(Policy = "HrManagement")]
        public async Task<IActionResult> GetRecruiters(CancellationToken ct)
        {
            var result = await _sender.Send(new GetRecruitersQuery(), ct);
            if (result.IsFailure) return BadRequest(new { message = result.Error });

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
