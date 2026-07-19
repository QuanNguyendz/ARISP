using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ARI.Infrastructure.Identity
{
    /// <summary>
    /// Mint JWT — logic chuyển verbatim từ AuthController.CreateTokenString cũ:
    /// HS256, expiry 7 ngày, claims sub/email/name/role (short names, MapInboundClaims=false).
    /// </summary>
    public class JwtTokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreateStaffToken(User user)
        {
            // Map giá trị role legacy trong DB về AppRoles chuẩn trước khi phát hành claim.
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
                new Claim("name", user.FullName ?? string.Empty),
                new Claim("role", roleClaimValue)
            };

            return CreateTokenString(claims);
        }

        public string CreateCandidateToken(CandidateAccount candidate)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, candidate.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, candidate.Email),
                new Claim("name", candidate.FullName ?? string.Empty),
                new Claim("role", AppRoles.Candidate)
            };

            return CreateTokenString(claims);
        }

        private string CreateTokenString(Claim[] claims)
        {
            var keyStr = _configuration["JWT:Secret"] is { Length: > 0 } s ? s
                : Environment.GetEnvironmentVariable("JWT_SECRET") ?? string.Empty;
            if (string.IsNullOrEmpty(keyStr))
                throw new InvalidOperationException("JWT:Secret is not configured.");
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
