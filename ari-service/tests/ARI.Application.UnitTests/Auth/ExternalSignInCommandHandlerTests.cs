using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Auth.Commands.CompleteExternalCandidateSignIn;
using ARI.Application.Auth.Commands.CompleteExternalStaffSignIn;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Google OAuth — đuôi nghiệp vụ CANDIDATE (<see cref="CompleteExternalCandidateSignInCommandHandler"/>) —
/// test-plan Report5 Unit v1.2, tab "CompleteExternalCandidateSignIn" (UTCID01–13): JIT tạo tài khoản (Name/prefix),
/// chuẩn hoá email, tài khoản đã có (active verified / active unverified / inactive), và lỗi phụ thuộc.
/// </summary>
public class CompleteExternalCandidateSignInCommandHandlerTests
{
    private const string Email = "candidate@example.com";

    private static CompleteExternalCandidateSignInCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token) => new(uow, token);

    private static CandidateAccount Existing(bool active = true, bool verified = true, string fullName = "Candidate User")
        => new() { Email = Email, PasswordHash = "", EmailVerified = verified, IsActive = active, FullName = fullName };

    private static CompleteExternalCandidateSignInCommand Cmd(string email = Email, string? name = "Candidate User") => new(email, name);

    // UTCID01 — mới, Name có → JIT tạo, FullName="Candidate User"
    [Fact]
    public async Task UTCID01_New_candidate_with_name()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
        var created = Assert.Single(uow.Repo<CandidateAccount>().Items);
        Assert.Equal(Email, created.Email);
        Assert.Equal("Candidate User", created.FullName);
        Assert.True(created.EmailVerified);
        Assert.Single(uow.Repo<CandidateRefreshToken>().Items);
    }

    // UTCID02 — mới, Name=null → FullName = prefix email ("candidate")
    [Fact]
    public async Task UTCID02_New_candidate_null_name_uses_prefix()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(name: null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("candidate", Assert.Single(uow.Repo<CandidateAccount>().Items).FullName);
    }

    // UTCID03 — mới, Name=" " (khoảng trắng) → FullName = prefix email
    [Fact]
    public async Task UTCID03_New_candidate_whitespace_name_uses_prefix()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(name: " "), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("candidate", Assert.Single(uow.Repo<CandidateAccount>().Items).FullName);
    }

    // UTCID04 — email cần chuẩn hoá → tạo với email đã chuẩn hoá
    [Fact]
    public async Task UTCID04_Email_normalized()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(email: " CANDIDATE@EXAMPLE.COM "), CancellationToken.None);
        Assert.True(res.IsSuccess);
        var created = Assert.Single(uow.Repo<CandidateAccount>().Items);
        Assert.Equal(Email, created.Email);
        Assert.Equal("Candidate User", created.FullName);
    }

    // UTCID05 — đã có, active + verified → đăng nhập, không tạo mới
    [Fact]
    public async Task UTCID05_Existing_active_verified()
    {
        var cand = Existing(active: true, verified: true);
        var uow = new InMemoryUnitOfWork().Seed(cand);
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(name: "Ignored"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal(AppRoles.Candidate, res.Value.Role);
        Assert.Single(uow.Repo<CandidateAccount>().Items);
        Assert.NotNull(cand.LastLoginAt);
    }

    // UTCID06 — đã có, active + CHƯA verified → đăng nhập, xoá mật khẩu + đánh dấu verified
    [Fact]
    public async Task UTCID06_Existing_active_unverified_wipes_password()
    {
        var cand = new CandidateAccount { Email = Email, PasswordHash = "attacker-hash", EmailVerified = false, IsActive = true };
        var uow = new InMemoryUnitOfWork().Seed(cand);
        var res = await Handler(uow, new FakeTokenService()).Handle(Cmd(name: null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.True(cand.EmailVerified);
        Assert.Equal("", cand.PasswordHash);
    }

    // UTCID07 — đã có, inactive → account_disabled
    [Fact]
    public async Task UTCID07_Existing_inactive_disabled()
    {
        var uow = new InMemoryUnitOfWork().Seed(Existing(active: false));
        var token = new FakeTokenService();
        var res = await Handler(uow, token).Handle(Cmd(name: null), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.AccountDisabled, res.ErrorCode);
        Assert.Equal(0, token.CandidateCount);
    }

    // UTCID08 — candidate lookup ném lỗi
    [Fact]
    public async Task UTCID08_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<CandidateAccount>("DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("DB Error", ex.Message);
    }

    // UTCID09 — candidate AddAsync (JIT) ném lỗi
    [Fact]
    public async Task UTCID09_Candidate_add_error()
    {
        var uow = new InMemoryUnitOfWork().FailAddFor<CandidateAccount>("Add Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Add Error", ex.Message);
    }

    // UTCID10 — save account (JIT) ném lỗi
    [Fact]
    public async Task UTCID10_Account_save_error()
    {
        var uow = new InMemoryUnitOfWork().FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(Cmd(), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }

    // UTCID11 — token service ném lỗi
    [Fact]
    public async Task UTCID11_Token_service_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Existing());
        var token = new FakeTokenService { CandidateThrows = new Exception("Token Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, token).Handle(Cmd(name: null), CancellationToken.None));
        Assert.Equal("Token Error", ex.Message);
    }

    // UTCID12 — refresh-token AddAsync ném lỗi
    [Fact]
    public async Task UTCID12_Refresh_add_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Existing()).FailAddFor<CandidateRefreshToken>("Refresh Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(Cmd(name: null), CancellationToken.None));
        Assert.Equal("Refresh Error", ex.Message);
    }

    // UTCID13 — refresh-token save (lần 2) ném lỗi
    [Fact]
    public async Task UTCID13_Refresh_save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Existing()).FailSaveOn(2, "Refresh Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService()).Handle(Cmd(name: null), CancellationToken.None));
        Assert.Equal("Refresh Save Error", ex.Message);
    }
}

/// <summary>
/// Google OAuth — đuôi nghiệp vụ STAFF (<see cref="CompleteExternalStaffSignInCommandHandler"/>) —
/// test-plan Report5 Unit v1.2, tab "CompleteExternalStaffSignIn" (UTCID01–16): validate domain (DB ưu tiên,
/// fallback appsettings, rỗng = cho mọi miền, chuẩn hoá list), pre-provisioned (không JIT), khoá/pending, lỗi phụ thuộc.
/// </summary>
public class CompleteExternalStaffSignInCommandHandlerTests
{
    private static CompleteExternalStaffSignInCommandHandler Handler(InMemoryUnitOfWork uow, FakeTokenService token, IConfiguration config)
        => new(uow, token, config);

    private static IConfiguration Config(params (string Key, string Value)[] kv)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(kv.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    private static SystemSetting DomainSetting(string value) => new() { Key = "allowed_email_domains", Value = value };

    private static User StaffUser(string email, bool active = true, string role = "Recruiter")
        => new() { Email = email, Role = role, IsActive = active, FullName = "Staff User" };

    // UTCID01 — DB setting cho company.com + user active → Success{Role="Recruiter"}
    [Fact]
    public async Task UTCID01_Db_domain_active_user()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).Seed(StaffUser("staff@company.com"));
        var res = await Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("staff-access-token", res.Value.AccessToken);
        Assert.False(string.IsNullOrEmpty(res.Value.RefreshToken));
        Assert.Equal("Recruiter", res.Value.Role);
    }

    // UTCID02 — miền không được phép → domain_not_allowed
    [Fact]
    public async Task UTCID02_Domain_not_allowed()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com"));
        var token = new FakeTokenService();
        var res = await Handler(uow, token, AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@blocked.com"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.DomainNotAllowed, res.ErrorCode);
        Assert.Equal(0, token.StaffCount);
    }

    // UTCID03 — DB setting rỗng; config Authentication:AllowedDomains cho phép → Success
    [Fact]
    public async Task UTCID03_Config_authentication_domain()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("")).Seed(StaffUser("staff@company.com"));
        var config = Config(("Authentication:AllowedDomains", "company.com"));
        var res = await Handler(uow, new FakeTokenService(), config).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("Recruiter", res.Value.Role);
    }

    // UTCID04 — thiếu Authentication:AllowedDomains; Auth:AllowedDomains cho phép → Success
    [Fact]
    public async Task UTCID04_Config_auth_fallback_domain()
    {
        var uow = new InMemoryUnitOfWork().Seed(StaffUser("staff@company.com"));
        var config = Config(("Auth:AllowedDomains", "company.com"));
        var res = await Handler(uow, new FakeTokenService(), config).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None);
        Assert.True(res.IsSuccess);
    }

    // UTCID05 — không cấu hình miền nào → cho phép mọi miền
    [Fact]
    public async Task UTCID05_No_domain_config_allows_all()
    {
        var uow = new InMemoryUnitOfWork().Seed(StaffUser("staff@any-domain.com"));
        var res = await Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@any-domain.com"), CancellationToken.None);
        Assert.True(res.IsSuccess);
    }

    // UTCID06 — giá trị miền có khoảng trắng + chữ hoa → vẫn chuẩn hoá đúng
    [Fact]
    public async Task UTCID06_Domain_value_normalized()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting(" @Company.com , OTHER.com ")).Seed(StaffUser("staff@company.com"));
        var res = await Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("Recruiter", res.Value.Role);
    }

    // UTCID07 — user chưa pre-provisioned → account_not_provisioned (không JIT)
    [Fact]
    public async Task UTCID07_Not_provisioned()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com"));
        var res = await Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.NotProvisioned, res.ErrorCode);
        Assert.Empty(uow.Repo<User>().Items);
    }

    // UTCID08 — user IsActive=false → pending_approval
    [Fact]
    public async Task UTCID08_Inactive_pending_approval()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).Seed(StaffUser("staff@company.com", active: false));
        var res = await Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.PendingApproval, res.ErrorCode);
    }

    // UTCID09 — user Role="Pending" → pending_approval
    [Fact]
    public async Task UTCID09_Pending_role_pending_approval()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).Seed(StaffUser("staff@company.com", role: "Pending"));
        var res = await Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal(AuthErrorCodes.PendingApproval, res.ErrorCode);
    }

    // UTCID10 — system-setting repo ném lỗi
    [Fact]
    public async Task UTCID10_Setting_repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<SystemSetting>("Setting DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None));
        Assert.Equal("Setting DB Error", ex.Message);
    }

    // UTCID11 — user repo ném lỗi
    [Fact]
    public async Task UTCID11_User_repo_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).FailFindFor<User>("User DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None));
        Assert.Equal("User DB Error", ex.Message);
    }

    // UTCID12 — user Update ném lỗi
    [Fact]
    public async Task UTCID12_User_update_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).Seed(StaffUser("staff@company.com")).FailUpdateFor<User>("Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None));
        Assert.Equal("Update Error", ex.Message);
    }

    // UTCID13 — save (đóng dấu LastLogin) ném lỗi
    [Fact]
    public async Task UTCID13_First_save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).Seed(StaffUser("staff@company.com")).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }

    // UTCID14 — token service ném lỗi
    [Fact]
    public async Task UTCID14_Token_service_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).Seed(StaffUser("staff@company.com"));
        var token = new FakeTokenService { StaffThrows = new Exception("Token Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, token, AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None));
        Assert.Equal("Token Error", ex.Message);
    }

    // UTCID15 — refresh-token AddAsync ném lỗi
    [Fact]
    public async Task UTCID15_Refresh_add_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).Seed(StaffUser("staff@company.com")).FailAddFor<RefreshToken>("Refresh Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None));
        Assert.Equal("Refresh Error", ex.Message);
    }

    // UTCID16 — save (phát refresh mới, lần 2) ném lỗi
    [Fact]
    public async Task UTCID16_Second_save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(DomainSetting("company.com")).Seed(StaffUser("staff@company.com")).FailSaveOn(2, "Refresh Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new FakeTokenService(), AuthData.EmptyConfig()).Handle(new CompleteExternalStaffSignInCommand("staff@company.com"), CancellationToken.None));
        Assert.Equal("Refresh Save Error", ex.Message);
    }
}
