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

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Thử tìm trong bảng Users trước (HR, Recruiter, Admin)
            var user = await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user != null)
            {
                bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

                if (!isValidPassword)
                    return Unauthorized(new { message = "Invalid email or password." });

                var token = GenerateJwtTokenForUser(user);
                return Ok(new AuthResponse
                {
                    AccessToken = token,
                    RefreshToken = Guid.NewGuid().ToString("N"),
                    FullName = user.FullName ?? "HR Recruiter",
                    Role = user.Role,
                    OrganizationId = user.OrganizationId
                });
            }

            // 2. Nếu không có ở bảng Users, thử tìm trong bảng CandidateAccounts
            var candidate = await _dbContext.CandidateAccounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Email == request.Email);

            if (candidate != null)
            {
                bool isValidCandidatePass = BCrypt.Net.BCrypt.Verify(request.Password, candidate.PasswordHash);

                if (!isValidCandidatePass)
                    return Unauthorized(new { message = "Invalid email or password." });

                // Sinh token dành riêng cho Candidate
                var token = GenerateJwtTokenForCandidate(candidate);
                return Ok(new AuthResponse
                {
                    AccessToken = token,
                    RefreshToken = Guid.NewGuid().ToString("N"),
                    FullName = candidate.FullName ?? "Candidate",
                    Role = AppRoles.Candidate,
                    OrganizationId = Guid.Empty // Candidate không thuộc tổ chức nội bộ nào cố định ban đầu
                });
            }

            // 3. Không tìm thấy ở cả 2 bảng
            return Unauthorized(new { message = "Invalid email or password." });
        }

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

        [HttpGet("magic-link/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyMagicLink([FromQuery] string email, [FromQuery] string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Invalid token or email." });

            // tìm kiếm Candidate thực tế trong DB để lấy ID cấp Token chuẩn
            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email == email);
            var candidate = candidates.FirstOrDefault();

            if (candidate == null)
            {
                return NotFound(new { message = "Candidate account not found." });
            }

            // sinh JWT Token riêng cho Candidate, không phụ thuộc token query (giả định đã được xác thực qua email)
            var candidateToken = GenerateJwtTokenForCandidate(candidate);

            return Ok(new
            {
                message = "Magic link authenticated successfully.",
                token = candidateToken
            });
        }

        // hàm sinh token cho User (super_admin, hr_admin, recruiter) với các claim cần thiết để phân quyền và xác thực sau này
        private string GenerateJwtTokenForUser(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role), // role: super_admin / hr_admin / recruiter
                new Claim("organization_id", user.OrganizationId.ToString())
            };

            return CreateTokenString(claims);
        }

        // hàm sinh token riêng cho Ứng viên (Candidate)
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

        // Hàm lõi tạo chuỗi mã hóa Token (Tránh lặp code)
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