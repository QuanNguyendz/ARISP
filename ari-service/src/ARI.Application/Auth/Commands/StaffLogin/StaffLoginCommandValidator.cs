using FluentValidation;

namespace ARI.Application.Auth.Commands.StaffLogin
{
    /// <summary>Message giữ verbatim từ guard inline cũ của AuthController (một message chung cho cả 2 field).</summary>
    public class StaffLoginCommandValidator : AbstractValidator<StaffLoginCommand>
    {
        public StaffLoginCommandValidator()
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Email) && !string.IsNullOrWhiteSpace(x.Password))
                .WithMessage("Email và mật khẩu là bắt buộc.");
        }
    }
}
