using ARI.Application.Auth.Commands.StaffLogin;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

public class StaffLoginCommandValidatorTests
{
    private readonly StaffLoginCommandValidator _validator = new();

    [Theory]
    [InlineData("", "secret")]
    [InlineData("a@b.c", "")]
    [InlineData(" ", " ")]
    public void Missing_email_or_password_fails_with_original_message(string email, string password)
    {
        var result = _validator.Validate(new StaffLoginCommand(email, password));

        Assert.False(result.IsValid);
        // Message giữ verbatim từ guard inline cũ của AuthController
        Assert.Equal("Email và mật khẩu là bắt buộc.", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Valid_credentials_pass()
    {
        var result = _validator.Validate(new StaffLoginCommand("a@b.c", "secret"));
        Assert.True(result.IsValid);
    }
}
