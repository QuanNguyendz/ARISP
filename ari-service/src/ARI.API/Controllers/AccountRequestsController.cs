using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.AccountRequests;
using ARI.Application.AccountRequests.Commands.CreateAccountRequests;
using ARI.Application.AccountRequests.Queries.GetMyAccountRequests;
using ARI.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// HR Leader gửi yêu cầu tạo tài khoản staff (lẻ hoặc hàng loạt) lên Super Admin phê duyệt.
    /// </summary>
    [ApiController]
    [Route("api/hr/account-requests")]
    [Authorize(Policy = "HrManagement")]
    public class AccountRequestsController : ControllerBase
    {
        private readonly ISender _sender;

        public AccountRequestsController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Danh sách yêu cầu do chính HR Leader hiện tại đã gửi (theo dõi trạng thái).</summary>
        [HttpGet]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var actorId = GetActorId();
            if (actorId == null) return Unauthorized();

            var result = await _sender.Send(new GetMyAccountRequestsQuery(actorId.Value), ct);
            return Ok(result.Value);
        }

        /// <summary>Tạo một hoặc nhiều yêu cầu tạo tài khoản. Nhiều mục → cùng một BatchId.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] List<AccountRequestItem> items, CancellationToken ct)
        {
            if (items == null || items.Count == 0)
                return BadRequest(new { message = "Danh sách yêu cầu trống." });

            var actorId = GetActorId();
            if (actorId == null) return Unauthorized();

            var result = await _sender.Send(new CreateAccountRequestsCommand(items, actorId.Value), ct);
            if (result.IsFailure)
            {
                return result.ErrorCode == CommonErrorCodes.Conflict
                    ? Conflict(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }

            var value = result.Value;
            return Ok(new { message = $"Đã gửi {value.Count} yêu cầu tạo tài khoản chờ Super Admin duyệt.", count = value.Count, batchId = value.BatchId });
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
