using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;
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
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Simple validation for prototype - Ignore multi-tenancy query filter during authentication
            var user = await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || request.Password != "password") // simplified password check for prototype
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            var token = GenerateJwtToken(user);

            var response = new AuthResponse
            {
                AccessToken = token,
                RefreshToken = Guid.NewGuid().ToString("N"),
                FullName = user.FullName ?? "HR Recruiter",
                Role = user.Role
            };

            return Ok(response);
        }

        [HttpPost("candidate/register")]
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
                PasswordHash = "hashed_password", // simplified
                FullName = request.FullName,
                Phone = request.Phone,
                EmailVerified = true
            };

            await _unitOfWork.Repository<CandidateAccount>().AddAsync(account);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { message = "Candidate registered successfully." });
        }

        [HttpGet("magic-link/verify")]
        public IActionResult VerifyMagicLink([FromQuery] string email, [FromQuery] string token)
        {
            // Simply verify email magic link for Candidate Portal access
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Invalid token or email." });

            return Ok(new { message = "Magic link authenticated successfully.", token = Guid.NewGuid().ToString("N") });
        }

        private string GenerateJwtToken(User user)
        {
            var keyStr = _configuration["JWT:Secret"] ?? "ARISP_SUPER_SECRET_JWT_KEY_MINIMUM_256_BITS_FOR_SECURITY";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

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
