using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Commands.StaffLogin
{
    /// <summary>Đăng nhập nội bộ (Super Admin / HR Admin / Recruiter) bằng Email + Mật khẩu pre-provisioned.</summary>
    public record StaffLoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

    public class StaffLoginCommandHandler : IRequestHandler<StaffLoginCommand, Result<AuthResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;

        public StaffLoginCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<AuthResponse>> Handle(StaffLoginCommand request, CancellationToken ct)
        {
            var users = await _unitOfWork.Repository<User>().FindAsync(u => u.Email == request.Email, ct);
            var user = users.FirstOrDefault();

            if (user == null)
                return Result.Failure<AuthResponse>("Sai email hoặc mật khẩu.", AuthErrorCodes.InvalidCredentials);

            if (!user.IsActive)
                return Result.Failure<AuthResponse>("Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.", AuthErrorCodes.AccountDisabled);

            if (string.IsNullOrEmpty(user.PasswordHash))
                return Result.Failure<AuthResponse>("Tài khoản này chỉ hỗ trợ đăng nhập qua SSO.", AuthErrorCodes.SsoOnly);

            if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
                return Result.Failure<AuthResponse>("Sai email hoặc mật khẩu.", AuthErrorCodes.InvalidCredentials);

            user.LastLoginAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);
            await _unitOfWork.SaveChangesAsync();

            var accessToken = _tokenService.CreateStaffToken(user);
            var refreshToken = await AuthSupport.IssueRefreshTokenForUserAsync(_unitOfWork, user.Id, ct);

            return Result.Success(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                FullName = user.FullName ?? "Staff",
                Role = user.Role
            });
        }
    }
}
