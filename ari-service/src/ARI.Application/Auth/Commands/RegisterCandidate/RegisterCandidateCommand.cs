using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Auth.Commands.RegisterCandidate
{
    /// <summary>
    /// Đăng ký tự do cho ứng viên. KHÔNG dùng FluentValidation cho độ mạnh mật khẩu —
    /// giữ đúng thứ tự check gốc: email trùng TRƯỚC, độ mạnh mật khẩu SAU (body lỗi y hệt cũ).
    /// </summary>
    public record RegisterCandidateCommand(string Email, string Password, string FullName, string? Phone) : IRequest<Result>;

    public class RegisterCandidateCommandHandler : IRequestHandler<RegisterCandidateCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly IEmailQueue _emailQueue;

        public RegisterCandidateCommandHandler(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IConfiguration configuration,
            IEmailQueue emailQueue)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _emailQueue = emailQueue;
        }

        public async Task<Result> Handle(RegisterCandidateCommand request, CancellationToken ct)
        {
            var email = AuthSupport.NormalizeEmail(request.Email);
            var existing = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email.ToLower() == email, ct);
            if (existing.Any())
                return Result.Failure("Email already registered.");

            if (!AuthSupport.IsStrongPassword(request.Password, out var validationError))
                return Result.Failure(validationError);

            var account = new CandidateAccount
            {
                Email = email,
                PasswordHash = _passwordHasher.Hash(request.Password),
                FullName = request.FullName,
                Phone = request.Phone,
                EmailVerified = false
            };

            await _unitOfWork.Repository<CandidateAccount>().AddAsync(account, ct);
            await _unitOfWork.SaveChangesAsync();

            // Gửi email xác minh — ứng viên phải bấm link mới đăng nhập được
            await AuthSupport.SendCandidateVerificationEmailAsync(_unitOfWork, _configuration, _emailQueue, account, ct);

            return Result.Success();
        }
    }
}
