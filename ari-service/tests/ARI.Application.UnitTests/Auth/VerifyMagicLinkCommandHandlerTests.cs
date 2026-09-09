using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Commands.VerifyMagicLink;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đăng nhập passwordless qua magic link (<see cref="VerifyMagicLinkCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "VerifyMagicLink" (UTCID01–11). Lưu ý: handler CHỈ tra theo email — KHÔNG đối chiếu token, KHÔNG kiểm
/// IsActive / EmailVerified — nên chỉ có 2 kết cục: candidate tồn tại → cấp token; không tồn tại → not_found.
/// </summary>
public class VerifyMagicLinkCommandHandlerTests
{
    private const string Email = "candidate@example.com";
    private const string Token = "valid-token";

    private static VerifyMagicLinkCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token) => new(uow, token);

    private static VerifyMagicLinkCommand Cmd(string email = Email, string token = Token) => new(email, token);

    // UTCID01 — candidate tồn tại → cấp candidate token
    [Fact]
    public async Task UTCID01_Existing_candidate_gets_token()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("candidate-access-token", res.Value);
    }

    // UTCID02 — email cần chuẩn hoá → vẫn cấp token
    [Fact]
    public async Task UTCID02_Email_normalized()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(email: " CANDIDATE@EXAMPLE.COM "), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("candidate-access-token", res.Value);
    }

    // UTCID03 — email toàn khoảng trắng, không có candidate khớp → not_found
    [Fact]
    public async Task UTCID03_Whitespace_email_not_found()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(email: " "), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Candidate account not found.", res.Error);
        Assert.Equal(AuthErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID04 — candidate không tồn tại → not_found
    [Fact]
    public async Task UTCID04_Unknown_candidate_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakeTokenService()).Handle(Cmd(email: "unknown@example.com"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID05 — token sai/hết hạn nhưng candidate tồn tại → vẫn Success (handler không đối chiếu token)
    [Fact]
    public async Task UTCID05_Wrong_token_still_succeeds()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(token: "wrong-token"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("candidate-access-token", res.Value);
    }

    // UTCID06 — token rỗng nhưng gọi thẳng handler → vẫn Success
    [Fact]
    public async Task UTCID06_Empty_token_still_succeeds()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(token: ""), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("candidate-access-token", res.Value);
    }

    // UTCID07 — candidate lookup ném lỗi
    [Fact]
    public async Task UTCID07_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateAccount>("DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID08 — token service ném lỗi
    [Fact]
    public async Task UTCID08_Token_service_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var token = new FakeTokenService { CandidateThrows = new Exception("Token Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, token).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Token Error", ex.Message);
    }

    // UTCID09 — token service trả chuỗi rỗng → Success("")
    [Fact]
    public async Task UTCID09_Token_service_returns_empty()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email));
        var token = new FakeTokenService { CandidateToken = "" };
        var res = await Handler(uow, token).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value);
    }

    // UTCID10 — candidate inactive → vẫn Success (handler không kiểm IsActive)
    [Fact]
    public async Task UTCID10_Inactive_candidate_still_succeeds()
    {
        var candidate = AuthData.Candidate(email: Email);
        candidate.IsActive = false;
        var uow = new InMemoryUnitOfWork().Seed(candidate);
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("candidate-access-token", res.Value);
    }

    // UTCID11 — candidate chưa xác minh email → vẫn Success (handler không kiểm EmailVerified)
    [Fact]
    public async Task UTCID11_Unverified_candidate_still_succeeds()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Candidate(email: Email, verified: false));
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("candidate-access-token", res.Value);
    }
}
