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
/// Đăng ký tự do ứng viên (<see cref="RegisterCandidateCommandHandler"/>, test-plan B13): email trùng kiểm
/// TRƯỚC độ mạnh mật khẩu, lưu account (email lower, chưa xác minh, mật khẩu đã hash) rồi phát magic link
/// xác minh (Audience candidate_verify, TTL ~24h) + 1 email.
/// </summary>
public class RegisterCandidateCommandHandlerTests
{
    private static RegisterCandidateCommandHandler Handler(
        InMemoryUnitOfWork uow, FakePasswordHasher hasher, RecordingEmailQueue email)
        => new(uow, hasher, AuthData.EmptyConfig(), email);

    [Fact]
    public async Task Duplicate_email_is_rejected_without_saving_or_emailing()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: "taken@example.io"));
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, new FakePasswordHasher(), email)
            .Handle(new RegisterCandidateCommand("taken@example.io", "Strong1!", "A", null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Email already registered", res.Error);
        Assert.Single(uow.Repo<CandidateAccount>().Items); // vẫn chỉ có account cũ
        Assert.Empty(email.Items);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Theory]
    [InlineData("short1!", "ít nhất 8")]        // < 8 ký tự
    [InlineData("nouppercase1!", "chữ hoa")]    // thiếu chữ hoa
    [InlineData("NoDigits!!", "chữ số")]        // thiếu chữ số
    [InlineData("NoSpecial1", "ký tự đặc biệt")] // thiếu ký tự đặc biệt
    public async Task Weak_password_is_rejected_with_specific_message(string password, string fragment)
    {
        var uow = new InMemoryUnitOfWork();
        var email = new RecordingEmailQueue();

        var res = await Handler(uow, new FakePasswordHasher(), email)
            .Handle(new RegisterCandidateCommand("free@example.io", password, "A", null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains(fragment, res.Error);
        Assert.Empty(uow.Repo<CandidateAccount>().Items);
        Assert.Empty(email.Items);
    }

    [Fact]
    public async Task Valid_registration_saves_account_and_sends_verification_email()
    {
        var uow = new InMemoryUnitOfWork();
        var hasher = new FakePasswordHasher();
        var email = new RecordingEmailQueue();
        var before = DateTimeOffset.UtcNow;

        var res = await Handler(uow, hasher, email)
            .Handle(new RegisterCandidateCommand("  New@X.io ", "Strong1!", "New User", "090"), CancellationToken.None);

        Assert.True(res.IsSuccess);

        var acc = Assert.Single(uow.Repo<CandidateAccount>().Items);
        Assert.Equal("new@x.io", acc.Email);          // normalize lower + trim
        Assert.False(acc.EmailVerified);
        Assert.Equal("hashed:Strong1!", acc.PasswordHash);

        var link = Assert.Single(uow.Repo<MagicLink>().Items);
        Assert.Equal(MagicLinkAudience.CandidateEmailVerify, link.Audience);
        Assert.Equal("new@x.io", link.Email);
        Assert.InRange(link.ExpiresAt, before.AddHours(24).AddMinutes(-1), before.AddHours(24).AddMinutes(1));

        var mail = Assert.Single(email.Items);
        Assert.Equal("new@x.io", mail.ToEmail);
    }

    [Fact]
    public async Task Duplicate_email_wins_over_weak_password()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: "taken@example.io"));
        var email = new RecordingEmailQueue();

        // Mật khẩu yếu NHƯNG email trùng → phải trả lỗi trùng (check email trước strength).
        var res = await Handler(uow, new FakePasswordHasher(), email)
            .Handle(new RegisterCandidateCommand("taken@example.io", "weak", "A", null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Email already registered", res.Error);
    }
}
