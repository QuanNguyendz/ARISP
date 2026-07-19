using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ARI.Application.Auth.Commands.VerifyMagicLink
{
    /// <summary>
    /// Xác thực passwordless cho Candidate Portal qua magic link. Success value = JWT candidate.
    /// (Giữ nguyên behavior cũ: chỉ tra cứu theo email — token không được đối chiếu ở endpoint này.)
    /// </summary>
    public record VerifyMagicLinkCommand(string Email, string Token) : IRequest<Result<string>>;

    public class VerifyMagicLinkCommandValidator : AbstractValidator<VerifyMagicLinkCommand>
    {
        public VerifyMagicLinkCommandValidator()
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrEmpty(x.Email) && !string.IsNullOrEmpty(x.Token))
                .WithMessage("Invalid token or email.");
        }
    }

    public class VerifyMagicLinkCommandHandler : IRequestHandler<VerifyMagicLinkCommand, Result<string>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;

        public VerifyMagicLinkCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        public async Task<Result<string>> Handle(VerifyMagicLinkCommand request, CancellationToken ct)
        {
            var normalizedEmail = AuthSupport.NormalizeEmail(request.Email);
            var candidates = await _unitOfWork.Repository<CandidateAccount>().FindAsync(c => c.Email.ToLower() == normalizedEmail, ct);
            var candidate = candidates.FirstOrDefault();

            if (candidate == null)
                return Result.Failure<string>("Candidate account not found.", AuthErrorCodes.NotFound);

            return Result.Success(_tokenService.CreateCandidateToken(candidate));
        }
    }
}
