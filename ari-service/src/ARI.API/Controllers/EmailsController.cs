using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Emails;
using ARI.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>Body của POST /api/emails/preview.</summary>
    public class PreviewEmailRequest
    {
        /// <summary>Khoá mẫu thư — xem <c>EmailTemplateKeys</c>.</summary>
        [Required(ErrorMessage = "templateKey là bắt buộc.")]
        public string TemplateKey { get; set; } = string.Empty;

        /// <summary>Hồ sơ ứng tuyển làm ngữ cảnh dựng thư.</summary>
        [Required(ErrorMessage = "contextId là bắt buộc.")]
        public Guid ContextId { get; set; }

        /// <summary>Tham số phụ theo từng mẫu — thư mời phỏng vấn dùng để truyền <c>slotId</c>.</summary>
        public Guid? SecondaryId { get; set; }
    }

    /// <summary>
    /// Trình soạn thảo thư gửi ứng viên (ADR-061, Phase 4).
    ///
    /// Ở đây CHỈ có bước xem trước và lịch sử. Việc GỬI luôn đi kèm chính lệnh nghiệp vụ tương ứng
    /// (duyệt CV + xếp lịch, gán ca, loại hồ sơ…) qua tham số <c>emailOverride</c> — không có
    /// endpoint "gửi thư rời", vì thư rời khỏi hành động là cách tạo ra đúng trạng thái mà ADR-059
    /// đã phải đi chữa: ứng viên bị xếp lịch mà không thư nào tới nơi.
    /// </summary>
    [ApiController]
    [Route("api/emails")]
    [Authorize(Policy = "InternalStaff")]
    public class EmailsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUserService;

        public EmailsController(ISender sender, ICurrentUserService currentUserService)
        {
            _sender = sender;
            _currentUserService = currentUserService;
        }

        private IActionResult MapFailure(string? errorCode, string? message) => errorCode switch
        {
            CommonErrorCodes.NotFound => NotFound(new { message }),
            CommonErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message }),
            _ => BadRequest(new { message }),
        };

        /// <summary>
        /// Nội dung thư đã điền đầy đủ, sẵn sàng cho nhân sự sửa. Mọi giá trị được thay THẬT ở đây
        /// — không còn placeholder nào trong nội dung trả về.
        /// </summary>
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] PreviewEmailRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(new PreviewEmailQuery(
                request.TemplateKey, request.ContextId, request.SecondaryId,
                _currentUserService.UserId, _currentUserService.Role), ct);

            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }
    }
}
