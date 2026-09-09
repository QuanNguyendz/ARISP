using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Emails;
using ARI.Application.Interfaces;
using ARI.Application.Offers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    public class DecideOfferRequest
    {
        /// <summary>approved | rejected</summary>
        [Required] public string Decision { get; set; } = string.Empty;
        /// <summary>Bắt buộc khi trả về bản nháp.</summary>
        public string? Note { get; set; }
    }

    public class SendOfferRequest
    {
        /// <summary>Thư mời do nhân sự sửa ở trình soạn thảo (ADR-061). Bỏ trống → dùng mẫu.</summary>
        public EmailOverride? EmailOverride { get; set; }
    }

    public class WithdrawOfferRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập lý do thu hồi thư mời.")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Thư mời nhận việc (ADR-061, Phase 5) — đoạn kết của phễu tuyển dụng.
    ///
    /// Luồng: nháp → gửi duyệt → Hiring Manager duyệt → gửi ứng viên → ứng viên nhận/từ chối.
    /// Hồ sơ chỉ chuyển sang <c>offer</c> khi thư ĐƯỢC GỬI — bản nháp không phải lời hứa.
    /// </summary>
    [ApiController]
    [Route("api/offers")]
    [Authorize(Policy = "InternalStaff")]
    public class OffersController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public OffersController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        private IActionResult MapFailure(string? errorCode, string? message) => errorCode switch
        {
            CommonErrorCodes.NotFound => NotFound(new { message }),
            CommonErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message }),
            CommonErrorCodes.Conflict => Conflict(new { message }),
            _ => BadRequest(new { message }),
        };

        [HttpGet]
        public async Task<IActionResult> GetOffers([FromQuery] string? status, CancellationToken ct)
        {
            var result = await _sender.Send(new GetOffersQuery(_currentUser.UserId, _currentUser.Role, status), ct);
            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOffer(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetOfferByIdQuery(id, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertOfferRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new CreateOfferCommand(request, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpsertOfferRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new UpdateOfferCommand(id, request, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }

        [HttpPost("{id:guid}/submit")]
        public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new SubmitOfferCommand(id, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure
                ? MapFailure(result.ErrorCode, result.Error)
                : Ok(new { message = "Đã gửi thư mời đi duyệt." });
        }

        /// <summary>
        /// HR Leader chốt mức lương/điều kiện, hoặc trả về bản nháp kèm góp ý.
        ///
        /// ADR-063 chuyển cổng này từ <c>HiringDecision</c> sang <c>OfferApproval</c>: Hiring Manager
        /// ĐỀ XUẤT mức lương (soạn thư, gửi duyệt) nhưng không tự chốt đề xuất của chính mình —
        /// người quyết định cuối cùng về thư mời là HR Leader.
        /// </summary>
        [HttpPost("{id:guid}/decide")]
        [Authorize(Policy = "OfferApproval")]
        public async Task<IActionResult> Decide(Guid id, [FromBody] DecideOfferRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(
                new DecideOfferCommand(id, request.Decision, request.Note, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure
                ? MapFailure(result.ErrorCode, result.Error)
                : Ok(new { message = "Đã ghi nhận quyết định." });
        }

        /// <summary>Gửi thư mời cho ứng viên — hồ sơ chuyển sang trạng thái "offer" từ đây.</summary>
        [HttpPost("{id:guid}/send")]
        public async Task<IActionResult> Send(Guid id, [FromBody] SendOfferRequest? request, CancellationToken ct)
        {
            var result = await _sender.Send(
                new SendOfferCommand(id, _currentUser.UserId, _currentUser.Role, request?.EmailOverride), ct);
            return result.IsFailure
                ? MapFailure(result.ErrorCode, result.Error)
                : Ok(new { message = "Đã gửi thư mời nhận việc cho ứng viên." });
        }

        [HttpPost("{id:guid}/withdraw")]
        [Authorize(Policy = "HrManagement")]
        public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawOfferRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(
                new WithdrawOfferCommand(id, request.Reason, _currentUser.UserId, _currentUser.Role), ct);
            return result.IsFailure
                ? MapFailure(result.ErrorCode, result.Error)
                : Ok(new { message = "Đã thu hồi thư mời." });
        }
    }
}
