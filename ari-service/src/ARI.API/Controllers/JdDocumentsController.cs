using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Application.JdDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Bản mô tả công việc soạn theo mẫu công ty, gắn với một phiếu yêu cầu tuyển dụng (ADR-064).
    ///
    /// Phân quyền **trong handler** (`RecruitmentRequestAccess`), không chỉ bằng policy: policy chỉ
    /// biết vai trò, còn luật thật là "đúng Recruiter được phân công TRÊN PHIẾU NÀY" — cùng điều
    /// kiện mà `CreateJobCommand` dùng.
    /// </summary>
    [ApiController]
    [Route("api/recruitment-requests/{requestId:guid}/jd")]
    [Authorize(Policy = "InternalStaff")]
    public class JdDocumentsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public JdDocumentsController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        /// <summary>Bản JD đang soạn; chưa có thì trả bản khởi tạo từ chính phiếu (chưa lưu DB).</summary>
        [HttpGet]
        public async Task<IActionResult> Get(Guid requestId, CancellationToken ct)
        {
            var result = await _sender.Send(
                new GetJdDocumentQuery(requestId, _currentUser.UserId, _currentUser.Role), ct);

            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }

        [HttpPut]
        public async Task<IActionResult> Save(Guid requestId, [FromBody] JdDocumentInput input, CancellationToken ct)
        {
            var result = await _sender.Send(
                new SaveJdDocumentCommand(requestId, input, _currentUser.UserId, _currentUser.Role), ct);

            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : NoContent();
        }

        /// <summary>
        /// Xuất file JD. Trả về storageKey + URL xem được — màn tạo tin gắn thẳng file này, không
        /// phải tải xuống rồi tải lên lại.
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> Generate(
            Guid requestId, [FromQuery] string? format, CancellationToken ct)
        {
            var result = await _sender.Send(
                new GenerateJdFileCommand(requestId, format, _currentUser.UserId, _currentUser.Role), ct);

            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }

        /// <summary>
        /// Gợi ý danh sách kỹ năng cho màn tạo tin, suy ra từ chính bản JD đã soạn.
        ///
        /// Là <c>POST</c> chứ không <c>GET</c> vì mỗi lượt là một lượt Gemini có tính phí — không để trình duyệt
        /// hay proxy nào tự ý gọi lại.
        /// </summary>
        [HttpPost("skills")]
        public async Task<IActionResult> ExtractSkills(Guid requestId, CancellationToken ct)
        {
            var result = await _sender.Send(
                new ExtractJdSkillsCommand(requestId, _currentUser.UserId, _currentUser.Role), ct);

            return result.IsFailure ? MapFailure(result.ErrorCode, result.Error) : Ok(result.Value);
        }

        private IActionResult MapFailure(string? code, string message) => code switch
        {
            CommonErrorCodes.NotFound => NotFound(new { message }),
            CommonErrorCodes.Forbidden => StatusCode(403, new { message }),
            CommonErrorCodes.Conflict => Conflict(new { message }),
            _ => BadRequest(new { message }),
        };
    }
}
