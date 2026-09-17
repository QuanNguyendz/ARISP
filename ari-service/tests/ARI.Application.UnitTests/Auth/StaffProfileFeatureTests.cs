using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Auth;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Auth;

/// <summary>
/// Đọc cài đặt thông báo của nhân sự (<see cref="GetStaffSettingsQueryHandler"/>): thiếu tài khoản → NotFound;
/// chưa cấu hình → trả mặc định; đã lưu → deserialize <c>User.SettingsJson</c>.
/// </summary>
public class GetStaffSettingsQueryHandlerTests
{
    [Fact]
    public async Task UTCID01_Missing_account_not_found()
    {
        var res = await new GetStaffSettingsQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetStaffSettingsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Returns_defaults_when_unset()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await new GetStaffSettingsQueryHandler(uow).Handle(new GetStaffSettingsQuery(user.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.ReceiveEmail);
        Assert.True(res.Value.ReceivePush);
    }

    [Fact]
    public async Task UTCID03_Deserializes_stored_settings()
    {
        var user = AuthData.Staff();
        user.SettingsJson = JsonSerializer.Serialize(new StaffSettingsDto { ReceiveEmail = false, ReceivePush = true });
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await new GetStaffSettingsQueryHandler(uow).Handle(new GetStaffSettingsQuery(user.Id), CancellationToken.None);

        Assert.False(res.Value.ReceiveEmail);
        Assert.True(res.Value.ReceivePush);
    }
}

/// <summary>
/// Ghi cài đặt thông báo (<see cref="UpdateStaffSettingsCommandHandler"/>): thiếu tài khoản → NotFound;
/// hợp lệ → serialize vào <c>SettingsJson</c> + persist, trả lại chính cài đặt vừa ghi.
/// </summary>
public class UpdateStaffSettingsCommandHandlerTests
{
    [Fact]
    public async Task UTCID01_Missing_account_not_found()
    {
        var res = await new UpdateStaffSettingsCommandHandler(new InMemoryUnitOfWork())
            .Handle(new UpdateStaffSettingsCommand(Guid.NewGuid(), new StaffSettingsDto()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Persists_and_returns_settings()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await new UpdateStaffSettingsCommandHandler(uow)
            .Handle(new UpdateStaffSettingsCommand(user.Id, new StaffSettingsDto { ReceiveEmail = false, ReceivePush = false }),
                CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.ReceiveEmail);
        Assert.Equal(1, uow.SaveChangesCount);
        var stored = JsonSerializer.Deserialize<StaffSettingsDto>(user.SettingsJson!)!;
        Assert.False(stored.ReceivePush);
    }
}

/// <summary>
/// Đọc hồ sơ cá nhân của nhân sự (<see cref="GetStaffProfileQueryHandler"/>): thiếu tài khoản → NotFound;
/// map email/vai trò/đội + cờ <c>HasPassword</c> (phân biệt tài khoản chỉ đăng nhập Google).
/// </summary>
public class GetStaffProfileQueryHandlerTests
{
    [Fact]
    public async Task UTCID01_Missing_account_not_found()
    {
        var res = await new GetStaffProfileQueryHandler(new InMemoryUnitOfWork())
            .Handle(new GetStaffProfileQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Maps_profile_with_password_flag()
    {
        var user = AuthData.Staff(email: "hr@corp.io", role: AppRoles.HrAdmin, passwordHash: "hashed:pw");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await new GetStaffProfileQueryHandler(uow).Handle(new GetStaffProfileQuery(user.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hr@corp.io", res.Value.Email);
        Assert.Equal(AppRoles.HrAdmin, res.Value.Role);
        Assert.True(res.Value.HasPassword);
        Assert.Null(res.Value.Department); // chưa gán đội
    }

    [Fact]
    public async Task UTCID03_Google_account_has_no_password()
    {
        var user = AuthData.Staff(passwordHash: null);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await new GetStaffProfileQueryHandler(uow).Handle(new GetStaffProfileQuery(user.Id), CancellationToken.None);

        Assert.False(res.Value.HasPassword);
    }
}

/// <summary>
/// Sửa hồ sơ cá nhân (<see cref="UpdateStaffProfileCommandHandler"/>, ADR-065): CHỈ đổi được họ tên
/// (email là danh tính, vai trò do SA cấp, đội đã gỡ khỏi lệnh này); tên trống/quá 120 ký tự bị chặn.
/// </summary>
public class UpdateStaffProfileCommandHandlerTests
{
    [Fact]
    public async Task UTCID01_Blank_name_rejected()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await new UpdateStaffProfileCommandHandler(uow)
            .Handle(new UpdateStaffProfileCommand(user.Id, "   "), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID02_Name_too_long_rejected()
    {
        var user = AuthData.Staff();
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await new UpdateStaffProfileCommandHandler(uow)
            .Handle(new UpdateStaffProfileCommand(user.Id, new string('a', 121)), CancellationToken.None);

        Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task UTCID03_Missing_account_not_found()
    {
        var res = await new UpdateStaffProfileCommandHandler(new InMemoryUnitOfWork())
            .Handle(new UpdateStaffProfileCommand(Guid.NewGuid(), "Nguyen Van A"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID04_Trims_name_and_keeps_email_role()
    {
        var user = AuthData.Staff(email: "hr@corp.io", role: AppRoles.HrAdmin);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await new UpdateStaffProfileCommandHandler(uow)
            .Handle(new UpdateStaffProfileCommand(user.Id, "  Trần Mạnh Dũng  "), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Trần Mạnh Dũng", user.FullName);
        Assert.Equal("hr@corp.io", res.Value.Email);   // email không đổi
        Assert.Equal(AppRoles.HrAdmin, res.Value.Role); // vai trò không đổi
        Assert.Equal(1, uow.SaveChangesCount);
    }
}

/// <summary>
/// Đổi/đặt mật khẩu nhân sự (<see cref="ChangeStaffPasswordCommandHandler"/>): tài khoản đã có mật khẩu buộc
/// xác minh mật khẩu hiện tại; mật khẩu mới phải đủ mạnh; tài khoản Google (chưa có mật khẩu) là lần ĐẶT đầu tiên.
/// </summary>
public class ChangeStaffPasswordCommandHandlerTests
{
    private static ChangeStaffPasswordCommandHandler Handler(InMemoryUnitOfWork uow, FakePasswordHasher hasher)
        => new(uow, hasher);

    [Fact]
    public async Task UTCID01_Missing_account_not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new FakePasswordHasher())
            .Handle(new ChangeStaffPasswordCommand(Guid.NewGuid(), "Old@1234", "New@1234"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Existing_password_requires_current()
    {
        var user = AuthData.Staff(passwordHash: "hashed:pw");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow, new FakePasswordHasher())
            .Handle(new ChangeStaffPasswordCommand(user.Id, null, "New@1234"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("mật khẩu hiện tại", res.Error);
    }

    [Fact]
    public async Task UTCID03_Wrong_current_password_rejected()
    {
        var user = AuthData.Staff(passwordHash: "hashed:pw");
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow, new FakePasswordHasher { VerifyResult = false })
            .Handle(new ChangeStaffPasswordCommand(user.Id, "wrong", "New@1234"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("wrong_current_password", res.ErrorCode);
    }

    [Fact]
    public async Task UTCID04_Weak_new_password_rejected()
    {
        var user = AuthData.Staff(passwordHash: null); // Google → không cần current
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow, new FakePasswordHasher())
            .Handle(new ChangeStaffPasswordCommand(user.Id, null, "weak"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID05_New_same_as_current_rejected()
    {
        var user = AuthData.Staff(passwordHash: "hashed:pw");
        var uow = new InMemoryUnitOfWork().Seed(user);

        // VerifyResult=true: current đúng VÀ new trùng current → chặn.
        var res = await Handler(uow, new FakePasswordHasher { VerifyResult = true })
            .Handle(new ChangeStaffPasswordCommand(user.Id, "Old@1234", "New@1234"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không được trùng", res.Error);
    }

    [Fact]
    public async Task UTCID06_First_time_set_for_google_account()
    {
        var user = AuthData.Staff(passwordHash: null);
        var uow = new InMemoryUnitOfWork().Seed(user);

        var res = await Handler(uow, new FakePasswordHasher())
            .Handle(new ChangeStaffPasswordCommand(user.Id, null, "New@1234"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("hashed:New@1234", user.PasswordHash);
        Assert.Equal("Đặt mật khẩu thành công.", res.Value);
        Assert.Equal(1, uow.SaveChangesCount);
    }
}
