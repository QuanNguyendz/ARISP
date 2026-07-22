using FluentValidation;

namespace ARI.Application.Auth.Commands.RefreshCandidateToken
{
    public class RefreshCandidateTokenCommandValidator : AbstractValidator<RefreshCandidateTokenCommand>
    {
        public RefreshCandidateTokenCommandValidator()
        {
            RuleFor(x => x.RefreshToken)
                .Must(t => !string.IsNullOrWhiteSpace(t))
                .WithMessage("Refresh token is required.");
        }
    }
}
