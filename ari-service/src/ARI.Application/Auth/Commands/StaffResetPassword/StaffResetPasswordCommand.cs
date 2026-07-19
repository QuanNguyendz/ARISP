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

namespace ARI.Application.Auth.Commands.StaffResetPassword
{
    /// <summary>Đặt lại mật khẩu STAFF: xác thực token Audience=staff rồi cập nhật bảng Users.</summary>
    public record StaffResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result>;

    public class StaffResetPasswordCommandValidator : AbstractValidator<StaffResetPasswordCommand>
    {
        public StaffResetPasswordCommandValidator()
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Email) && !string.IsNullOrWhiteSpace(x.Token) && !string.IsNullOrWhiteSpace(x.NewPassword))
                .WithMessage("Missing required fields.");
        }
    }

    public class StaffResetPasswordCommandHandler : IRequestHandler<StaffResetPasswordCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public StaffResetPasswordCommandHandler(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result> Handle(StaffResetPasswordCommand request, CancellationToken ct)
        {
            var users = await _unitOfWork.Repository<User>().FindAsync(u => u.Email == request.Email, ct);
            var user = users.FirstOrDefault();

            if (user == null || !user.IsActive)
                return Result.Failure("Invalid email or recovery token.");

            // Tìm token hợp lệ trong bảng MagicLinks (chỉ token thuộc cổng Staff)
            var magicLinks = await _unitOfWork.Repository<MagicLink>().FindAsync(m =>
                m.Email == request.Email
                && m.TokenHash == request.Token
                && m.Audience == MagicLinkAudience.Staff
                && m.UsedAt == null
                && m.ExpiresAt > DateTimeOffset.UtcNow, ct);
            var magicLink = magicLinks.FirstOrDefault();

            if (magicLink == null)
                return Result.Failure("Invalid, expired, or already used recovery token.");

            if (!AuthSupport.IsStrongPassword(request.NewPassword, out var validationError))
                return Result.Failure(validationError);

            user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<User>().Update(user);

            // Đánh dấu token đã dùng để chống tái sử dụng
            magicLink.UsedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<MagicLink>().Update(magicLink);

            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
