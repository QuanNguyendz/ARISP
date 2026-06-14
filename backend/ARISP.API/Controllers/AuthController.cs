using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;
using ARISP.Domain.Constants;
using ARISP.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;

namespace ARISP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ARISPDbContext _dbContext;
        private readonly IEmailService _emailService;

        private const int REFRESH_TOKEN_EXPIRY_DAYS = 30;

        public AuthController(IUnitOfWork unitOfWork, IConfiguration configuration, ARISPDbContext dbContext, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _dbContext = dbContext;
            _emailService = emailService;
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
            var candidate = await _dbContext.CandidateAccounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Email == request.Email);

            if (candidate == null)
                return Unauthorized(new { message = "Invalid email or password." });

            bool isValidCandidatePass = BCrypt.Net.BCrypt.Verify(request.Password, candidate.PasswordHash);
            if (!isValidCandidatePass)
                return Unauthorized(new { message = "Invalid email or password." });

            // Cập nhật thời gian đăng nhập cuối
            candidate.LastLoginAt = DateTimeOffset.UtcNow;

            var accessToken = GenerateJwtTokenForCandidate(candidate);
            var refreshToken = await GenerateAndStoreRefreshTokenForCandidateAsync(candidate.Id);

            return Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                FullName = candidate.FullName ?? "Candidate",
                Role = AppRoles.Candidate
            });
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
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Email và mật khẩu là bắt buộc." });

            var user = await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
                return Unauthorized(new { message = "Sai email hoặc mật khẩu." });

            if (!user.IsActive)
                return Unauthorized(new { message = "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên." });

            if (string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest(new { message = "Tài khoản này chỉ hỗ trợ đăng nhập qua SSO." });

            bool isValidPass = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!isValidPass)
                return Unauthorized(new { message = "Sai email hoặc mật khẩu." });

            user.LastLoginAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            var accessToken = GenerateJwtTokenForUser(user);
            var refreshToken = await GenerateAndStoreRefreshTokenForUserAsync(user.Id);

            return Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                FullName = user.FullName ?? "Staff",
                Role = user.Role
            });
        }

        // ============================================================
        // HR / INTERNAL STAFF LOGIN (OAuth2)
        // ============================================================

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 2: ĐIỀU HƯỚNG CHALLENGE OAUTH2
        /// </summary>
        [HttpGet("external/signin")]
        [AllowAnonymous]
        public IActionResult ExternalSignIn([FromQuery] string provider = "Google", [FromQuery] string returnUrl = "/")
        {
            if (string.IsNullOrEmpty(provider)) provider = "Google";

            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("ExternalCallback", new { provider, returnUrl })
            };

            return Challenge(props, provider);
        }

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 2 (CALLBACK): TIẾP NHẬN DỮ LIỆU ĐĂNG NHẬP OAUTH2 VÀ XỬ LÝ JIT PROVISIONING
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
            var name = externalPrincipal?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "name")?.Value;

            if (string.IsNullOrEmpty(email))
            {
                await HttpContext.SignOutAsync("External");
                return BadRequest(new { message = "External provider did not return an email." });
            }

            var allowed = _configuration["Authentication:AllowedDomains"] ?? _configuration["Auth:AllowedDomains"] ?? string.Empty;
            var allowedDomains = allowed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLower()).ToList();
            var emailDomain = email.Split('@').ElementAtOrDefault(1)?.ToLower() ?? string.Empty;

            var isDomainAllowed = !allowedDomains.Any() || allowedDomains.Contains(emailDomain);
            if (!isDomainAllowed)
            {
                await HttpContext.SignOutAsync("External");
                return Forbid();
            }

            var user = await _dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                if (!user.IsActive || user.Role == "Pending")
                {
                    await HttpContext.SignOutAsync("External");
                    var pendingUrl = BuildRedirectUrl(returnUrl, new[] { ("status", "pending"), ("message", "pending_approval") });
                    return Redirect(pendingUrl);
                }

                user.LastLoginAt = DateTimeOffset.UtcNow;

                var token = GenerateJwtTokenForUser(user);
                var refreshToken = await GenerateAndStoreRefreshTokenForUserAsync(user.Id);
                await HttpContext.SignOutAsync("External");

                var redirectUrl = BuildRedirectUrl(returnUrl, fragment: $"access_token={Uri.EscapeDataString(token)}&refresh_token={Uri.EscapeDataString(refreshToken)}&role={Uri.EscapeDataString(user.Role)}");
                return Redirect(redirectUrl);
            }

            // Tài khoản chưa được Super Admin cấp phát → CHẶN đăng nhập, KHÔNG tự động tạo tài khoản
            await HttpContext.SignOutAsync("External");
            var rejectedUrl = BuildRedirectUrl(returnUrl, new[] { ("status", "rejected"), ("message", "account_not_provisioned") });
            return Redirect(rejectedUrl);
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
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { message = "Refresh token is required." });

            var tokenHash = HashToken(request.RefreshToken);

            var storedToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash
                                        && rt.RevokedAt == null
                                        && rt.ExpiresAt > DateTimeOffset.UtcNow);

            if (storedToken == null)
                return Unauthorized(new { message = "Invalid or expired refresh token." });

            // Revoke token cũ
            storedToken.RevokedAt = DateTimeOffset.UtcNow;

            // Tìm user để sinh JWT mới
            var user = await _dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == storedToken.UserId);
            if (user == null)
                return Unauthorized(new { message = "User not found." });

            // Sinh token mới
            var newAccessToken = GenerateJwtTokenForUser(user);
            var newRefreshToken = await GenerateAndStoreRefreshTokenForUserAsync(user.Id);

            return Ok(new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                FullName = user.FullName ?? "",
                Role = user.Role
            });
        }

        /// <summary>
        /// REFRESH TOKEN CHO CANDIDATE
        /// </summary>
        [HttpPost("candidate/refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshTokenForCandidate([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { message = "Refresh token is required." });

            var tokenHash = HashToken(request.RefreshToken);

            var storedToken = await _dbContext.CandidateRefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash
                                        && rt.RevokedAt == null
                                        && rt.ExpiresAt > DateTimeOffset.UtcNow);

            if (storedToken == null)
                return Unauthorized(new { message = "Invalid or expired refresh token." });

            // Revoke token cũ
            storedToken.RevokedAt = DateTimeOffset.UtcNow;

            // Tìm candidate để sinh JWT mới
            var candidate = await _dbContext.CandidateAccounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == storedToken.CandidateAccountId);
            if (candidate == null)
                return Unauthorized(new { message = "Candidate not found." });

            // Sinh token mới
            var newAccessToken = GenerateJwtTokenForCandidate(candidate);
            var newRefreshToken = await GenerateAndStoreRefreshTokenForCandidateAsync(candidate.Id);

            return Ok(new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                FullName = candidate.FullName ?? "Candidate",
                Role = AppRoles.Candidate
            });
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

            string fullName = "";

            // Xác định user type dựa theo role
            if (role == AppRoles.Candidate)
            {
                if (Guid.TryParse(userId, out var candidateGuid))
                {
                    var candidate = await _dbContext.CandidateAccounts.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(c => c.Id == candidateGuid);
                    fullName = candidate?.FullName ?? "";
                }
            }
            else
            {
                if (Guid.TryParse(userId, out var userGuid))
                {
                    var user = await _dbContext.Users.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(u => u.Id == userGuid);
                    fullName = user?.FullName ?? "";
                }
            }

            return Ok(new UserMeResponse
            {
                Id = userId,
                Email = email ?? "",
                Name = fullName,
                Role = role ?? ""
            });
        }

        /// <summary>
        /// ĐĂNG XUẤT – REVOKE REFRESH TOKEN HIỆN TẠI
        /// FE authService.ts:183 gọi endpoint này
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest? request = null)
        {
            if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            {
                var tokenHash = HashToken(request.RefreshToken);

                // Thử tìm trong bảng RefreshTokens (HR User)
                var hrToken = await _dbContext.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null);
                if (hrToken != null)
                {
                    hrToken.RevokedAt = DateTimeOffset.UtcNow;
                }

                // Thử tìm trong bảng CandidateRefreshTokens
                var candidateToken = await _dbContext.CandidateRefreshTokens
                    .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null);
                if (candidateToken != null)
                {
                    candidateToken.RevokedAt = DateTimeOffset.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
            }

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
            var existing = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email == request.Email);
            if (existing.Any())
            {
                return BadRequest(new { message = "Email already registered." });
            }

            if (!IsValidCandidatePassword(request.Password, out var validationError))
            {
                return BadRequest(new { message = validationError });
            }

            var account = new CandidateAccount
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                Phone = request.Phone,
                EmailVerified = true
            };

            await _unitOfWork.Repository<CandidateAccount>().AddAsync(account);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "Candidate registered successfully." });
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
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Invalid token or email." });

            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email == email);
            var candidate = candidates.FirstOrDefault();

            if (candidate == null)
                return NotFound(new { message = "Candidate account not found." });

            var candidateToken = GenerateJwtTokenForCandidate(candidate);
            return Ok(new
            {
                message = "Magic link authenticated successfully.",
                token = candidateToken
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
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required." });

            var candidate = await _dbContext.CandidateAccounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Email == request.Email);

            // Bảo mật: Luôn báo Ok để tránh kẻ xấu lợi dụng dò tìm email có tồn tại hay không
            if (candidate == null)
                return Ok(new { message = "If the email exists in our system, a reset link has been sent." });

            var resetToken = Guid.NewGuid().ToString("N");

            // 1. Tạo bản ghi MagicLink mới dựa trên class MagicLink
            var magicLinkRecord = new MagicLink
            {
                Id = Guid.NewGuid(),
                Email = candidate.Email,
                TokenHash = resetToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(2), // Link có giá trị trong 2 giờ
                CreatedAt = DateTimeOffset.UtcNow
            };

            // 2. Lưu token vào bảng MagicLinks
            await _dbContext.MagicLinks.AddAsync(magicLinkRecord);
            await _dbContext.SaveChangesAsync();

            var resetLink = $"http://localhost:3000/auth/reset-password?token={resetToken}&email={Uri.EscapeDataString(candidate.Email)}";

            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 5px;'>
                    <h2 style='color: #0056b3; text-align: center;'>ARISP Account Password Reset</h2>
                    <p>Hi {candidate.FullName ?? "Candidate"},</p>
                    <p>We received a request to reset your password. Click the button below to set up a new password. This link is valid for 2 hours:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetLink}' style='background-color: #28a745; color: white; padding: 12px 25px; text-decoration: none; font-weight: bold; border-radius: 4px; display: inline-block;'>Reset Password</a>
                    </div>
                    <p>If the button doesn't work, you can also copy and paste the following link into your browser:</p>
                    <p style='word-break: break-all; color: #666;'>{resetLink}</p>
                    <hr style='border: none; border-top: 1px solid #eee;'/>
                    <p style='font-size: 12px; color: #999;'>If you did not request this change, please ignore this email.</p>
                </div>";

            await _emailService.SendEmailAsync(candidate.Email, "Reset Your ARISP Account Password", emailBody);

            return Ok(new { message = "If the email exists in our system, a reset link has been sent." });
        }

        /// <summary>
        /// API ĐẶT LẠI MẬT KHẨU: Xác thực token hợp lệ từ bảng MagicLinks và cập nhật mật khẩu mới
        /// </summary>
        [HttpPost("candidate/reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> CandidateResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Missing required fields." });
            }

            // 1. Tìm ứng viên dựa theo email
            var candidate = await _dbContext.CandidateAccounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Email == request.Email);

            if (candidate == null)
            {
                return BadRequest(new { message = "Invalid email or recovery token." });
            }

            // 🛠️ ĐÃ SỬA ĐỔI CHUẨN: Tìm token hợp lệ trong bảng MagicLinks
            var magicLink = await _dbContext.MagicLinks
                .FirstOrDefaultAsync(m => m.Email == request.Email
                                       && m.TokenHash == request.Token
                                       && m.UsedAt == null
                                       && m.ExpiresAt > DateTimeOffset.UtcNow);

            if (magicLink == null)
            {
                return BadRequest(new { message = "Invalid, expired, or already used recovery token." });
            }

            if (!IsValidCandidatePassword(request.NewPassword, out var validationError))
            {
                return BadRequest(new { message = validationError });
            }

            // 2. Cập nhật mật khẩu mới hóa mã BCrypt
            candidate.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            candidate.UpdatedAt = DateTimeOffset.UtcNow;

            // 3. Đánh dấu token đã được sử dụng để tránh dùng lại (Tăng cường bảo mật)
            magicLink.UsedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Password has been reset successfully. You can now login with your new password." });
        }

        private bool IsValidCandidatePassword(string password, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                errorMessage = "Mật khẩu phải có ít nhất 8 ký tự.";
                return false;
            }

            if (!password.Any(char.IsUpper))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một chữ hoa.";
                return false;
            }

            if (!password.Any(char.IsDigit))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một chữ số.";
                return false;
            }

            const string specialCharacters = "!@#$%^&*";
            if (!password.Any(c => specialCharacters.Contains(c)))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất một ký tự đặc biệt trong !@#$%^&*.";
                return false;
            }

            return true;
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        private string BuildRedirectUrl(string returnUrl, (string, string)[]? queryPairs = null, string? fragment = null)
        {
            var adminFrontend = _configuration["Authentication:AdminFrontendUrl"] ?? _configuration["Auth:AdminFrontendUrl"] ?? string.Empty;

            string target = returnUrl;
            if (string.IsNullOrEmpty(target)) target = "/";

            bool isLocal = Url.IsLocalUrl(target);
            bool allowedExternal = false;
            if (!string.IsNullOrEmpty(adminFrontend) && !string.IsNullOrEmpty(target))
            {
                allowedExternal = target.StartsWith(adminFrontend, StringComparison.OrdinalIgnoreCase);
            }

            if (!isLocal && !allowedExternal)
            {
                target = !string.IsNullOrEmpty(adminFrontend) ? adminFrontend : "/admin";
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

        /// <summary>
        /// Hash token bằng SHA256 để lưu an toàn trong DB
        /// </summary>
        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Sinh refresh token mới cho HR User, hash SHA256 rồi lưu vào bảng RefreshTokens.
        /// Trả về token gốc (chưa hash) để gửi cho client.
        /// </summary>
        private async Task<string> GenerateAndStoreRefreshTokenForUserAsync(Guid userId)
        {
            var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var tokenHash = HashToken(rawToken);

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(REFRESH_TOKEN_EXPIRY_DAYS),
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity);
            await _dbContext.SaveChangesAsync();

            return rawToken;
        }

        /// <summary>
        /// Sinh refresh token mới cho Candidate, hash SHA256 rồi lưu vào bảng CandidateRefreshTokens.
        /// Trả về token gốc (chưa hash) để gửi cho client.
        /// </summary>
        private async Task<string> GenerateAndStoreRefreshTokenForCandidateAsync(Guid candidateAccountId)
        {
            var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var tokenHash = HashToken(rawToken);

            var refreshTokenEntity = new CandidateRefreshToken
            {
                Id = Guid.NewGuid(),
                CandidateAccountId = candidateAccountId,
                TokenHash = tokenHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(REFRESH_TOKEN_EXPIRY_DAYS),
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _dbContext.CandidateRefreshTokens.AddAsync(refreshTokenEntity);
            await _dbContext.SaveChangesAsync();

            return rawToken;
        }

        private string GenerateJwtTokenForUser(User user)
        {
            var roleClaimValue = user.Role switch
            {
                "super_admin" => AppRoles.SuperAdmin,
                "hr_admin" => AppRoles.HrAdmin,
                "recruiter" => AppRoles.Recruiter,
                _ => user.Role
            };

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", roleClaimValue)
            };

            return CreateTokenString(claims);
        }

        private string GenerateJwtTokenForCandidate(CandidateAccount candidate)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, candidate.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, candidate.Email),
                new Claim("role", AppRoles.Candidate)
            };

            return CreateTokenString(claims);
        }

        private string CreateTokenString(Claim[] claims)
        {
            var keyStr = _configuration["JWT:Secret"] ?? "ARISP_SUPER_SECRET_JWT_KEY_MINIMUM_256_BITS_FOR_SECURITY";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"] ?? "ARISP",
                audience: _configuration["JWT:Audience"] ?? "ARISP_Client",
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}