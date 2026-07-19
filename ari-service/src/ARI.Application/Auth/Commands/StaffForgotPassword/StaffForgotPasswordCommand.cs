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
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Auth.Commands.StaffForgotPassword
{
    /// <summary>
    /// Quên mật khẩu STAFF nội bộ — tra bảng Users, token Audience=staff.
    /// Tài khoản SSO-only vẫn được đặt mật khẩu lần đầu qua link này. Luôn Success (chống dò email).
    /// </summary>
    public record StaffForgotPasswordCommand(string Email) : IRequest<Result>;

    public class StaffForgotPasswordCommandValidator : AbstractValidator<StaffForgotPasswordCommand>
    {
        public StaffForgotPasswordCommandValidator()
        {
            RuleFor(x => x.Email)
                .Must(e => !string.IsNullOrWhiteSpace(e))
                .WithMessage("Email is required.");
        }
    }

    public class StaffForgotPasswordCommandHandler : IRequestHandler<StaffForgotPasswordCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IEmailQueue _emailQueue;

        public StaffForgotPasswordCommandHandler(IUnitOfWork unitOfWork, IConfiguration configuration, IEmailQueue emailQueue)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _emailQueue = emailQueue;
        }

        public async Task<Result> Handle(StaffForgotPasswordCommand request, CancellationToken ct)
        {
            var users = await _unitOfWork.Repository<User>().FindAsync(u => u.Email == request.Email, ct);
            var user = users.FirstOrDefault();

            // Bảo mật: Luôn trả Ok để tránh dò tìm email tồn tại. Chỉ gửi thư khi tài khoản hợp lệ & đang hoạt động.
            if (user != null && user.IsActive)
            {
                var resetToken = Guid.NewGuid().ToString("N");

                var magicLinkRecord = new MagicLink
                {
                    Id = Guid.NewGuid(),
                    Email = user.Email,
                    TokenHash = resetToken,
                    Audience = MagicLinkAudience.Staff,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(2), // Link có giá trị trong 2 giờ
                    CreatedAt = DateTimeOffset.UtcNow
                };

                await _unitOfWork.Repository<MagicLink>().AddAsync(magicLinkRecord, ct);
                await _unitOfWork.SaveChangesAsync();

                var frontendUrl = _configuration["Authentication:AdminFrontendUrl"] ?? _configuration["Auth:AdminFrontendUrl"] ?? "https://localhost:3000";
                var resetLink = $"{frontendUrl}/auth/reset-password?token={resetToken}&email={Uri.EscapeDataString(user.Email)}&audience={MagicLinkAudience.Staff}";

                var emailBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 5px;'>
                        <h2 style='color: #0056b3; text-align: center;'>ARISP Staff Account Password Reset</h2>
                        <p>Hi {user.FullName ?? "there"},</p>
                        <p>We received a request to reset the password for your ARISP internal account. Click the button below to set up a new password. This link is valid for 2 hours:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{resetLink}' style='background-color: #28a745; color: white; padding: 12px 25px; text-decoration: none; font-weight: bold; border-radius: 4px; display: inline-block;'>Reset Password</a>
                        </div>
                        <p>If the button doesn't work, you can also copy and paste the following link into your browser:</p>
                        <p style='word-break: break-all; color: #666;'>{resetLink}</p>
                        <hr style='border: none; border-top: 1px solid #eee;'/>
                        <p style='font-size: 12px; color: #999;'>If you did not request this change, please ignore this email or contact your administrator.</p>
                    </div>";

                // Gửi mail qua hàng đợi nền — không chặn response (tránh độ trễ SMTP 3–4s)
                _emailQueue.Enqueue(new EmailQueueItem(user.Email, "Reset Your ARISP Staff Account Password", emailBody));
            }

            return Result.Success();
        }
    }
}
