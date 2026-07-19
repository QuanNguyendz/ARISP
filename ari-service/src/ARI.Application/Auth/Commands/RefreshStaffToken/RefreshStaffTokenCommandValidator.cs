using FluentValidation;

namespace ARI.Application.Auth.Commands.RefreshStaffToken
{
    public class RefreshStaffTokenCommandValidator : AbstractValidator<RefreshStaffTokenCommand>
    {
        public RefreshStaffTokenCommandValidator()
        {
            RuleFor(x => x.RefreshToken)
                .Must(t => !string.IsNullOrWhiteSpace(t))
                .WithMessage("Refresh token is required.");
        }
    }
}
