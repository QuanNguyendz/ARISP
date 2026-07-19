using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Auth.Commands.CompleteExternalStaffSignIn
{
    /// <summary>Token trả cho FE sau khi hoàn tất OAuth callback (đưa vào URL fragment).</summary>
    public record ExternalSignInTokens(string AccessToken, string RefreshToken, string Role);

    /// <summary>
    /// Đuôi nghiệp vụ của Google OAuth callback cho STAFF: validate domain (system_settings ưu tiên,
    /// fallback appsettings), tra cứu tài khoản pre-provisioned (KHÔNG JIT tạo mới), mint token.
    /// Phần protocol (Challenge/AuthenticateAsync/SignOut/Redirect) ở lại controller.
    /// </summary>
    public record CompleteExternalStaffSignInCommand(string Email) : IRequest<Result<ExternalSignInTokens>>;

    public class CompleteExternalStaffSignInCommandHandler
        : IRequestHandler<CompleteExternalStaffSignInCommand, Result<ExternalSignInTokens>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;

        public CompleteExternalStaffSignInCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _configuration = configuration;
        }

        public async Task<Result<ExternalSignInTokens>> Handle(CompleteExternalStaffSignInCommand request, CancellationToken ct)
        {
            var email = request.Email;

            // Ưu tiên danh sách miền do Super Admin cấu hình qua UI (bảng system_settings),
            // fallback sang appsettings/env nếu DB chưa được set.
            var dbDomainSetting = (await _unitOfWork.Repository<SystemSetting>()
                .FindAsync(s => s.Key == "allowed_email_domains", ct)).FirstOrDefault();
            var allowed = !string.IsNullOrWhiteSpace(dbDomainSetting?.Value)
                ? dbDomainSetting!.Value
                : (_configuration["Authentication:AllowedDomains"] ?? _configuration["Auth:AllowedDomains"] ?? string.Empty);
            var allowedDomains = allowed
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().TrimStart('@').ToLower())
                .Where(s => s.Length > 0)
                .ToList();
            var emailDomain = email.Split('@').ElementAtOrDefault(1)?.ToLower() ?? string.Empty;

            var isDomainAllowed = !allowedDomains.Any() || allowedDomains.Contains(emailDomain);
            if (!isDomainAllowed)
                return Result.Failure<ExternalSignInTokens>("Email domain is not allowed.", AuthErrorCodes.DomainNotAllowed);

            var users = await _unitOfWork.Repository<User>().FindAsync(u => u.Email == email, ct);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                // Tài khoản chưa được Super Admin cấp phát → CHẶN đăng nhập, KHÔNG tự động tạo tài khoản
                return Result.Failure<ExternalSignInTokens>("Account not provisioned.", AuthErrorCodes.NotProvisioned);
            }

            if (!user.IsActive || user.Role == "Pending")
                return Result.Failure<ExternalSignInTokens>("Account pending approval.", AuthErrorCodes.PendingApproval);

            user.LastLoginAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.SaveChangesAsync();

            var token = _tokenService.CreateStaffToken(user);
            var refreshToken = await AuthSupport.IssueRefreshTokenForUserAsync(_unitOfWork, user.Id, ct);

            return Result.Success(new ExternalSignInTokens(token, refreshToken, user.Role));
        }
    }
}
