using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common.Security;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Common;

/// <summary>
/// <see cref="JobAccess"/> là cổng phân quyền theo tài nguyên cho tin tuyển dụng — thay cho hai
/// bản <c>CanManageAsync</c> giống hệt nhau cộng 7 bản nội tuyến của cùng một vị từ.
/// </summary>
public class JobAccessTests
{
    private static JobPosting Job(Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Fresher .NET",
        CreatedByUserId = ownerId,
        Status = "active",
    };

    private static Task<(bool ok, JobPosting? job, JobAccessLevel level)> Evaluate(
        InMemoryUnitOfWork uow, Guid jobId, Guid? userId, string? role)
        => JobAccess.EvaluateAsync(uow, jobId, userId, role, CancellationToken.None);

    [Fact]
    public async Task Owner_gets_owner_level()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var (ok, found, level) = await Evaluate(uow, job.Id, owner, AppRoles.Recruiter);

        Assert.True(ok);
        Assert.Equal(job.Id, found!.Id);
        Assert.Equal(JobAccessLevel.Owner, level);
    }

    [Theory]
    [InlineData(AppRoles.HrAdmin)]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(RoleNames.HrAdmin)]     // dạng giá trị DB cũng phải nhận ra
    [InlineData(RoleNames.SuperAdmin)]
    public async Task Admin_gets_admin_level_on_someone_elses_job(string role)
    {
        var job = Job(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var (ok, _, level) = await Evaluate(uow, job.Id, Guid.NewGuid(), role);

        Assert.True(ok);
        Assert.Equal(JobAccessLevel.Admin, level);
    }

    [Fact]
    public async Task Stranger_gets_nothing()
    {
        var job = Job(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var (ok, found, level) = await Evaluate(uow, job.Id, Guid.NewGuid(), AppRoles.Recruiter);

        Assert.False(ok);
        Assert.NotNull(found); // tin có tồn tại — người gọi phân biệt được 404 với 403
        Assert.Equal(JobAccessLevel.None, level);
    }

    [Fact]
    public async Task Hiring_manager_is_not_an_admin()
    {
        // Vai trò Hiring Manager KHÔNG tự nó mở ra tin nào: phạm vi đến từ đội tuyển dụng của
        // từng tin. Nếu chỗ này trả về Admin thì cả mô hình phân quyền của ADR-061 sụp.
        var job = Job(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var (ok, _, level) = await Evaluate(uow, job.Id, Guid.NewGuid(), AppRoles.HiringManager);

        Assert.False(ok);
        Assert.Equal(JobAccessLevel.None, level);
    }

    [Fact]
    public async Task Missing_job_is_distinguishable_from_missing_permission()
    {
        var (ok, job, level) = await Evaluate(new InMemoryUnitOfWork(), Guid.NewGuid(), Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.False(ok);
        Assert.Null(job); // không tìm thấy tin → 404, khác với tìm thấy nhưng không có quyền → 403
        Assert.Equal(JobAccessLevel.None, level);
    }

    [Fact]
    public async Task Anonymous_caller_gets_nothing_even_with_an_admin_role_string()
    {
        // Mọi lệnh ghi đều cần ActorId để ghi audit log; không có danh tính thì không có quyền.
        var job = Job(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(job);

        var (ok, _, level) = await Evaluate(uow, job.Id, Guid.Empty, AppRoles.SuperAdmin);

        Assert.False(ok);
        Assert.Equal(JobAccessLevel.None, level);
    }

    [Fact]
    public async Task CanManage_requires_owner_but_CanView_accepts_team_member_threshold()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var uow = new InMemoryUnitOfWork().Seed(job);

        var manage = await JobAccess.CanManageAsync(uow, job.Id, owner, AppRoles.Recruiter, CancellationToken.None);
        var view = await JobAccess.CanViewAsync(uow, job.Id, owner, AppRoles.Recruiter, CancellationToken.None);

        Assert.True(manage.ok);
        Assert.True(view.ok);

        var strangerManage = await JobAccess.CanManageAsync(uow, job.Id, Guid.NewGuid(), AppRoles.Recruiter, CancellationToken.None);
        var strangerView = await JobAccess.CanViewAsync(uow, job.Id, Guid.NewGuid(), AppRoles.Recruiter, CancellationToken.None);

        Assert.False(strangerManage.ok);
        Assert.False(strangerView.ok);
    }

    // ===== Phạm vi danh sách =====

    [Fact]
    public async Task Scoped_job_ids_is_null_for_admins_meaning_unrestricted()
    {
        // null ≠ tập rỗng. Trả tập rỗng cho quản trị viên sẽ làm trắng mọi màn hình của họ.
        var uow = new InMemoryUnitOfWork().Seed(Job(Guid.NewGuid()));

        var scope = await JobAccess.ScopedJobIdsAsync(uow, Guid.NewGuid(), AppRoles.HrAdmin, CancellationToken.None);

        Assert.Null(scope);
    }

    [Fact]
    public async Task Scoped_job_ids_returns_only_own_jobs_for_a_recruiter()
    {
        var me = Guid.NewGuid();
        var mine = Job(me);
        var theirs = Job(Guid.NewGuid());
        var uow = new InMemoryUnitOfWork().Seed(mine).Seed(theirs);

        var scope = await JobAccess.ScopedJobIdsAsync(uow, me, AppRoles.Recruiter, CancellationToken.None);

        Assert.NotNull(scope);
        Assert.Contains(mine.Id, scope!);
        Assert.DoesNotContain(theirs.Id, scope!);
    }

    [Fact]
    public async Task Scoped_job_ids_is_empty_not_null_for_a_hiring_manager_with_no_assignments()
    {
        // Đây là bất biến chặn đúng rủi ro lớn nhất khi thêm vai trò mới vào policy InternalStaff:
        // một HM chưa được gán tin nào phải thấy DANH SÁCH RỖNG, không phải toàn bộ công ty.
        var uow = new InMemoryUnitOfWork().Seed(Job(Guid.NewGuid()));

        var scope = await JobAccess.ScopedJobIdsAsync(uow, Guid.NewGuid(), AppRoles.HiringManager, CancellationToken.None);

        Assert.NotNull(scope);
        Assert.Empty(scope!);
    }
}
