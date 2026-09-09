using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.VerifyCandidateEmail;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Xác minh email ứng viên (<see cref="VerifyCandidateEmailCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "VerifyCandidateEmail" (UTCID01–14): tài khoản tồn tại, đã xác minh, token hợp lệ (match/expiry/used/audience),
/// happy path (đánh dấu xác minh + tiêu token), chuẩn hoá email, và lỗi phụ thuộc.
/// </summary>
public class VerifyCandidateEmailCommandHandlerTests
{
    private const string Email = "candidate@example.com";
    private const string Token = "valid-token";

    private static VerifyCandidateEmailCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    private static MagicLink Link(string audience = MagicLinkAudience.CandidateEmailVerify, DateTimeOffset? expiresAt = null, DateTimeOffset? usedAt = null)
        => AuthData.VerifyLink(Email, Token, audience, expiresAt, usedAt);

    private static VerifyCandidateEmailCommand Cmd(string email = Email, string token = Token) => new(email, token);

    // UTCID01 — candidate không tồn tại → Failure
    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Liên kết xác minh không hợp lệ.", res.Error);
    }

    // UTCID02 — đã xác minh trước đó → Success với thông điệp tương ứng
    [Fact]
    public async Task UTCID02_Already_verified()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: true));
        var res = await Handler(uow).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("Tài khoản đã được xác minh trước đó. Bạn có thể đăng nhập.", res.Value);
    }

    // UTCID03 — không có MagicLink xác minh → Failure
    [Fact]
    public async Task UTCID03_No_verification_link()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false));
        var res = await Handler(uow).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu gửi lại.", res.Error);
    }

    // UTCID04 — token không khớp → Failure
    [Fact]
    public async Task UTCID04_Token_mismatch()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).Seed(Link());
        var res = await Handler(uow).Handle(Cmd(token: "wrong-token"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu gửi lại.", res.Error);
    }

    // UTCID05 — token hết hạn → Failure
    [Fact]
    public async Task UTCID05_Token_expired()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).Seed(Link(expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));
        var res = await Handler(uow).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
    }

    // UTCID06 — token đã dùng → Failure
    [Fact]
    public async Task UTCID06_Token_used()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).Seed(Link(usedAt: DateTimeOffset.UtcNow));
        var res = await Handler(uow).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
    }

    // UTCID07 — MagicLink sai audience → Failure
    [Fact]
    public async Task UTCID07_Wrong_audience()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).Seed(Link(audience: MagicLinkAudience.Candidate));
        var res = await Handler(uow).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsFailure);
    }

    // UTCID08 — hợp lệ → Success, đánh dấu xác minh + tiêu token
    [Fact]
    public async Task UTCID08_Valid_verification()
    {
        var candidate = AuthData.Candidate(email: Email, verified: false);
        var link = Link();
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(link);

        var res = await Handler(uow).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Xác minh email thành công. Bạn có thể đăng nhập ngay bây giờ.", res.Value);
        Assert.True(candidate.EmailVerified);
        Assert.NotNull(link.UsedAt);
    }

    // UTCID09 — email cần chuẩn hoá → vẫn xác minh thành công
    [Fact]
    public async Task UTCID09_Email_normalized()
    {
        var candidate = AuthData.Candidate(email: Email, verified: false);
        var uow = new InMemoryUnitOfWork().Seed(candidate).Seed(Link());
        var res = await Handler(uow).Handle(Cmd(email: " CANDIDATE@EXAMPLE.COM "), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.True(candidate.EmailVerified);
    }

    // UTCID10 — candidate lookup ném lỗi
    [Fact]
    public async Task UTCID10_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateAccount>("DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID11 — MagicLink lookup ném lỗi
    [Fact]
    public async Task UTCID11_MagicLink_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).FailFindFor<MagicLink>("MagicLink DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("MagicLink DB Error", ex.Message);
    }

    // UTCID12 — candidate Update ném lỗi
    [Fact]
    public async Task UTCID12_Candidate_update_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).Seed(Link()).FailUpdateFor<CandidateAccount>("Candidate Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Candidate Update Error", ex.Message);
    }

    // UTCID13 — MagicLink Update ném lỗi
    [Fact]
    public async Task UTCID13_MagicLink_update_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).Seed(Link()).FailUpdateFor<MagicLink>("MagicLink Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("MagicLink Update Error", ex.Message);
    }

    // UTCID14 — SaveChangesAsync ném lỗi
    [Fact]
    public async Task UTCID14_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false)).Seed(Link()).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }
}
