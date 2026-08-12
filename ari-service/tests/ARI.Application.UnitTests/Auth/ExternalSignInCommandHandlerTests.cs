using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Commands.CompleteExternalCandidateSignIn;
using ARI.Application.Auth.Commands.CompleteExternalStaffSignIn;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Google OAuth — đuôi nghiệp vụ STAFF (<see cref="CompleteExternalStaffSignInCommandHandler"/>, Rule 15):
/// validate domain (ưu tiên system_settings), CHỈ tài khoản pre-provisioned (không JIT), chặn khoá/pending,
/// happy path mint JWT + refresh. Domain rỗng ⇒ cho phép mọi miền (config test rỗng).
/// </summary>
public class CompleteExternalStaffSignInCommandHandlerTests
{
    private static CompleteExternalStaffSignInCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token)
        => new(uow, token, AuthData.EmptyConfig());

    [Fact]
    public async Task Domain_not_allowed_is_rejected()
    {
        // Super Admin cấu hình chỉ cho phép @company.io qua system_settings.
        var uow = new InMemoryUnitOfWork()
            .Seed(new SystemSetting { Key = "allowed_email_domains", Value = "company.io" });
        var token = new FakeTokenService();

        var res = await Handler(uow, token)
            .Handle(new CompleteExternalStaffSignInCommand("intruder@gmail.com"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.DomainNotAllowed, res.ErrorCode);
        Assert.Equal(0, token.StaffCount);
    }

    [Fact]
    public async Task Unprovisioned_email_is_rejected_without_creating_account()
    {
        var uow = new InMemoryUnitOfWork();   // config rỗng → mọi miền hợp lệ; không seed user
        var token = new FakeTokenService();

        var res = await Handler(uow, token)
            .Handle(new CompleteExternalStaffSignInCommand("ghost@example.io"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.NotProvisioned, res.ErrorCode);
        Assert.Empty(uow.Repo<User>().Items);   // KHÔNG JIT tạo mới
        Assert.Equal(0, token.StaffCount);
    }

    [Fact]
    public async Task Inactive_account_is_pending_approval()
    {
        var uow = new InMemoryUnitOfWork().Seed(AuthData.Staff(email: "hr@example.io", active: false));
        var token = new FakeTokenService();

        var res = await Handler(uow, token)
            .Handle(new CompleteExternalStaffSignInCommand("hr@example.io"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.PendingApproval, res.ErrorCode);
        Assert.Equal(0, token.StaffCount);
    }

    [Fact]
    public async Task Provisioned_active_staff_signs_in_with_jwt_and_refresh()
    {
        var staff = AuthData.Staff(email: "hr@example.io", role: AppRoles.HrAdmin);
        var uow = new InMemoryUnitOfWork().Seed(staff);
        var token = new FakeTokenService { StaffToken = "staff-jwt" };

        var res = await Handler(uow, token)
            .Handle(new CompleteExternalStaffSignInCommand("hr@example.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("staff-jwt", res.Value.AccessToken);
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal(AppRoles.HrAdmin, res.Value.Role);
        Assert.NotNull(staff.LastLoginAt);
        Assert.Single(uow.Repo<RefreshToken>().Items);
    }

    [Fact]
    public async Task Db_allowed_domain_permits_matching_staff()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(new SystemSetting { Key = "allowed_email_domains", Value = "company.io" })
            .Seed(AuthData.Staff(email: "hr@company.io", role: AppRoles.Recruiter));
        var token = new FakeTokenService();

        var res = await Handler(uow, token)
            .Handle(new CompleteExternalStaffSignInCommand("hr@company.io"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, token.StaffCount);
    }
}

/// <summary>
/// Google OAuth — đuôi nghiệp vụ CANDIDATE (<see cref="CompleteExternalCandidateSignInCommandHandler"/>):
/// KHÔNG validate domain, JIT tạo tài khoản lần đầu, chặn tài khoản bị khoá, mint JWT + refresh.
/// </summary>
public class CompleteExternalCandidateSignInCommandHandlerTests
{
    private static CompleteExternalCandidateSignInCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token)
        => new(uow, token);

    [Fact]
    public async Task Jit_creates_candidate_on_first_google_signin()
    {
        var uow = new InMemoryUnitOfWork();
        var token = new FakeTokenService { CandidateToken = "cand-jwt" };

        var res = await Handler(uow, token)
            .Handle(new CompleteExternalCandidateSignInCommand("new@gmail.com", "New User"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("cand-jwt", res.Value.AccessToken);
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
        var created = Assert.Single(uow.Repo<CandidateAccount>().Items);
        Assert.Equal("new@gmail.com", created.Email);
        Assert.Equal("New User", created.FullName);
        Assert.True(created.EmailVerified);
        Assert.Single(uow.Repo<CandidateRefreshToken>().Items);
    }

    [Fact]
    public async Task Jit_uses_email_prefix_when_name_missing()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new FakeTokenService())
            .Handle(new CompleteExternalCandidateSignInCommand("john.doe@gmail.com", null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("john.doe", Assert.Single(uow.Repo<CandidateAccount>().Items).FullName);
    }

    [Fact]
    public async Task Existing_active_candidate_signs_in_without_new_account()
    {
        var cand = new CandidateAccount { Email = "me@example.io", PasswordHash = "", EmailVerified = true, FullName = "Nguyen Van A" };
        var uow = new InMemoryUnitOfWork().Seed(cand);

        var res = await Handler(uow, new FakeTokenService { CandidateToken = "cand-jwt" })
            .Handle(new CompleteExternalCandidateSignInCommand("me@example.io", "Ignored"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("cand-jwt", res.Value.AccessToken);
        Assert.Single(uow.Repo<CandidateAccount>().Items);   // không tạo mới
        Assert.NotNull(cand.LastLoginAt);
    }

    [Fact]
    public async Task Disabled_candidate_is_rejected()
    {
        var cand = new CandidateAccount { Email = "me@example.io", PasswordHash = "", EmailVerified = true, IsActive = false };
        var uow = new InMemoryUnitOfWork().Seed(cand);
        var token = new FakeTokenService();

        var res = await Handler(uow, token)
            .Handle(new CompleteExternalCandidateSignInCommand("me@example.io", null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.AccountDisabled, res.ErrorCode);
        Assert.Equal(0, token.CandidateCount);
    }
}
