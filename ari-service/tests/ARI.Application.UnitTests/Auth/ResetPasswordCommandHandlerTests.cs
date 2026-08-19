using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.CandidateResetPassword;
using ARI.Application.Auth.Commands.StaffResetPassword;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đặt lại mật khẩu CANDIDATE (<see cref="CandidateResetPasswordCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "CandidateResetPassword" (UTCID01–14): tồn tại tài khoản, token hợp lệ (audience/expiry/used), độ mạnh
/// mật khẩu, happy path (đổi hash + tiêu token), chuẩn hoá email, và lỗi phụ thuộc.
/// </summary>
public class CandidateResetPasswordCommandHandlerTests
{
    private const string Email = "candidate@example.com";
    private const string Token = "valid-token";
    private const string StrongPassword = "Password@123";

    private static CandidateResetPasswordCommandHandler Handler(InMemoryUnitOfWork uow, FakePasswordHasher hasher) => new(uow, hasher);

    private static MagicLink Link(string email = Email, string token = Token, string audience = MagicLinkAudience.Candidate,
        DateTimeOffset? expiresAt = null, DateTimeOffset? usedAt = null)
        => AuthData.VerifyLink(email, token, audience, expiresAt, usedAt);

    private static CandidateResetPasswordCommand Cmd(string password = StrongPassword, string email = Email, string token = Token)
        => new(email, token, password);

    // UTCID01 — candidate không tồn tại → "Invalid email or recovery token."
    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid email or recovery token.", res.Error);
    }

    // UTCID02 — không có token khớp → "Invalid, expired, or already used recovery token."
    [Fact]
    public async Task UTCID02_No_matching_token()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid, expired, or already used recovery token.", res.Error);
    }

    // UTCID03 — token hết hạn
    [Fact]
    public async Task UTCID03_Expired_token()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email))
            .Seed(Link(expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid, expired, or already used recovery token.", res.Error);
    }

    // UTCID04 — token đã dùng
    [Fact]
    public async Task UTCID04_Used_token()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email))
            .Seed(Link(usedAt: DateTimeOffset.UtcNow));
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid, expired, or already used recovery token.", res.Error);
    }

    // UTCID05 — token sai audience (staff) → không khớp cổng candidate
    [Fact]
    public async Task UTCID05_Wrong_audience()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email))
            .Seed(Link(audience: MagicLinkAudience.Staff));
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid, expired, or already used recovery token.", res.Error);
    }

    // UTCID06 — mật khẩu < 8 ký tự
    [Fact]
    public async Task UTCID06_Password_too_short()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(password: "Pass@1"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Mật khẩu phải có ít nhất 8 ký tự.", res.Error);
    }

    // UTCID07 — mật khẩu không có chữ hoa
    [Fact]
    public async Task UTCID07_Password_no_uppercase()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(password: "password@123"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Mật khẩu phải chứa ít nhất một chữ hoa.", res.Error);
    }

    // UTCID08 — mật khẩu không có chữ số
    [Fact]
    public async Task UTCID08_Password_no_digit()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(password: "Password@abc"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Mật khẩu phải chứa ít nhất một chữ số.", res.Error);
    }

    // UTCID09 — mật khẩu không có ký tự đặc biệt
    [Fact]
    public async Task UTCID09_Password_no_special()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(password: "Password123"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Mật khẩu phải chứa ít nhất một ký tự đặc biệt trong !@#$%^&*.", res.Error);
    }

    // UTCID10 — hợp lệ → Success, đổi hash + tiêu token
    [Fact]
    public async Task UTCID10_Valid_reset()
    {
        var candidate = AuthData.Candidate(email: Email);
        var link = Link();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(link);

        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hashed:" + StrongPassword, candidate.PasswordHash);
        Assert.NotNull(link.UsedAt);
    }

    // UTCID11 — email cần chuẩn hoá → vẫn thành công
    [Fact]
    public async Task UTCID11_Email_normalized()
    {
        var candidate = AuthData.Candidate(email: Email);
        var link = Link();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(link);

        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(email: " CANDIDATE@EXAMPLE.COM "), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(link.UsedAt);
    }

    // UTCID12 — candidate repo ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID12_Candidate_repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateAccount>("DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID13 — password hasher ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID13_Hasher_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email)).Seed(Link());
        var hasher = new FakePasswordHasher { HashThrows = new Exception("Hash Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, hasher).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Hash Error", ex.Message);
    }

    // UTCID14 — SaveChangesAsync ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID14_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email)).Seed(Link()).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Đặt lại mật khẩu STAFF (<see cref="StaffResetPasswordCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "StaffResetPassword" (UTCID01–15): tồn tại + active, token hợp lệ (audience/expiry/used), độ mạnh mật khẩu,
/// happy path, và lỗi phụ thuộc (user lookup / magiclink lookup / hasher / save).
/// </summary>
public class StaffResetPasswordCommandHandlerTests
{
    private const string Email = "staff@example.com";
    private const string Token = "valid-token";
    private const string StrongPassword = "Password@123";

    private static StaffResetPasswordCommandHandler Handler(InMemoryUnitOfWork uow, FakePasswordHasher hasher) => new(uow, hasher);

    private static MagicLink Link(string audience = MagicLinkAudience.Staff, DateTimeOffset? expiresAt = null, DateTimeOffset? usedAt = null)
        => AuthData.VerifyLink(Email, Token, audience, expiresAt, usedAt);

    private static StaffResetPasswordCommand Cmd(string password = StrongPassword) => new(Email, Token, password);

    // UTCID01 — staff không tồn tại
    [Fact]
    public async Task UTCID01_Unknown_staff()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid email or recovery token.", res.Error);
    }

    // UTCID02 — staff inactive
    [Fact]
    public async Task UTCID02_Inactive_staff()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: false)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid email or recovery token.", res.Error);
    }

    // UTCID03 — không có MagicLink
    [Fact]
    public async Task UTCID03_No_matching_token()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true));
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid, expired, or already used recovery token.", res.Error);
    }

    // UTCID04 — MagicLink hết hạn
    [Fact]
    public async Task UTCID04_Expired_token()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link(expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid, expired, or already used recovery token.", res.Error);
    }

    // UTCID05 — MagicLink đã dùng
    [Fact]
    public async Task UTCID05_Used_token()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link(usedAt: DateTimeOffset.UtcNow));
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid, expired, or already used recovery token.", res.Error);
    }

    // UTCID06 — MagicLink sai audience (candidate)
    [Fact]
    public async Task UTCID06_Wrong_audience()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link(audience: MagicLinkAudience.Candidate));
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Invalid, expired, or already used recovery token.", res.Error);
    }

    // UTCID07 — mật khẩu < 8 ký tự
    [Fact]
    public async Task UTCID07_Password_too_short()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(password: "Pass@1"), CancellationToken.None);
        Assert.Equal("Mật khẩu phải có ít nhất 8 ký tự.", res.Error);
    }

    // UTCID08 — không có chữ hoa
    [Fact]
    public async Task UTCID08_Password_no_uppercase()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(password: "password@123"), CancellationToken.None);
        Assert.Equal("Mật khẩu phải chứa ít nhất một chữ hoa.", res.Error);
    }

    // UTCID09 — không có chữ số
    [Fact]
    public async Task UTCID09_Password_no_digit()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(password: "Password@abc"), CancellationToken.None);
        Assert.Equal("Mật khẩu phải chứa ít nhất một chữ số.", res.Error);
    }

    // UTCID10 — không có ký tự đặc biệt
    [Fact]
    public async Task UTCID10_Password_no_special()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link());
        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(password: "Password123"), CancellationToken.None);
        Assert.Equal("Mật khẩu phải chứa ít nhất một ký tự đặc biệt trong !@#$%^&*.", res.Error);
    }

    // UTCID11 — hợp lệ → Success, đổi hash + tiêu token
    [Fact]
    public async Task UTCID11_Valid_reset()
    {
        var user = AuthData.Staff(email: Email, active: true);
        var link = Link();
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(link);

        var res = await Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hashed:" + StrongPassword, user.PasswordHash);
        Assert.NotNull(link.UsedAt);
    }

    // UTCID12 — user lookup ném lỗi
    [Fact]
    public async Task UTCID12_User_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<User>("User DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("User DB Error", ex.Message);
    }

    // UTCID13 — MagicLink lookup ném lỗi
    [Fact]
    public async Task UTCID13_MagicLink_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).FailFindFor<MagicLink>("MagicLink DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("MagicLink DB Error", ex.Message);
    }

    // UTCID14 — password hasher ném lỗi
    [Fact]
    public async Task UTCID14_Hasher_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link());
        var hasher = new FakePasswordHasher { HashThrows = new Exception("Hash Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, hasher).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Hash Error", ex.Message);
    }

    // UTCID15 — SaveChangesAsync ném lỗi
    [Fact]
    public async Task UTCID15_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: Email, active: true)).Seed(Link()).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }
}
