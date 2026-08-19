using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth.Commands.Logout;
using ARI.Application.Common.Security;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đăng xuất — revoke refresh token (<see cref="LogoutCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "LogoutCommandHandler" (UTCID01–13): null/rỗng/khoảng trắng → no-op Success; khớp bảng staff / candidate /
/// cả hai → đặt RevokedAt; token lạ hoặc đã revoke → no-op; và lỗi phụ thuộc (lookup/update/save từng bảng).
/// </summary>
public class LogoutCommandHandlerTests
{
    private static LogoutCommandHandler Handler(InMemoryUnitOfWork uow) => new(uow);

    private static RefreshToken StaffToken(string raw, DateTimeOffset? revokedAt = null)
        => new() { UserId = Guid.NewGuid(), TokenHash = TokenHashing.Sha256Base64(raw), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10), RevokedAt = revokedAt };

    private static CandidateRefreshToken CandToken(string raw)
        => new() { CandidateAccountId = Guid.NewGuid(), TokenHash = TokenHashing.Sha256Base64(raw), ExpiresAt = DateTimeOffset.UtcNow.AddDays(10) };

    // UTCID01 — RefreshToken=null → no-op Success
    [Fact]
    public async Task UTCID01_Null_token_no_op()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Handler(uow).Handle(new LogoutCommand(null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID02 — RefreshToken="" → no-op Success
    [Fact]
    public async Task UTCID02_Empty_token_no_op()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Handler(uow).Handle(new LogoutCommand(""), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID03 — RefreshToken=" " → no-op Success
    [Fact]
    public async Task UTCID03_Whitespace_token_no_op()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Handler(uow).Handle(new LogoutCommand(" "), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID04 — khớp token staff → revoke staff token
    [Fact]
    public async Task UTCID04_Staff_token_revoked()
    {
        var token = StaffToken("staff-refresh-token");
        var uow = new InMemoryUnitOfWork().Seed(token);

        var res = await Handler(uow).Handle(new LogoutCommand("staff-refresh-token"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(token.RevokedAt);
    }

    // UTCID05 — khớp token candidate → revoke candidate token
    [Fact]
    public async Task UTCID05_Candidate_token_revoked()
    {
        var token = CandToken("candidate-refresh-token");
        var uow = new InMemoryUnitOfWork().Seed(token);

        var res = await Handler(uow).Handle(new LogoutCommand("candidate-refresh-token"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(token.RevokedAt);
    }

    // UTCID06 — cả hai bảng đều có token active khớp → revoke cả hai
    [Fact]
    public async Task UTCID06_Both_tables_revoked()
    {
        var staff = StaffToken("shared-refresh-token");
        var cand = CandToken("shared-refresh-token");
        var uow = new InMemoryUnitOfWork().Seed(staff).Seed(cand);

        var res = await Handler(uow).Handle(new LogoutCommand("shared-refresh-token"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(staff.RevokedAt);
        Assert.NotNull(cand.RevokedAt);
    }

    // UTCID07 — không có token khớp → no-op Success
    [Fact]
    public async Task UTCID07_Unknown_token_no_op()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new LogoutCommand("unknown-refresh-token"), CancellationToken.None);
        Assert.True(res.IsSuccess);
    }

    // UTCID08 — token khớp nhưng đã revoke → không match (query lọc RevokedAt==null) → no-op Success
    [Fact]
    public async Task UTCID08_Already_revoked_token_no_op()
    {
        var already = DateTimeOffset.UtcNow.AddMinutes(-5);
        var token = StaffToken("already-revoked-token", revokedAt: already);
        var uow = new InMemoryUnitOfWork().Seed(token);

        var res = await Handler(uow).Handle(new LogoutCommand("already-revoked-token"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(already, token.RevokedAt);   // không bị đặt lại
    }

    // UTCID09 — staff token lookup ném lỗi
    [Fact]
    public async Task UTCID09_Staff_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<RefreshToken>("Staff DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(new LogoutCommand("valid-refresh-token"), CancellationToken.None));
        Assert.Equal("Staff DB Error", ex.Message);
    }

    // UTCID10 — candidate token lookup ném lỗi
    [Fact]
    public async Task UTCID10_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateRefreshToken>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(new LogoutCommand("valid-refresh-token"), CancellationToken.None));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    // UTCID11 — staff token Update ném lỗi
    [Fact]
    public async Task UTCID11_Staff_update_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(StaffToken("valid-refresh-token")).FailUpdateFor<RefreshToken>("Staff Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(new LogoutCommand("valid-refresh-token"), CancellationToken.None));
        Assert.Equal("Staff Update Error", ex.Message);
    }

    // UTCID12 — candidate token Update ném lỗi
    [Fact]
    public async Task UTCID12_Candidate_update_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(CandToken("valid-refresh-token")).FailUpdateFor<CandidateRefreshToken>("Candidate Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(new LogoutCommand("valid-refresh-token"), CancellationToken.None));
        Assert.Equal("Candidate Update Error", ex.Message);
    }

    // UTCID13 — SaveChangesAsync ném lỗi
    [Fact]
    public async Task UTCID13_Save_error()
    {
        var uow = new InMemoryUnitOfWork().FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(new LogoutCommand("valid-refresh-token"), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }
}
