using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.RecruitmentRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Phiếu yêu cầu tuyển dụng (ADR-063) — điểm bắt đầu bắt buộc của mọi tin tuyển dụng.
    ///
    /// Hiring Manager lập phiếu → HR Leader duyệt kèm phân công Recruiter → Recruiter dựng tin.
    ///
    /// Danh tính người gọi lấy từ <see cref="ICurrentUserService"/> (token), KHÔNG bao giờ từ tham
    /// số client khai — ADR-061 đã phải vá đúng lỗ đó ở <c>review/confirm</c> (đọc người duyệt từ
    /// header) và ở <c>?mine=true</c>. Phạm vi dữ liệu cũng do server tính, xem
    /// <c>RecruitmentRequestScope</c>.
    /// </summary>
    [ApiController]
    [Route("api/recruitment-requests")]
    [Authorize(Policy = "InternalStaff")]
    public class RecruitmentRequestsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public RecruitmentRequestsController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Danh sách phiếu trong phạm vi của người gọi: HM thấy phiếu mình lập, Recruiter thấy phiếu
        /// được giao, HR Leader/Super Admin thấy tất cả.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? priority, CancellationToken ct)
        {
            var result = await _sender.Send(
                new GetRecruitmentRequestsQuery(status, search, priority, _currentUser.UserId, _currentUser.Role), ct);

            return result.IsFailure ? BadRequest(new { message = result.Error }) : Ok(result.Value);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(
                new GetRecruitmentRequestByIdQuery(id, _currentUser.UserId, _currentUser.Role), ct);

            return Respond(result, r => Ok(r.Value));
        }

        /// <summary>Hiring Manager lập phiếu cho đội của mình.</summary>
        [HttpPost]
        [Authorize(Policy = "RecruitmentRequestAuthoring")]
        public async Task<IActionResult> Create([FromBody] RecruitmentRequestInput input, CancellationToken ct)
        {
            var result = await _sender.Send(new CreateRecruitmentRequestCommand(input, _currentUser.UserId, _currentUser.Role), ct);

            return result.IsFailure
                ? BadRequest(new { message = result.Error })
                : CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });
        }

        /// <summary>Sửa nội dung phiếu — chỉ chính chủ, và chỉ khi phiếu chưa duyệt hoặc bị trả về.</summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] RecruitmentRequestInput input, CancellationToken ct)
        {
            var result = await _sender.Send(
                new UpdateRecruitmentRequestCommand(id, input, _currentUser.UserId, _currentUser.Role), ct);

            return Respond(result, _ => NoContent());
        }

        /// <summary>Gửi lại phiếu sau khi đã sửa theo góp ý của HR Leader.</summary>
        [HttpPost("{id:guid}/resubmit")]
        public async Task<IActionResult> Resubmit(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new ResubmitRecruitmentRequestCommand(id, _currentUser.UserId), ct);
            return Respond(result, _ => NoContent());
        }

        /// <summary>Chính chủ rút phiếu.</summary>
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new CancelRecruitmentRequestCommand(id, _currentUser.UserId), ct);
            return Respond(result, _ => NoContent());
        }

        /// <summary>
        /// HR Leader duyệt phiếu VÀ phân công Recruiter trong cùng một thao tác. Duyệt mà không có
        /// người phụ trách thì phiếu nằm im — cùng bài học với ADR-059 ("duyệt CV phải kèm xếp lịch").
        /// </summary>
        [HttpPost("{id:guid}/approve")]
        [Authorize(Policy = "RecruitmentRequestReview")]
        public async Task<IActionResult> Approve(
            Guid id, [FromBody] ApproveRecruitmentRequestBody body, CancellationToken ct)
        {
            var result = await _sender.Send(
                new ApproveRecruitmentRequestCommand(id, body.AssignedRecruiterId, body.Note, _currentUser.UserId), ct);

            return Respond(result, _ => NoContent());
        }

        /// <summary>HR Leader trả phiếu về cho HM kèm lý do (thường là chỉnh dải lương).</summary>
        [HttpPost("{id:guid}/reject")]
        [Authorize(Policy = "RecruitmentRequestReview")]
        public async Task<IActionResult> Reject(
            Guid id, [FromBody] RejectRecruitmentRequestBody body, CancellationToken ct)
        {
            var result = await _sender.Send(
                new RejectRecruitmentRequestCommand(id, body.Reason, _currentUser.UserId), ct);

            return Respond(result, _ => NoContent());
        }

        /// <summary>
        /// Mở lại phiếu ĐÃ DUYỆT để sửa (ADR-066) — quay về chờ duyệt, phân công bị gỡ theo chữ ký.
        ///
        /// KHÔNG gác bằng policy <c>RecruitmentRequestReview</c>: người dùng chính là <b>Hiring Manager
        /// chủ phiếu</b>, mà vai đó không qua được policy duyệt. Điều kiện "chủ phiếu hoặc quản trị viên"
        /// viết tường minh trong handler — cùng lý lẽ với ADR-063 khi tách <c>OfferApproval</c>.
        /// </summary>
        [HttpPost("{id:guid}/reopen")]
        public async Task<IActionResult> Reopen(
            Guid id, [FromBody] RevokeRecruitmentRequestBody body, CancellationToken ct)
        {
            var result = await _sender.Send(
                new ReopenRecruitmentRequestCommand(id, body?.Reason, _currentUser.UserId, _currentUser.Role), ct);

            return Respond(result, _ => NoContent());
        }

        /// <summary>Đóng phiếu đã duyệt vì hết nhu cầu (ADR-066). Khác <c>cancel</c> ở chỗ bắt buộc lý do + báo cho người liên quan.</summary>
        [HttpPost("{id:guid}/close")]
        public async Task<IActionResult> Close(
            Guid id, [FromBody] RevokeRecruitmentRequestBody body, CancellationToken ct)
        {
            var result = await _sender.Send(
                new CloseRecruitmentRequestCommand(id, body?.Reason, _currentUser.UserId, _currentUser.Role), ct);

            return Respond(result, _ => NoContent());
        }

        /// <summary>Ánh xạ mã lỗi nghiệp vụ sang mã HTTP — giữ giống các controller khác.</summary>
        private IActionResult Respond<T>(T result, Func<T, IActionResult> onSuccess) where T : Result
        {
            if (!result.IsFailure) return onSuccess(result);

            return result.ErrorCode switch
            {
                CommonErrorCodes.NotFound => NotFound(new { message = result.Error }),
                CommonErrorCodes.Forbidden => StatusCode(403, new { message = result.Error }),
                CommonErrorCodes.Conflict => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };
        }
    }

    public record ApproveRecruitmentRequestBody(Guid AssignedRecruiterId, string? Note);

    public record RejectRecruitmentRequestBody(string? Reason);

    /// <summary>Dùng chung cho cả <c>reopen</c> lẫn <c>close</c> — hai thao tác cùng nhận đúng một thứ.</summary>
    public record RevokeRecruitmentRequestBody(string? Reason);
}
