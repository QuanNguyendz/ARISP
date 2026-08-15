using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Commands.CandidateLogin
{
    /// <summary>Đăng nhập ứng viên bằng Email + Mật khẩu (cổng /jobs/login).</summary>
    public record CandidateLoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

    public class CandidateLoginCommandHandler : IRequestHandler<CandidateLoginCommand, Result<AuthResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;

        public CandidateLoginCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<AuthResponse>> Handle(CandidateLoginCommand request, CancellationToken ct)
        {
            var email = AuthSupport.NormalizeEmail(request.Email);
            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email.ToLower() == email, ct);
            var candidate = candidates.FirstOrDefault();

            if (candidate == null)
                return Result.Failure<AuthResponse>("Sai email hoặc mật khẩu.", AuthErrorCodes.InvalidCredentials);

            // Tài khoản CHƯA ĐẶT mật khẩu (tạo qua Google Sign-In nên chưa từng có mật khẩu nào).
            // Không phải "tài khoản khác": mỗi email chỉ có duy nhất một CandidateAccount, đăng nhập
            // Google và đăng nhập mật khẩu vào cùng một hồ sơ. Câu thông báo cũ ("Tài khoản này đăng ký
            // qua Google") khiến người dùng tưởng email của mình thuộc về một tài khoản riêng biệt, nên
            // nêu thẳng lối thoát: đăng nhập bằng Google, hoặc đặt mật khẩu qua "Quên mật khẩu".
            if (string.IsNullOrEmpty(candidate.PasswordHash))
                return Result.Failure<AuthResponse>(
                    "Tài khoản này chưa đặt mật khẩu vì bạn đăng ký bằng Google. Hãy đăng nhập bằng Google, "
                    + "hoặc dùng \"Quên mật khẩu?\" để đặt mật khẩu cho chính tài khoản này.",
                    AuthErrorCodes.PasswordlessGoogle);

            if (!_passwordHasher.Verify(request.Password, candidate.PasswordHash))
                return Result.Failure<AuthResponse>("Sai email hoặc mật khẩu.", AuthErrorCodes.InvalidCredentials);

            // Chặn đăng nhập đến khi ứng viên xác minh email (code để FE hiển thị nút gửi lại)
            if (!candidate.EmailVerified)
                return Result.Failure<AuthResponse>("Tài khoản chưa được xác minh. Vui lòng kiểm tra email để kích hoạt.", AuthErrorCodes.EmailNotVerified);

            // Cập nhật thời gian đăng nhập cuối
            candidate.LastLoginAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(candidate);
            await _unitOfWork.SaveChangesAsync();

            var accessToken = _tokenService.CreateCandidateToken(candidate);
            var refreshToken = await AuthSupport.IssueRefreshTokenForCandidateAsync(_unitOfWork, candidate.Id, ct);

            return Result.Success(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                FullName = candidate.FullName ?? "Candidate",
                Role = AppRoles.Candidate
            });
        }
    }
}
