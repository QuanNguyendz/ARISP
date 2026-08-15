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
    public async Task Jit_lay_anh_dai_dien_tu_google()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new FakeTokenService()).Handle(
            new CompleteExternalCandidateSignInCommand("new@gmail.com", "New User", "https://lh3.googleusercontent.com/a/abc"),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("https://lh3.googleusercontent.com/a/abc", Assert.Single(uow.Repo<CandidateAccount>().Items).AvatarUrl);
    }

    [Fact]
    public async Task Anh_google_chi_dien_vao_cho_trong_khong_de_anh_tu_tai_len()
    {
        // Ứng viên đã tự chọn ảnh — mỗi lần đăng nhập Google không được đạp mất lựa chọn đó.
        var cand = new CandidateAccount
        {
            Email = "me@example.io",
            EmailVerified = true,
            AvatarUrl = "avatars/anh-toi-tu-chon.png",
        };
        var uow = new InMemoryUnitOfWork().Seed(cand);

        var res = await Handler(uow, new FakeTokenService()).Handle(
            new CompleteExternalCandidateSignInCommand("me@example.io", null, "https://lh3.googleusercontent.com/a/abc"),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("avatars/anh-toi-tu-chon.png", cand.AvatarUrl);
    }

    [Fact]
    public async Task Tai_khoan_chua_co_anh_thi_nhan_anh_google()
    {
        var cand = new CandidateAccount { Email = "me@example.io", EmailVerified = true, AvatarUrl = null };
        var uow = new InMemoryUnitOfWork().Seed(cand);

        var res = await Handler(uow, new FakeTokenService()).Handle(
            new CompleteExternalCandidateSignInCommand("me@example.io", null, "https://lh3.googleusercontent.com/a/abc"),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("https://lh3.googleusercontent.com/a/abc", cand.AvatarUrl);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("avatars/gia-mao.png")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Picture_khong_phai_url_http_bi_bo_qua(string? picture)
    {
        // Cột AvatarUrl dùng chung cho cả storageKey, nên giá trị lạ lọt vào sẽ bị hiểu nhầm
        // thành khoá file khi dựng URL hiển thị.
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new FakeTokenService()).Handle(
            new CompleteExternalCandidateSignInCommand("new@gmail.com", "New User", picture), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Null(Assert.Single(uow.Repo<CandidateAccount>().Items).AvatarUrl);
    }

    [Fact]
    public async Task Google_signin_wipes_password_set_on_an_unverified_account()
    {
        // Chiếm tài khoản trước: kẻ xấu đăng ký form web bằng email nạn nhân và đặt mật khẩu của hắn.
        // Tài khoản nằm im vì EmailVerified=false. Khi chủ email thật đăng nhập Google, nếu ta chỉ set
        // EmailVerified=true thì hoá ra xác minh hộ mật khẩu của kẻ xấu → hắn đăng nhập được.
        var cand = new CandidateAccount
        {
            Email = "victim@gmail.com",
            PasswordHash = "hash-cua-ke-xau",
            EmailVerified = false,
        };
        var uow = new InMemoryUnitOfWork().Seed(cand);

        var res = await Handler(uow, new FakeTokenService())
            .Handle(new CompleteExternalCandidateSignInCommand("victim@gmail.com", "Victim"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(cand.EmailVerified);
        Assert.Equal(string.Empty, cand.PasswordHash);   // mật khẩu của kẻ xấu bị vô hiệu
    }

    [Fact]
    public async Task Google_signin_keeps_password_of_an_already_verified_account()
    {
        // Ngược lại: tài khoản đã xác minh email thì mật khẩu là của chính chủ — không được đụng vào,
        // nếu không mỗi lần đăng nhập bằng Google là người dùng mất mật khẩu đang dùng.
        var cand = new CandidateAccount
        {
            Email = "owner@gmail.com",
            PasswordHash = "hash-cua-chinh-chu",
            EmailVerified = true,
        };
        var uow = new InMemoryUnitOfWork().Seed(cand);

        var res = await Handler(uow, new FakeTokenService())
            .Handle(new CompleteExternalCandidateSignInCommand("owner@gmail.com", null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hash-cua-chinh-chu", cand.PasswordHash);
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
