using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Auth.Commands.VerifyCandidateEmail
{
    /// <summary>
    /// Xác minh email ứng viên qua link. Success value = message hiển thị
    /// (phân biệt "đã xác minh trước đó" vs "xác minh thành công").
    /// </summary>
    public record VerifyCandidateEmailCommand(string Email, string Token) : IRequest<Result<string>>;

    public class VerifyCandidateEmailCommandHandler : IRequestHandler<VerifyCandidateEmailCommand, Result<string>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public VerifyCandidateEmailCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<string>> Handle(VerifyCandidateEmailCommand request, CancellationToken ct)
        {
            var normalizedEmail = AuthSupport.NormalizeEmail(request.Email);
            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email.ToLower() == normalizedEmail, ct);
            var candidate = candidates.FirstOrDefault();

            if (candidate == null)
                return Result.Failure<string>("Liên kết xác minh không hợp lệ.");

            if (candidate.EmailVerified)
                return Result.Success("Tài khoản đã được xác minh trước đó. Bạn có thể đăng nhập.");

            var magicLinks = await _unitOfWork.Repository<MagicLink>().FindAsync(m =>
                m.Email.ToLower() == normalizedEmail
                && m.TokenHash == request.Token
                && m.Audience == MagicLinkAudience.CandidateEmailVerify
                && m.UsedAt == null
                && m.ExpiresAt > DateTimeOffset.UtcNow, ct);
            var magicLink = magicLinks.FirstOrDefault();

            if (magicLink == null)
                return Result.Failure<string>("Liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu gửi lại.");

            candidate.EmailVerified = true;
            candidate.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<CandidateAccount>().Update(candidate);

            magicLink.UsedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<MagicLink>().Update(magicLink);

            await _unitOfWork.SaveChangesAsync();

            return Result.Success("Xác minh email thành công. Bạn có thể đăng nhập ngay bây giờ.");
        }
    }
}
