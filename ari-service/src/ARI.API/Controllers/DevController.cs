using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Dev.SeedPractice;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace ARI.API.Controllers
{
    /// <summary>
    /// DEV-ONLY tooling (ADR-050). MỌI action trả 404 khi KHÔNG phải môi trường Development
    /// → endpoint trơ ở production (an toàn dù có auto-register controller). Không đưa nghiệp
    /// vụ thật vào đây. <c>AllowAnonymous</c> vì đây là bootstrap trước khi có tài khoản/login.
    /// </summary>
    [ApiController]
    [Route("api/dev")]
    [AllowAnonymous]
    public class DevController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ISender _sender;

        public DevController(IWebHostEnvironment env, ISender sender)
        {
            _env = env;
            _sender = sender;
        }

        /// <summary>
        /// Tạo sẵn một hồ sơ đủ điều kiện phỏng vấn thử để test nhanh, lặp lại (ADR-050).
        /// Idempotent; <c>?fresh=true</c> tạo application mới. Trả về practiceUrl + thông tin
        /// đăng nhập candidate. Kết hợp <c>Interview:PracticeAttemptsPerRound=0</c> để chạy lại vô hạn.
        /// </summary>
        [HttpPost("seed-practice")]
        public async Task<IActionResult> SeedPractice([FromQuery] bool fresh, CancellationToken ct)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var result = await _sender.Send(new SeedPracticeCommand(fresh), ct);
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            var v = result.Value;
            return Ok(new
            {
                message = v.ReusedApplication
                    ? "Đã dùng lại hồ sơ practice có sẵn. Đăng nhập candidate rồi mở practiceUrl."
                    : "Đã tạo hồ sơ practice mới. Đăng nhập candidate rồi mở practiceUrl.",
                // Thông tin đăng nhập candidate (dev). Không phải secret hệ thống — tài khoản test tạo tại chỗ.
                credentials = new { email = v.CandidateEmail, login = v.Password },
                applicationId = v.ApplicationId,
                practiceUrl = v.PracticeUrl,
                note = "Cần Interview:PracticeAttemptsPerRound=0 (appsettings.Development.json) để chạy lại nhiều lần trên cùng hồ sơ. Endpoint chỉ hoạt động khi ASPNETCORE_ENVIRONMENT=Development."
            });
        }
    }
}
