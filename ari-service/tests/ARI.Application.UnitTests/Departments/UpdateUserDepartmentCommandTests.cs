using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.UpdateUserDepartment;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Departments;

/// <summary>
/// Gán/đổi đội của một tài khoản (ADR-065) — đường DUY NHẤT đổi được đội.
///
/// <c>UpdateStaffProfileCommand</c> cố ý không nhận tham số đó nữa: khi nhân viên tự sửa được thì ô
/// "Đội" khoá cứng trên phiếu chỉ là hình thức — Hiring Manager sửa hồ sơ rồi quay ra lập phiếu.
/// </summary>
public class UpdateUserDepartmentCommandTests
{
    private readonly Guid _superAdminId = Guid.NewGuid();
    private readonly Guid _hmId = Guid.NewGuid();

    private static readonly Department Backend = new() { Id = Guid.NewGuid(), Name = "Backend Team", IsActive = true };
    private static readonly Department Dissolved = new() { Id = Guid.NewGuid(), Name = "Đội cũ", IsActive = false };

    private InMemoryUnitOfWork Seed(Guid? current = null)
    {
        var uow = new InMemoryUnitOfWork();
        uow.Seed(Backend, Dissolved);
        uow.Seed(new User
        {
            Id = _hmId, Email = "hm@x.io", Role = RoleNames.HiringManager, IsActive = true,
            DepartmentId = current,
        });
        return uow;
    }

    private Task<Result> Run(InMemoryUnitOfWork uow, Guid? departmentId) =>
        new UpdateUserDepartmentCommandHandler(uow)
            .Handle(new UpdateUserDepartmentCommand(_hmId, departmentId, _superAdminId), CancellationToken.None);

    [Fact]
    public async Task Gan_doi_dang_hoat_dong_thi_duoc()
    {
        var uow = Seed();

        var res = await Run(uow, Backend.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(Backend.Id, uow.Repo<User>().Items.Single(u => u.Id == _hmId).DepartmentId);
    }

    [Fact]
    public async Task Gan_vao_doi_da_TAT_thi_bi_chan()
    {
        // Gán vào đội đã tắt là dựng sẵn một tài khoản không lập được phiếu, mà chỗ báo lỗi lại nằm
        // tận màn phiếu chứ không phải ở đây.
        var uow = Seed();

        var res = await Run(uow, Dissolved.Id);

        Assert.True(res.IsFailure);
        Assert.Null(uow.Repo<User>().Items.Single(u => u.Id == _hmId).DepartmentId);
    }

    [Fact]
    public async Task Go_khoi_doi_thi_duoc()
    {
        // Người rời công ty / đội giải thể — gỡ phải làm được, khác hẳn với gán vào đội đã tắt.
        var uow = Seed(current: Backend.Id);

        var res = await Run(uow, null);

        Assert.True(res.IsSuccess);
        Assert.Null(uow.Repo<User>().Items.Single(u => u.Id == _hmId).DepartmentId);
    }

    [Fact]
    public async Task Tai_khoan_khong_ton_tai_thi_bao_NotFound()
    {
        var uow = Seed();

        var res = await new UpdateUserDepartmentCommandHandler(uow)
            .Handle(new UpdateUserDepartmentCommand(Guid.NewGuid(), Backend.Id, _superAdminId), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Doi_doi_thi_ghi_AUDIT_LOG()
    {
        // Đội quyết định phiếu tuyển dụng ghi tên bộ phận nào, nên ai đổi đội của ai phải tra được.
        var uow = Seed();

        await Run(uow, Backend.Id);

        var entry = Assert.Single(uow.Repo<AuditLog>().Items);
        Assert.Equal("user_department_updated", entry.Action);
        Assert.Equal(_superAdminId, entry.ActorUserId);
    }
}
