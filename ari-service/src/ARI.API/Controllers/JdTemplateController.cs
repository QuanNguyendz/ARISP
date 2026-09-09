using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.JdTemplates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Mẫu bản mô tả công việc của công ty (ADR-064) — logo, thông tin công ty, màu/phông và danh
    /// sách mục. Mọi JD dựng từ trình soạn đều theo mẫu này.
    ///
    /// <b>ĐỌC thì mọi nhân sự đều được</b> (trình soạn JD cần mẫu để dựng biểu mẫu và xem trước),
    /// nhưng <b>SỬA là việc của HR Leader</b>. Cố ý không đặt trong <c>AdminController</c>: chỗ đó là
    /// <c>SuperAdminOnly</c>, mà mẫu JD thuộc quy trình tuyển dụng chứ không phải quản trị hệ thống.
    /// </summary>
    [ApiController]
    [Route("api/jd-template")]
    [Authorize(Policy = "InternalStaff")]
    public class JdTemplateController : ControllerBase
    {
        /// <summary>Chặn sớm ở tầng HTTP; handler vẫn kiểm lại kích thước và định dạng.</summary>
        private const int MaxLogoBytes = 2 * 1024 * 1024;

        private readonly ISender _sender;
        private readonly ICurrentUserService _currentUser;

        public JdTemplateController(ISender sender, ICurrentUserService currentUser)
        {
            _sender = sender;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var result = await _sender.Send(new GetJdTemplateQuery(), ct);
            return result.IsFailure ? BadRequest(new { message = result.Error }) : Ok(result.Value);
        }

        [HttpPut]
        [Authorize(Policy = "HrManagement")]
        public async Task<IActionResult> Update([FromBody] UpdateJdTemplateInput input, CancellationToken ct)
        {
            var result = await _sender.Send(new UpdateJdTemplateCommand(input, _currentUser.UserId), ct);
            return result.IsFailure ? BadRequest(new { message = result.Error }) : NoContent();
        }

        [HttpPost("logo")]
        [Authorize(Policy = "HrManagement")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Chưa chọn file logo." });

            if (file.Length > MaxLogoBytes)
                return BadRequest(new { message = "Logo không được vượt quá 2MB." });

            using var mem = new MemoryStream();
            await file.CopyToAsync(mem, ct);

            var result = await _sender.Send(
                new UploadCompanyLogoCommand(mem.ToArray(), file.FileName, file.ContentType, _currentUser.UserId), ct);

            return result.IsFailure
                ? BadRequest(new { message = result.Error })
                : Ok(new { logoUrl = result.Value });
        }
    }
}
