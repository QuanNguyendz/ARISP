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

namespace ARI.Application.Auth.Commands.CandidateForgotPassword
{
    /// <summary>Tạo token khôi phục (TTL 2h) + gửi email. Luôn Success để tránh dò tìm email tồn tại.</summary>
    public record CandidateForgotPasswordCommand(string Email) : IRequest<Result>;

    public class CandidateForgotPasswordCommandValidator : AbstractValidator<CandidateForgotPasswordCommand>
    {
        public CandidateForgotPasswordCommandValidator()
        {
            RuleFor(x => x.Email)
                .Must(e => !string.IsNullOrWhiteSpace(e))
                .WithMessage("Email is required.");
        }
    }

    public class CandidateForgotPasswordCommandHandler : IRequestHandler<CandidateForgotPasswordCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IEmailQueue _emailQueue;

        public CandidateForgotPasswordCommandHandler(IUnitOfWork unitOfWork, IConfiguration configuration, IEmailQueue emailQueue)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _emailQueue = emailQueue;
        }

        public async Task<Result> Handle(CandidateForgotPasswordCommand request, CancellationToken ct)
        {
            var normalizedEmail = AuthSupport.NormalizeEmail(request.Email);
            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email.ToLower() == normalizedEmail, ct);
            var candidate = candidates.FirstOrDefault();

            // Bảo mật: Luôn báo Ok để tránh kẻ xấu lợi dụng dò tìm email có tồn tại hay không
            if (candidate == null)
                return Result.Success();

            var resetToken = Guid.NewGuid().ToString("N");

            // 1. Tạo bản ghi MagicLink mới dựa trên class MagicLink
            var magicLinkRecord = new MagicLink
            {
                Id = Guid.NewGuid(),
                Email = candidate.Email,
                TokenHash = resetToken,
                Audience = MagicLinkAudience.Candidate,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(2), // Link có giá trị trong 2 giờ
                CreatedAt = DateTimeOffset.UtcNow
            };

            // 2. Lưu token vào bảng MagicLinks
            await _unitOfWork.Repository<MagicLink>().AddAsync(magicLinkRecord, ct);
            await _unitOfWork.SaveChangesAsync();

            // ADR-046: link reset của candidate phải về Candidate site → ưu tiên CandidateBaseUrl.
            var frontendUrl = _configuration["Frontend:CandidateBaseUrl"] ?? _configuration["Authentication:AdminFrontendUrl"] ?? _configuration["Auth:AdminFrontendUrl"] ?? "https://localhost:3000";
            var resetLink = $"{frontendUrl}/auth/reset-password?token={resetToken}&email={Uri.EscapeDataString(candidate.Email)}&audience={MagicLinkAudience.Candidate}";

            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 5px;'>
                    <h2 style='color: #0056b3; text-align: center;'>ARISP Account Password Reset</h2>
                    <p>Hi {candidate.FullName ?? "Candidate"},</p>
                    <p>We received a request to reset your password. Click the button below to set up a new password. This link is valid for 2 hours:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetLink}' style='background-color: #28a745; color: white; padding: 12px 25px; text-decoration: none; font-weight: bold; border-radius: 4px; display: inline-block;'>Reset Password</a>
                    </div>
                    <p>If the button doesn't work, you can also copy and paste the following link into your browser:</p>
                    <p style='word-break: break-all; color: #666;'>{resetLink}</p>
                    <hr style='border: none; border-top: 1px solid #eee;'/>
                    <p style='font-size: 12px; color: #999;'>If you did not request this change, please ignore this email.</p>
                </div>";

            // Gửi mail qua hàng đợi nền — không chặn response (tránh độ trễ SMTP 3–4s)
            _emailQueue.Enqueue(new EmailQueueItem(candidate.Email, "Reset Your ARISP Account Password", emailBody));

            return Result.Success();
        }
    }
}
