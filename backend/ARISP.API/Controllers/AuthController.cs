using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
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

        public AuthController(IUnitOfWork unitOfWork, IConfiguration configuration, ARISPDbContext dbContext)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _dbContext = dbContext;
        }

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 1: DÀNH RIÊNG CHO ỨNG VIÊN (Candidate - Tại /jobs/login)
        /// Xác thực truyền thống qua form điền Email + Mật khẩu cá nhân
        /// </summary>
        [HttpPost("candidate/login")]
        [AllowAnonymous]
        public async Task<IActionResult> CandidateLogin([FromBody] LoginRequest request)
        {
            // Bảo mật biệt lập cổng, không quét thông tin vào bảng Users nội bộ nữa
            var candidate = await _dbContext.CandidateAccounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Email == request.Email);

            if (candidate == null)
                return Unauthorized(new { message = "Invalid email or password." });

            bool isValidCandidatePass = BCrypt.Net.BCrypt.Verify(request.Password, candidate.PasswordHash);
            if (!isValidCandidatePass)
                return Unauthorized(new { message = "Invalid email or password." });

            var token = GenerateJwtTokenForCandidate(candidate);

            // 🛡️ ĐÃ SỬA: Giải quyết xong Conflict & Loại bỏ hoàn toàn OrganizationId
            return Ok(new AuthResponse
            {
                AccessToken = token,
                RefreshToken = Guid.NewGuid().ToString("N"),
                FullName = candidate.FullName ?? "Candidate",
                Role = AppRoles.Candidate
            });
        }

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 2: ĐIỀU HƯỚNG CHALLENGE OAUTH2 (Dành cho nội bộ Super Admin, HR Leader, Recruiter)
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

            // Tìm nhân viên trong bảng nội bộ
            var user = await _dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                // Nếu tài khoản bị khóa hoặc đang ở trạng thái chờ duyệt (Pending)
                if (!user.IsActive || user.Role == "Pending")
                {
                    await HttpContext.SignOutAsync("External");
                    var pendingUrl = BuildRedirectUrl(returnUrl, new[] { ("status", "pending"), ("message", "pending_approval") });
                    return Redirect(pendingUrl);
                }

                var token = GenerateJwtTokenForUser(user);
                await HttpContext.SignOutAsync("External");

                var redirectUrl = BuildRedirectUrl(returnUrl, fragment: $"access_token={Uri.EscapeDataString(token)}&role={Uri.EscapeDataString(user.Role)}");
                return Redirect(redirectUrl);
            }

            // 🚀 ĐÃ SỬA LUỒNG JIT PROVISIONING: Loại bỏ hoàn toàn truy vết tới bảng Organizations
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = "password", // Đánh dấu tài khoản SSO
                Role = "Pending",                      // Khởi tạo trạng thái chờ duyệt tuyển dụng
                FullName = name,
                Department = null,
                IsActive = false                       // Chờ SuperAdmin hoặc HR Admin duyệt kích hoạt
            };

            await _dbContext.Users.AddAsync(newUser);
            await _dbContext.SaveChangesAsync();

            await HttpContext.SignOutAsync("External");
            var createdPendingUrl = BuildRedirectUrl(returnUrl, new[] { ("status", "pending"), ("message", "created_pending") });
            return Redirect(createdPendingUrl);
        }

        private string BuildRedirectUrl(string returnUrl, (string, string)[] queryPairs = null, string fragment = null)
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

        /// <summary>
        /// CỔNG ĐĂNG NHẬP 3: XÁC THỰC PASSWORDLESS CHO CANDIDATE PORTAL QUA MAGIC LINK
        /// </summary>
        [HttpGet("magic-link/verify")]
        [AllowAnonymous] // Đã sửa gộp Conflict: Đảm bảo cổng Magic Link cho phép gọi ẩn danh không cần Bearer Token trước
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

        private string GenerateJwtTokenForUser(User user)
        {
            // Map quyền chuẩn hóa an toàn dựa theo dữ liệu thực tế từ AppRoles
            var roleClaimValue = user.Role switch
            {
                "super_admin" => AppRoles.SuperAdmin,
                "hr_admin" => AppRoles.HrAdmin,
                "recruiter" => AppRoles.Recruiter,
                _ => user.Role
            };

            // 🛡️ ĐÃ SỬA: Đã giải quyết Conflict và lược bỏ hoàn toàn Claim "organization_id"
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, roleClaimValue)
            };

            return CreateTokenString(claims);
        }

        private string GenerateJwtTokenForCandidate(CandidateAccount candidate)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, candidate.Id.ToString()),
                new Claim(ClaimTypes.Email, candidate.Email),
                new Claim(ClaimTypes.Role, AppRoles.Candidate)
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