using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Commands.CandidateForgotPassword;
using ARI.Application.Auth.Commands.CandidateLogin;
using ARI.Application.Auth.Commands.CandidateResetPassword;
using ARI.Application.Auth.Commands.CompleteExternalCandidateSignIn;
using ARI.Application.Auth.Commands.CompleteExternalStaffSignIn;
using ARI.Application.Auth.Commands.Logout;
using ARI.Application.Auth.Commands.RefreshCandidateToken;
using ARI.Application.Auth.Commands.RefreshStaffToken;
using ARI.Application.Auth.Commands.RegisterCandidate;
using ARI.Application.Auth.Commands.ResendCandidateVerification;
using ARI.Application.Auth.Commands.StaffForgotPassword;
using ARI.Application.Auth.Commands.StaffLogin;
using ARI.Application.Auth.Commands.StaffResetPassword;
using ARI.Application.Auth.Commands.VerifyCandidateEmail;
using ARI.Application.Auth.Commands.VerifyMagicLink;
using ARI.Application.Auth.Queries.GetCurrentUser;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.API.Controllers
{
    /// <summary>
    /// Cổng xác thực — controller thin: nghiệp vụ nằm trong ARI.Application/Auth (CQRS),
    /// controller chỉ giữ HTTP mapping + luồng protocol OAuth (Challenge/Authenticate/SignOut/Redirect).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IConfiguration _configuration;

        public AuthController(ISender sender, IConfiguration configuration)
        {
            _sender = sender;
            _configuration = configuration;
        }

        // ============================================================
        // CANDIDATE LOGIN ENDPOINTS
        // ============================================================

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 1: DÀNH RIÊNG CHO ỨNG VIÊN (Candidate - Tại /jobs/login)
        /// Xác thực truyền thống qua form điền Email + Mật khẩu cá nhân
        /// </summary>
        [HttpPost("candidate/login")]
        [AllowAnonymous]
        public async Task<IActionResult> CandidateLogin([FromBody] LoginRequest request)
        {
            var result = await _sender.Send(new CandidateLoginCommand(request.Email, request.Password));
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    // Kèm `code` để FE hiện được lối thoát (nút đặt mật khẩu) thay vì chỉ in câu lỗi —
                    // trước đây nhánh này là nhánh DUY NHẤT không trả code nên FE không phân biệt được.
                    AuthErrorCodes.PasswordlessGoogle => BadRequest(new { message = result.Error, code = "passwordless_google" }),
                    AuthErrorCodes.EmailNotVerified => StatusCode(403, new { message = result.Error, code = "email_not_verified" }),
                    _ => Unauthorized(new { message = result.Error }),
                };
            }
            return Ok(result.Value);
        }

        // ============================================================
        // HR / INTERNAL STAFF LOGIN (Email + Password)
        // ============================================================

        /// <summary>
        /// CỔNG ĐĂNG NHẬP NỘI BỘ: Dành cho Super Admin, HR Admin, Recruiter
        /// Xác thực truyền thống qua form điền Email + Mật khẩu (tài khoản được Super Admin cấp phát trước)
        /// </summary>
        [HttpPost("staff/login")]
        [AllowAnonymous]
        public async Task<IActionResult> StaffLogin([FromBody] LoginRequest request)
        {
            var result = await _sender.Send(new StaffLoginCommand(request.Email, request.Password));
            if (result.IsFailure)
            {
                return result.ErrorCode switch
                {
                    AuthErrorCodes.InvalidCredentials or AuthErrorCodes.AccountDisabled => Unauthorized(new { message = result.Error }),
                    // SsoOnly + lỗi validation (không có ErrorCode) → 400 như cũ
                    _ => BadRequest(new { message = result.Error }),
                };
            }
            return Ok(result.Value);
        }

        // ============================================================
        // HR / INTERNAL STAFF LOGIN (OAuth2)
        // ============================================================

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 2: ĐIỀU HƯỚNG CHALLENGE OAUTH2
        /// </summary>
        [HttpGet("external/signin")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalSignIn([FromQuery] string provider = "Google", [FromQuery] string returnUrl = "/")
        {
            if (string.IsNullOrEmpty(provider)) provider = "Google";

            if (!await IsExternalProviderAvailableAsync(provider))
                return StatusCode(503, new { message = $"Đăng nhập {provider} hiện chưa khả dụng. Vui lòng dùng email & mật khẩu." });

            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("ExternalCallback", new { provider, returnUrl })
            };

            return Challenge(props, provider);
        }

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 2 (CALLBACK): TIẾP NHẬN DỮ LIỆU ĐĂNG NHẬP OAUTH2 VÀ XỬ LÝ PROVISIONING
        /// </summary>
        [HttpGet("external/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalCallback([FromQuery] string provider = "Google", [FromQuery] string returnUrl = "/")
        {
            var result = await HttpContext.AuthenticateAsync("External");
            if (result?.Succeeded != true)
            {
                return BadRequest(new { message = "External authentication failed." });
            }

            var externalPrincipal = result.Principal;
            var email = externalPrincipal?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;

            if (string.IsNullOrEmpty(email))
            {
                await HttpContext.SignOutAsync("External");
                return BadRequest(new { message = "External provider did not return an email." });
            }

            var signIn = await _sender.Send(new CompleteExternalStaffSignInCommand(email));
            await HttpContext.SignOutAsync("External");

            if (signIn.IsFailure)
            {
                // Mọi nhánh thất bại đều PHẢI redirect về frontend. Trước đây DomainNotAllowed trả
                // Forbid() → trình duyệt dừng ở trang 403 trắng của chính API (localhost:5000), người
                // dùng không có đường quay lại màn đăng nhập ngoài việc tự sửa URL.
                return signIn.ErrorCode switch
                {
                    AuthErrorCodes.DomainNotAllowed => Redirect(BuildRedirectUrl(returnUrl, new[] { ("status", "rejected"), ("message", AuthErrorCodes.DomainNotAllowed) })),
                    AuthErrorCodes.PendingApproval => Redirect(BuildRedirectUrl(returnUrl, new[] { ("status", "pending"), ("message", AuthErrorCodes.PendingApproval) })),
                    _ => Redirect(BuildRedirectUrl(returnUrl, new[] { ("status", "rejected"), ("message", AuthErrorCodes.NotProvisioned) })),
                };
            }

            var tokens = signIn.Value;
            var redirectUrl = BuildRedirectUrl(returnUrl, fragment: $"access_token={Uri.EscapeDataString(tokens.AccessToken)}&refresh_token={Uri.EscapeDataString(tokens.RefreshToken)}&role={Uri.EscapeDataString(tokens.Role)}");
            return Redirect(redirectUrl);
        }

        // ============================================================
        // CANDIDATE LOGIN (OAuth2 - Google Sign-In)
        // ============================================================

        /// <summary>
        /// CANDIDATE GOOGLE SIGN-IN: ĐIỀU HƯỚNG CHALLENGE OAUTH2
        /// Khác với staff: ứng viên được tự đăng ký tự do nên KHÔNG validate domain, JIT tạo tài khoản nếu chưa có.
        /// </summary>
        [HttpGet("candidate/external/signin")]
        [AllowAnonymous]
        public async Task<IActionResult> CandidateExternalSignIn([FromQuery] string provider = "Google", [FromQuery] string returnUrl = "/")
        {
            if (string.IsNullOrEmpty(provider)) provider = "Google";

            if (!await IsExternalProviderAvailableAsync(provider))
                return StatusCode(503, new { message = $"Đăng nhập {provider} hiện chưa khả dụng. Vui lòng dùng email & mật khẩu." });

            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("CandidateExternalCallback", new { provider, returnUrl })
            };

            return Challenge(props, provider);
        }

        /// <summary>
        /// CANDIDATE GOOGLE SIGN-IN (CALLBACK): tiếp nhận dữ liệu Google, JIT tạo CandidateAccount nếu chưa tồn tại,
        /// sinh JWT + refresh token cho ứng viên rồi redirect kèm token về frontend.
        /// </summary>
        [HttpGet("candidate/external/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> CandidateExternalCallback([FromQuery] string provider = "Google", [FromQuery] string returnUrl = "/")
        {
            var result = await HttpContext.AuthenticateAsync("External");
            if (result?.Succeeded != true)
            {
                var failUrl = BuildRedirectUrl(returnUrl, new[] { ("status", "error"), ("message", "external_authentication_failed") });
                return Redirect(failUrl);
            }

            var externalPrincipal = result.Principal;
            var rawEmail = externalPrincipal?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
            var name = externalPrincipal?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "name")?.Value;

            if (string.IsNullOrEmpty(rawEmail))
            {
                await HttpContext.SignOutAsync("External");
                var noEmailUrl = BuildRedirectUrl(returnUrl, new[] { ("status", "error"), ("message", "no_email_from_provider") });
                return Redirect(noEmailUrl);
            }

            var signIn = await _sender.Send(new CompleteExternalCandidateSignInCommand(rawEmail, name));
            await HttpContext.SignOutAsync("External");

            if (signIn.IsFailure)
            {
                var disabledUrl = BuildRedirectUrl(returnUrl, new[] { ("status", "error"), ("message", "account_disabled") });
                return Redirect(disabledUrl);
            }

            var tokens = signIn.Value;
            var redirectUrl = BuildRedirectUrl(returnUrl, fragment: $"access_token={Uri.EscapeDataString(tokens.AccessToken)}&refresh_token={Uri.EscapeDataString(tokens.RefreshToken)}&role={Uri.EscapeDataString(tokens.Role)}");
            return Redirect(redirectUrl);
        }

        // ============================================================
        // REFRESH TOKEN ENDPOINTS
        // ============================================================

        /// <summary>
        /// REFRESH TOKEN CHO HR USER (Internal Staff)
        /// FE apiClient.ts:33 gọi endpoint này khi access token hết hạn (401)
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshTokenForUser([FromBody] RefreshTokenRequest request)
        {
            var result = await _sender.Send(new RefreshStaffTokenCommand(request.RefreshToken));
            if (result.IsFailure)
            {
                // Lỗi validation (thiếu token, không có ErrorCode) → 400; token sai/hết hạn → 401
                return result.ErrorCode is null
                    ? BadRequest(new { message = result.Error })
                    : Unauthorized(new { message = result.Error });
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// REFRESH TOKEN CHO CANDIDATE
        /// </summary>
        [HttpPost("candidate/refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshTokenForCandidate([FromBody] RefreshTokenRequest request)
        {
            var result = await _sender.Send(new RefreshCandidateTokenCommand(request.RefreshToken));
            if (result.IsFailure)
            {
                return result.ErrorCode is null
                    ? BadRequest(new { message = result.Error })
                    : Unauthorized(new { message = result.Error });
            }
            return Ok(result.Value);
        }

        // ============================================================
        // USER INFO & LOGOUT
        // ============================================================

        /// <summary>
        /// LẤY THÔNG TIN NGƯỜI DÙNG HIỆN TẠI TỪ JWT TOKEN
        /// FE authService.ts:207 gọi endpoint này
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst("email")?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst("role")?.Value
                ?? User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid token." });

            var result = await _sender.Send(new GetCurrentUserQuery(userId, email, role));
            return Ok(result.Value);
        }

        /// <summary>
        /// ĐĂNG XUẤT – REVOKE REFRESH TOKEN HIỆN TẠI
        /// FE authService.ts:183 gọi endpoint này
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest? request = null)
        {
            await _sender.Send(new LogoutCommand(request?.RefreshToken));
            return Ok(new { message = "Logged out successfully." });
        }

        // ============================================================
        // CANDIDATE REGISTRATION
        // ============================================================

        /// <summary>
        /// ĐĂNG KÝ TỰ DO DÀNH CHO ỨNG VIÊN (Candidate)
        /// </summary>
        [HttpPost("candidate/register")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterCandidate([FromBody] CandidateRegisterRequest request)
        {
            var result = await _sender.Send(new RegisterCandidateCommand(request.Email, request.Password, request.FullName, request.Phone));
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Đăng ký thành công. Vui lòng kiểm tra email để xác minh tài khoản." });
        }

        // ============================================================
        // CANDIDATE EMAIL VERIFICATION
        // ============================================================

        /// <summary>
        /// XÁC MINH EMAIL: ứng viên bấm link trong email → kích hoạt tài khoản (EmailVerified = true).
        /// </summary>
        [HttpGet("candidate/verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyCandidateEmail([FromQuery] string email, [FromQuery] string token)
        {
            var result = await _sender.Send(new VerifyCandidateEmailCommand(email, token));
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = result.Value });
        }

        /// <summary>
        /// GỬI LẠI EMAIL XÁC MINH cho ứng viên chưa kích hoạt tài khoản.
        /// Luôn trả Ok để tránh dò tìm email tồn tại.
        /// </summary>
        [HttpPost("candidate/resend-verification")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendCandidateVerification([FromBody] ForgotPasswordRequest request)
        {
            var result = await _sender.Send(new ResendCandidateVerificationCommand(request.Email));
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Nếu tài khoản tồn tại và chưa xác minh, email xác minh đã được gửi lại." });
        }

        // ============================================================
        // MAGIC LINK ENDPOINTS
        // ============================================================

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 3: XÁC THỰC PASSWORDLESS CHO CANDIDATE PORTAL QUA MAGIC LINK
        /// </summary>
        [HttpGet("magic-link/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyMagicLink([FromQuery] string email, [FromQuery] string token)
        {
            var result = await _sender.Send(new VerifyMagicLinkCommand(email, token));
            if (result.IsFailure)
            {
                return result.ErrorCode == AuthErrorCodes.NotFound
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });
            }

            return Ok(new
            {
                message = "Magic link authenticated successfully.",
                token = result.Value
            });
        }

        // ============================================================
        // PASSWORD RECOVERY ENDPOINTS
        // ============================================================

        /// <summary>
        /// API YÊU CẦU QUÊN MẬT KHẨU: Tạo token khôi phục và gửi qua hòm thư điện tử
        /// </summary>
        [HttpPost("candidate/forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> CandidateForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var result = await _sender.Send(new CandidateForgotPasswordCommand(request.Email));
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "If the email exists in our system, a reset link has been sent." });
        }

        /// <summary>
        /// API ĐẶT LẠI MẬT KHẨU: Xác thực token hợp lệ từ bảng MagicLinks và cập nhật mật khẩu mới
        /// </summary>
        [HttpPost("candidate/reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> CandidateResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _sender.Send(new CandidateResetPasswordCommand(request.Email, request.Token, request.NewPassword));
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Password has been reset successfully. You can now login with your new password." });
        }

        /// <summary>
        /// API QUÊN MẬT KHẨU DÀNH RIÊNG CHO STAFF NỘI BỘ (Super Admin / HR Admin / Recruiter).
        /// </summary>
        [HttpPost("staff/forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> StaffForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var result = await _sender.Send(new StaffForgotPasswordCommand(request.Email));
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "If the email exists in our system, a reset link has been sent." });
        }

        /// <summary>
        /// API ĐẶT LẠI MẬT KHẨU DÀNH RIÊNG CHO STAFF NỘI BỘ.
        /// </summary>
        [HttpPost("staff/reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> StaffResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _sender.Send(new StaffResetPasswordCommand(request.Email, request.Token, request.NewPassword));
            if (result.IsFailure)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Password has been reset successfully. You can now login with your new password." });
        }

        // ============================================================
        // PRIVATE HELPERS (HTTP/protocol concerns — ở lại controller)
        // ============================================================

        /// <summary>
        /// Kiểm tra một external auth scheme (vd "Google") đã được đăng ký chưa.
        /// Provider chỉ được đăng ký khi có credentials thật — tránh Challenge ném 500 khi tắt mềm ở Dev.
        /// </summary>
        private async Task<bool> IsExternalProviderAvailableAsync(string provider)
        {
            var schemeProvider = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
            return await schemeProvider.GetSchemeAsync(provider) is not null;
        }

        private string BuildRedirectUrl(string returnUrl, (string, string)[]? queryPairs = null, string? fragment = null)
        {
            var adminFrontend = _configuration["Authentication:AdminFrontendUrl"] ?? _configuration["Auth:AdminFrontendUrl"] ?? string.Empty;
            // ADR-046: OAuth callback có thể trả về Staff (AdminFrontendUrl) hoặc Candidate (CandidateBaseUrl) origin.
            var candidateFrontend = _configuration["Frontend:CandidateBaseUrl"] ?? string.Empty;

            string target = returnUrl;
            if (string.IsNullOrEmpty(target)) target = "/";

            bool isLocal = Url.IsLocalUrl(target);
            bool allowedExternal = false;
            if (!string.IsNullOrEmpty(target))
            {
                allowedExternal =
                    (!string.IsNullOrEmpty(adminFrontend) && target.StartsWith(adminFrontend, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(candidateFrontend) && target.StartsWith(candidateFrontend, StringComparison.OrdinalIgnoreCase));
            }

            if (!isLocal && !allowedExternal)
            {
                // Rơi về đây nghĩa là returnUrl không thuộc origin nào đã khai (cấu hình sai hoặc
                // bị chèn). Phải trả về ĐÚNG trang callback của staff chứ không phải gốc site:
                // `status`/`message` chỉ được đọc ở `/auth/callback`, ném vào `/` thì tham số rơi
                // vào hư không — người dùng thấy trang chủ kèm query lạ, không có báo lỗi nào.
                target = !string.IsNullOrEmpty(adminFrontend)
                    ? $"{adminFrontend.TrimEnd('/')}/auth/callback"
                    : "/auth/callback";
            }

            if (queryPairs != null && queryPairs.Length > 0)
            {
                var separator = target.Contains("?") ? "&" : "?";
                var qs = string.Join("&", queryPairs.Select(p => $"{Uri.EscapeDataString(p.Item1)}={Uri.EscapeDataString(p.Item2)}"));
                target = target + separator + qs;
            }

            if (!string.IsNullOrEmpty(fragment))
            {
                if (fragment.StartsWith("#")) fragment = fragment.Substring(1);
                target = target + "#" + fragment;
            }

            return target;
        }
    }
}
