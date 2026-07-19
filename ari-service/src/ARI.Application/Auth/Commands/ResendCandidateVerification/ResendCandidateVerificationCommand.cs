using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace ARI.Application.Auth.Commands.ResendCandidateVerification
{
    /// <summary>Gửi lại email xác minh — luôn Success để tránh dò tìm email tồn tại.</summary>
    public record ResendCandidateVerificationCommand(string Email) : IRequest<Result>;

    public class ResendCandidateVerificationCommandValidator : AbstractValidator<ResendCandidateVerificationCommand>
    {
        public ResendCandidateVerificationCommandValidator()
        {
            RuleFor(x => x.Email)
                .Must(e => !string.IsNullOrWhiteSpace(e))
                .WithMessage("Email là bắt buộc.");
        }
    }

    public class ResendCandidateVerificationCommandHandler : IRequestHandler<ResendCandidateVerificationCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IEmailQueue _emailQueue;

        public ResendCandidateVerificationCommandHandler(IUnitOfWork unitOfWork, IConfiguration configuration, IEmailQueue emailQueue)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _emailQueue = emailQueue;
        }

        public async Task<Result> Handle(ResendCandidateVerificationCommand request, CancellationToken ct)
        {
            var normalizedEmail = AuthSupport.NormalizeEmail(request.Email);
            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email.ToLower() == normalizedEmail, ct);
            var candidate = candidates.FirstOrDefault();

            // Chỉ gửi khi tài khoản tồn tại & chưa xác minh
            if (candidate != null && !candidate.EmailVerified)
            {
                await AuthSupport.SendCandidateVerificationEmailAsync(_unitOfWork, _configuration, _emailQueue, candidate, ct);
            }

            return Result.Success();
        }
    }
}
