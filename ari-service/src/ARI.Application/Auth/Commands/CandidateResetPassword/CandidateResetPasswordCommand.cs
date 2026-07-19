using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ARI.Application.Auth.Commands.CandidateResetPassword
{
    /// <summary>Đặt lại mật khẩu candidate: xác thực token MagicLink (Audience=candidate) rồi cập nhật hash mới.</summary>
    public record CandidateResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result>;

    public class CandidateResetPasswordCommandValidator : AbstractValidator<CandidateResetPasswordCommand>
    {
        public CandidateResetPasswordCommandValidator()
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Email) && !string.IsNullOrWhiteSpace(x.Token) && !string.IsNullOrWhiteSpace(x.NewPassword))
                .WithMessage("Missing required fields.");
        }
    }

    public class CandidateResetPasswordCommandHandler : IRequestHandler<CandidateResetPasswordCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public CandidateResetPasswordCommandHandler(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result> Handle(CandidateResetPasswordCommand request, CancellationToken ct)
        {
            // 1. Tìm ứng viên dựa theo email
            var normalizedEmail = AuthSupport.NormalizeEmail(request.Email);
            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email.ToLower() == normalizedEmail, ct);
            var candidate = candidates.FirstOrDefault();

            if (candidate == null)
                return Result.Failure("Invalid email or recovery token.");

            // Tìm token hợp lệ trong bảng MagicLinks (chỉ token thuộc cổng Candidate)
            var magicLinks = await _unitOfWork.Repository<MagicLink>().FindAsync(m =>
                m.Email.ToLower() == normalizedEmail
                && m.TokenHash == request.Token
                && m.Audience == MagicLinkAudience.Candidate
                && m.UsedAt == null
                && m.ExpiresAt > DateTimeOffset.UtcNow, ct);
            var magicLink = magicLinks.FirstOrDefault();

            if (magicLink == null)
                return Result.Failure("Invalid, expired, or already used recovery token.");

            if (!AuthSupport.IsStrongPassword(request.NewPassword, out var validationError))
                return Result.Failure(validationError);

            // 2. Cập nhật mật khẩu mới hóa mã BCrypt
            candidate.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            candidate.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(candidate);

            // 3. Đánh dấu token đã được sử dụng để tránh dùng lại (Tăng cường bảo mật)
            magicLink.UsedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<MagicLink>().Update(magicLink);

            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
