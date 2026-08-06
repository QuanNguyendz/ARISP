using System.Threading;
using System.Threading.Tasks;
using System;
using ARI.Application.Dev.RegradeSession;
using ARI.Application.Dev.SeedInterviewJob;
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

        /// <summary>
        /// Dựng job ĐỦ 3 VÒNG (sơ loại → trắc nghiệm → chuyên môn) để test trọn quy trình:
        /// phỏng vấn thử, phỏng vấn thật ở Kiosk, thi trắc nghiệm, HR xác nhận từng vòng (ADR-053).
        /// Trả sẵn mã Kiosk vòng 1 + thông tin đăng nhập ứng viên và HR (tài khoản test tạo tại chỗ).
        /// </summary>
        [HttpPost("seed-interview-job")]
        public async Task<IActionResult> SeedInterviewJob([FromQuery] bool fresh, CancellationToken ct)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var result = await _sender.Send(new SeedInterviewJobCommand(fresh), ct);
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            var v = result.Value;
            return Ok(new
            {
                message = v.ReusedApplication
                    ? "Đã dùng lại hồ sơ có sẵn trên job 3 vòng."
                    : "Đã tạo job 3 vòng + hồ sơ ứng tuyển mới.",
                // Tài khoản test dev tạo tại chỗ (không phải secret hệ thống) — cùng quy ước đặt tên
                // "login" như seed-practice ở trên.
                candidate = new { email = v.CandidateEmail, login = v.CandidateLogin },
                staff = new { email = v.StaffEmail, login = v.StaffLogin, role = "Hr_admin" },
                jobPostingId = v.JobPostingId,
                applicationId = v.ApplicationId,
                rounds = new object[]
                {
                    new { round = 1, type = "screening", how = "Phỏng vấn AI — thử qua Portal, thật qua Kiosk" },
                    new { round = 2, type = "online_test", how = "Thi trắc nghiệm tại /candidate/online-test/{applicationId}" },
                    new { round = 3, type = "technical", how = "Phỏng vấn AI thật qua Kiosk (cấp mã mới sau khi HR duyệt vòng 1)" },
                },
                practiceUrl = v.PracticeUrl,
                kioskCode = v.KioskCode,
                kioskCodeExpiresAt = v.KioskCodeExpiresAt,
                urls = new
                {
                    candidateApplications = "/candidate/applications",
                    kiosk = "/kiosk",
                    staffEvaluations = "/hr/evaluations",
                    staffCandidate = $"/hr/candidates/{v.ApplicationId}",
                    onlineTestBank = $"/hr/jobs/{v.JobPostingId}/online-test",
                    onlineTestResults = $"/hr/jobs/{v.JobPostingId}/online-test/results",
                },
                note = "Mã Kiosk dùng MỘT lần, hết hạn sau 2 giờ. Gọi lại endpoint này để lấy mã mới khi cần."
            });
        }

        /// <summary>
        /// Chấm LẠI một phiên đã kết thúc bằng prompt hiện tại — dùng khi báo cáo cũ sinh từ prompt
        /// lỗi thời (sai ngôn ngữ, thiếu điểm từng câu). <c>?lang=vi|en</c> ép ngôn ngữ báo cáo.
        /// Từ chối nếu HR đã xác nhận đánh giá đó.
        /// </summary>
        [HttpPost("regrade-session/{sessionId:guid}")]
        public async Task<IActionResult> RegradeSession(Guid sessionId, [FromQuery] string? lang, CancellationToken ct)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var result = await _sender.Send(new RegradeSessionCommand(sessionId, lang), ct);
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { success = true, message = "Đã chấm lại phiên. Mở lại trang xem lại để kiểm tra." });
        }
    }
}
