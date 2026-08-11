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
/// Đặt lại mật khẩu (test-plan B28): token hợp lệ → đổi hash + đánh dấu UsedAt (one-time); tài khoản thiếu →
/// 'Invalid email or recovery token.'; token sai/hết hạn/đã dùng → 'Invalid, expired, or already used...';
/// mật khẩu yếu → thông điệp IsStrongPassword. Staff dùng Audience Staff, Candidate dùng Audience Candidate.
/// </summary>
public class StaffResetPasswordCommandHandlerTests
{
    private static StaffResetPasswordCommandHandler Handler(InMemoryUnitOfWork uow, FakePasswordHasher hasher) => new(uow, hasher);

    [Fact]
    public async Task Valid_token_changes_hash_and_burns_link()
    {
        var user = AuthData.Staff(email: "hr@example.io", active: true);
        var link = AuthData.VerifyLink("hr@example.io", token: "tok", audience: MagicLinkAudience.Staff);
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(link);

        var res = await Handler(uow, new FakePasswordHasher())
            .Handle(new StaffResetPasswordCommand("hr@example.io", "tok", "Strong1!"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hashed:Strong1!", user.PasswordHash);
        Assert.NotNull(link.UsedAt);
    }

    [Fact]
    public async Task Missing_user_is_rejected()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher())
            .Handle(new StaffResetPasswordCommand("nobody@example.io", "tok", "Strong1!"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Invalid email or recovery token", res.Error);
    }

    [Fact]
    public async Task Missing_or_used_token_is_rejected()
    {
        var user = AuthData.Staff(email: "hr@example.io", active: true);
        var uow = new InMemoryUnitOfWork().Seed(user); // không có MagicLink

        var res = await Handler(uow, new FakePasswordHasher())
            .Handle(new StaffResetPasswordCommand("hr@example.io", "tok", "Strong1!"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Invalid, expired, or already used", res.Error);
    }

    [Fact]
    public async Task Weak_password_is_rejected_after_token_validated()
    {
        var user = AuthData.Staff(email: "hr@example.io", active: true);
        var link = AuthData.VerifyLink("hr@example.io", token: "tok", audience: MagicLinkAudience.Staff);
        var uow = new InMemoryUnitOfWork().Seed(user).Seed(link);

        var res = await Handler(uow, new FakePasswordHasher())
            .Handle(new StaffResetPasswordCommand("hr@example.io", "tok", "weak"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("ít nhất 8", res.Error);   // IsStrongPassword
        Assert.Null(link.UsedAt);                   // token chưa bị tiêu
    }
}

/// <inheritdoc cref="StaffResetPasswordCommandHandlerTests"/>
public class CandidateResetPasswordCommandHandlerTests
{
    private static CandidateResetPasswordCommandHandler Handler(InMemoryUnitOfWork uow, FakePasswordHasher hasher) => new(uow, hasher);

    [Fact]
    public async Task Valid_token_changes_hash_and_burns_link()
    {
        var candidate = AuthData.Candidate(email: "cand@example.io");
        var link = AuthData.VerifyLink("cand@example.io", token: "tok", audience: MagicLinkAudience.Candidate);
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(link);

        var res = await Handler(uow, new FakePasswordHasher())
            .Handle(new CandidateResetPasswordCommand("cand@example.io", "tok", "Strong1!"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hashed:Strong1!", candidate.PasswordHash);
        Assert.NotNull(link.UsedAt);
    }

    [Fact]
    public async Task Missing_candidate_is_rejected()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher())
            .Handle(new CandidateResetPasswordCommand("nobody@example.io", "tok", "Strong1!"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Invalid email or recovery token", res.Error);
    }

    [Fact]
    public async Task Wrong_audience_token_is_rejected()
    {
        var candidate = AuthData.Candidate(email: "cand@example.io");
        // Token audience Staff → không khớp cổng Candidate.
        var link = AuthData.VerifyLink("cand@example.io", token: "tok", audience: MagicLinkAudience.Staff);
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(link);

        var res = await Handler(uow, new FakePasswordHasher())
            .Handle(new CandidateResetPasswordCommand("cand@example.io", "tok", "Strong1!"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Invalid, expired, or already used", res.Error);
    }
}
