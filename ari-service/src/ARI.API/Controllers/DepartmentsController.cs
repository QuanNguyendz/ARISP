using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Departments;
using ARI.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Đội/bộ phận của công ty (ADR-065).
    ///
    /// <b>ĐỌC cho mọi nhân sự nội bộ</b> — biểu mẫu phiếu yêu cầu tuyển dụng cần danh sách để quản
    /// trị viên chọn đội khi lập hộ. <b>GHI chỉ Super Admin</b>: đây là cơ cấu tổ chức, và cho người
    /// khác sửa được là mở lại đúng đường mà thay đổi này vừa bịt.
    /// </summary>
    [ApiController]
    [Route("api/departments")]
    [Authorize(Policy = "InternalStaff")]
    public class DepartmentsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public DepartmentsController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        /// <summary><paramref name="activeOnly"/> = true khi dùng cho ô chọn: đội đã tắt không được gán mới.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false, CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetDepartmentsQuery(activeOnly), ct);
            return result.IsFailure ? BadRequest(new { message = result.Error }) : Ok(result.Value);
        }

        [HttpPost]
        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Create([FromBody] DepartmentInput input, CancellationToken ct)
        {
            var result = await _sender.Send(new CreateDepartmentCommand(input, _currentUser.UserId), ct);

            if (result.IsFailure)
                return result.ErrorCode == CommonErrorCodes.Conflict
                    ? Conflict(new { message = result.Error })
                    : BadRequest(new { message = result.Error });

            return Ok(new { id = result.Value });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DepartmentInput input, CancellationToken ct)
        {
            var result = await _sender.Send(new UpdateDepartmentCommand(id, input, _currentUser.UserId), ct);

            if (!result.IsFailure) return NoContent();

            return result.ErrorCode switch
            {
                CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                CommonErrorCodes.Conflict => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };
        }
    }
}
