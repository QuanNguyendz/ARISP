using FluentValidation;

namespace ARI.Application.Auth.Commands.VerifyCandidateEmail
{
    public class VerifyCandidateEmailCommandValidator : AbstractValidator<VerifyCandidateEmailCommand>
    {
        public VerifyCandidateEmailCommandValidator()
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Email) && !string.IsNullOrWhiteSpace(x.Token))
                .WithMessage("Thiếu email hoặc token xác minh.");
        }
    }
}
