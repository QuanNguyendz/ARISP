using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.RegisterCandidate;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đăng ký ứng viên (<see cref="RegisterCandidateCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "RegisterCandidate" (UTCID01–17): happy path (lưu account chưa xác minh + phát link xác minh + email),
/// chuẩn hoá email, trùng email (kiểm TRƯỚC độ mạnh mật khẩu), độ mạnh mật khẩu, và 7 case lỗi phụ thuộc.
/// </summary>
public class RegisterCandidateCommandHandlerTests
{
    private const string Email = "candidate@example.com";
    private const string Password = "Password@123";
    private const string FullName = "Candidate User";
    private const string Phone = "0901234567";

    private static RegisterCandidateCommandHandler Handler(InMemoryUnitOfWork uow, FakePasswordHasher hasher, RecordingEmailQueue email)
        => new(uow, hasher, AuthData.EmptyConfig(), email);

    private static RegisterCandidateCommand Cmd(string email = Email, string password = Password, string fullName = FullName, string? phone = Phone)
        => new(email, password, fullName, phone);

    // UTCID01 — hợp lệ → Success, lưu account chưa xác minh + MagicLink candidate_verify + email
    [Fact]
    public async Task UTCID01_Valid_registration()
    {
        var uow = new InMemoryUnitOfWork(); var email = new RecordingEmailQueue();
        var before = DateTimeOffset.UtcNow;

        var res = await Handler(uow, new FakePasswordHasher(), email).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var acc = Assert.Single(uow.Repo<CandidateAccount>().Items);
        Assert.Equal(Email, acc.Email);
        Assert.False(acc.EmailVerified);
        Assert.Equal("hashed:" + Password, acc.PasswordHash);
        Assert.Equal(Phone, acc.Phone);
        var link = Assert.Single(uow.Repo<MagicLink>().Items);
        Assert.Equal(MagicLinkAudience.CandidateEmailVerify, link.Audience);
        Assert.InRange(link.ExpiresAt, before.AddHours(24).AddMinutes(-1), before.AddHours(24).AddMinutes(1));
        Assert.Single(email.Items);
    }

    // UTCID02 — email cần chuẩn hoá + Phone null → Success
    [Fact]
    public async Task UTCID02_Email_normalized_phone_null()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue())
            .Handle(Cmd(email: " CANDIDATE@EXAMPLE.COM ", phone: null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var acc = Assert.Single(uow.Repo<CandidateAccount>().Items);
        Assert.Equal(Email, acc.Email);
        Assert.Null(acc.Phone);
    }

    // UTCID03 — email rỗng, không trùng → Success (handler không validate định dạng email)
    [Fact]
    public async Task UTCID03_Empty_email_still_succeeds()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue())
            .Handle(Cmd(email: "", phone: null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("", Assert.Single(uow.Repo<CandidateAccount>().Items).Email);
    }

    // UTCID04 — FullName toàn khoảng trắng → Success (handler không validate FullName)
    [Fact]
    public async Task UTCID04_Whitespace_full_name_still_succeeds()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue())
            .Handle(Cmd(fullName: " ", phone: null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<CandidateAccount>().Items);
    }

    // UTCID05 — email đã đăng ký → "Email already registered."
    [Fact]
    public async Task UTCID05_Duplicate_email()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, new FakePasswordHasher(), email).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email already registered.", res.Error);
        Assert.Single(uow.Repo<CandidateAccount>().Items);
        Assert.Empty(email.Items);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID06 — email trùng sau chuẩn hoá → "Email already registered."
    [Fact]
    public async Task UTCID06_Normalized_duplicate_email()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));

        var res = await Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue())
            .Handle(Cmd(email: " CANDIDATE@EXAMPLE.COM ", phone: null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Email already registered.", res.Error);
    }

    // UTCID07 — mật khẩu < 8 ký tự
    [Fact]
    public async Task UTCID07_Password_too_short()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher(), new RecordingEmailQueue())
            .Handle(Cmd(password: "Pass@1", phone: null), CancellationToken.None);
        Assert.Equal("Mật khẩu phải có ít nhất 8 ký tự.", res.Error);
    }

    // UTCID08 — không có chữ hoa
    [Fact]
    public async Task UTCID08_Password_no_uppercase()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher(), new RecordingEmailQueue())
            .Handle(Cmd(password: "password@123", phone: null), CancellationToken.None);
        Assert.Equal("Mật khẩu phải chứa ít nhất một chữ hoa.", res.Error);
    }

    // UTCID09 — không có chữ số
    [Fact]
    public async Task UTCID09_Password_no_digit()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher(), new RecordingEmailQueue())
            .Handle(Cmd(password: "Password@abc", phone: null), CancellationToken.None);
        Assert.Equal("Mật khẩu phải chứa ít nhất một chữ số.", res.Error);
    }

    // UTCID10 — không có ký tự đặc biệt
    [Fact]
    public async Task UTCID10_Password_no_special()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher(), new RecordingEmailQueue())
            .Handle(Cmd(password: "Password123", phone: null), CancellationToken.None);
        Assert.Equal("Mật khẩu phải chứa ít nhất một ký tự đặc biệt trong !@#$%^&*.", res.Error);
    }

    // UTCID11 — candidate lookup ném lỗi
    [Fact]
    public async Task UTCID11_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateAccount>("DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID12 — password hasher ném lỗi
    [Fact]
    public async Task UTCID12_Hasher_error()
    {
        var hasher = new FakePasswordHasher { HashThrows = new Exception("Hash Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(new InMemoryUnitOfWork(), hasher, new RecordingEmailQueue()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Hash Error", ex.Message);
    }

    // UTCID13 — candidate AddAsync ném lỗi
    [Fact]
    public async Task UTCID13_Account_add_error()
    {
        var uow = new InMemoryUnitOfWork().FailAddFor<CandidateAccount>("Account Add Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Account Add Error", ex.Message);
    }

    // UTCID14 — save account (lần save thứ 1) ném lỗi
    [Fact]
    public async Task UTCID14_Account_save_error()
    {
        var uow = new InMemoryUnitOfWork().FailSaveOn(1, "Account Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Account Save Error", ex.Message);
    }

    // UTCID15 — MagicLink AddAsync ném lỗi
    [Fact]
    public async Task UTCID15_MagicLink_add_error()
    {
        var uow = new InMemoryUnitOfWork().FailAddFor<MagicLink>("MagicLink Add Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("MagicLink Add Error", ex.Message);
    }

    // UTCID16 — save MagicLink (lần save thứ 2) ném lỗi
    [Fact]
    public async Task UTCID16_MagicLink_save_error()
    {
        var uow = new InMemoryUnitOfWork().FailSaveOn(2, "MagicLink Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakePasswordHasher(), new RecordingEmailQueue()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("MagicLink Save Error", ex.Message);
    }

    // UTCID17 — email queue ném lỗi
    [Fact]
    public async Task UTCID17_Email_queue_error()
    {
        var email = new RecordingEmailQueue { EnqueueThrows = new Exception("Queue Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(new InMemoryUnitOfWork(), new FakePasswordHasher(), email).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Queue Error", ex.Message);
    }
}
