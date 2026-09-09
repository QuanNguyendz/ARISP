using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.HiringTeam;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.HiringTeam;

/// <summary>
/// Đội tuyển dụng của tin (ADR-061) — trục phân quyền thứ hai bên cạnh chủ tin.
/// Gán THEO TIN chứ không theo phòng ban: <c>department</c> là chuỗi tự do (trên tin thì do Gemini
/// trích từ JD), chỉ dùng để xếp gợi ý lúc chọn người.
/// </summary>
public class HiringTeamTests
{
    /// <summary>Đội dùng chung cho các test gợi ý — ADR-065 đổi phòng ban thành khoá ngoại.</summary>
    private static readonly Department Engineering = new() { Id = Guid.NewGuid(), Name = "Engineering" };
    private static readonly Department Marketing = new() { Id = Guid.NewGuid(), Name = "Marketing" };

    private static User Hm(string name = "Trưởng phòng Backend", Department? department = null) => new()
    {
        Id = Guid.NewGuid(),
        FullName = name,
        Email = $"{Guid.NewGuid():N}@corp.io",
        Role = RoleNames.HiringManager,
        DepartmentId = (department ?? Engineering).Id,
        IsActive = true,
    };

    private static JobPosting Job(Guid owner, string? department = "Engineering") => new()
    {
        Id = Guid.NewGuid(),
        Title = "Backend Developer",
        JobDescription = "Mô tả",
        Status = "active",
        Department = department,
        CreatedByUserId = owner,
    };

    private static AddHiringTeamMemberCommandHandler AddHandler(InMemoryUnitOfWork uow)
        => new(uow, new RecordingNotificationService());

    private static Task<Result<HiringTeamMemberDto>> Add(
        InMemoryUnitOfWork uow, Guid jobId, Guid userId, Guid actorId, string? actorRole = null,
        bool isPrimary = true, string? roleOnJob = null)
        => AddHandler(uow).Handle(
            new AddHiringTeamMemberCommand(jobId, userId, roleOnJob, isPrimary, actorId, actorRole ?? AppRoles.Recruiter),
            CancellationToken.None);

    // ===== Gán người =====

    [Fact]
    public async Task Owner_can_assign_a_hiring_manager_to_their_job()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        var res = await Add(uow, job.Id, hm.Id, owner);

        Assert.True(res.IsSuccess);
        Assert.Equal(hm.Id, res.Value!.UserId);
        Assert.True(res.Value.IsPrimary);
        Assert.Equal(JobTeamRoles.HiringManager, res.Value.RoleOnJob);
    }

    [Fact]
    public async Task A_stranger_cannot_assign_anyone_to_someone_elses_job()
    {
        var job = Job(Guid.NewGuid());
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        var res = await Add(uow, job.Id, hm.Id, Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task A_team_member_cannot_add_more_people_to_the_team()
    {
        // Nếu thành viên đội tự thêm được người khác, một Hiring Manager có thể kéo đồng nghiệp
        // vào để xem hồ sơ ứng viên — leo thang quyền ngay trong tính năng phân quyền.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var other = Hm("HM khác");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm).Seed(other);
        await Add(uow, job.Id, hm.Id, owner);

        var res = await Add(uow, job.Id, other.Id, hm.Id, AppRoles.HiringManager, isPrimary: false);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Cannot_assign_a_recruiter_account_as_hiring_manager()
    {
        // Cổng duyệt sẽ chờ một người không có màn hình nào để duyệt.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var recruiter = new User
        {
            Id = Guid.NewGuid(), Email = "r@corp.io", Role = RoleNames.Recruiter, IsActive = true,
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(recruiter);

        var res = await Add(uow, job.Id, recruiter.Id, owner);

        Assert.True(res.IsFailure);
        Assert.Contains("Hiring Manager", res.Error);
    }

    [Fact]
    public async Task Cannot_assign_a_locked_account()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        hm.IsActive = false;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        var res = await Add(uow, job.Id, hm.Id, owner);

        Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task Promoting_a_second_primary_demotes_the_first()
    {
        // Index UNIQUE có lọc ở DB từ chối hai người cùng cờ chính, nên phải nhường chỗ trước.
        // Nếu không, thao tác đổi Hiring Manager sẽ vỡ ở tầng DB với lỗi khó hiểu.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var first = Hm("HM cũ");
        var second = Hm("HM mới");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(first).Seed(second);

        await Add(uow, job.Id, first.Id, owner);
        await Add(uow, job.Id, second.Id, owner);

        var members = uow.Repo<JobHiringTeamMember>().Items;
        Assert.Single(members, m => m.IsPrimary);
        Assert.Equal(second.Id, members.Single(m => m.IsPrimary).UserId);
    }

    [Fact]
    public async Task Re_adding_a_removed_member_revives_the_same_row()
    {
        // Ràng buộc UNIQUE (job, user) CỐ Ý không lọc deleted_at: chèn dòng thứ hai sẽ vi phạm.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        await Add(uow, job.Id, hm.Id, owner);
        var memberId = uow.Repo<JobHiringTeamMember>().Items.Single().Id;

        await new RemoveHiringTeamMemberCommandHandler(uow, new RecordingNotificationService())
            .Handle(new RemoveHiringTeamMemberCommand(job.Id, memberId, owner, AppRoles.Recruiter), CancellationToken.None);
        await Add(uow, job.Id, hm.Id, owner);

        var row = Assert.Single(uow.Repo<JobHiringTeamMember>().Items);
        Assert.Equal(memberId, row.Id);
        Assert.Null(row.DeletedAt);
    }

    [Fact]
    public async Task Removing_a_member_clears_the_primary_flag()
    {
        // Giữ cờ chính trên dòng đã xoá mềm sẽ khiến index UNIQUE có lọc... vẫn cho qua (vì lọc
        // deleted_at IS NULL), nhưng PrimaryHiringManagerAsync đọc qua query filter nên tin sẽ
        // "không có HM" mà cờ vẫn còn — trạng thái mâu thuẫn. Dọn ngay lúc gỡ.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);
        await Add(uow, job.Id, hm.Id, owner);
        var memberId = uow.Repo<JobHiringTeamMember>().Items.Single().Id;

        await new RemoveHiringTeamMemberCommandHandler(uow, new RecordingNotificationService())
            .Handle(new RemoveHiringTeamMemberCommand(job.Id, memberId, owner, AppRoles.Recruiter), CancellationToken.None);

        var row = Assert.Single(uow.Repo<JobHiringTeamMember>().Items);
        Assert.False(row.IsPrimary);
        Assert.NotNull(row.DeletedAt);
    }

    // ===== Ảnh hưởng tới phân quyền =====

    [Fact]
    public async Task Team_membership_grants_read_but_not_write_on_the_job()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);
        await Add(uow, job.Id, hm.Id, owner);

        var (_, _, level) = await JobAccess.EvaluateAsync(uow, job.Id, hm.Id, AppRoles.HiringManager, CancellationToken.None);

        Assert.Equal(JobAccessLevel.TeamMember, level);

        var canView = await JobAccess.CanViewAsync(uow, job.Id, hm.Id, AppRoles.HiringManager, CancellationToken.None);
        var canManage = await JobAccess.CanManageAsync(uow, job.Id, hm.Id, AppRoles.HiringManager, CancellationToken.None);
        Assert.True(canView.ok);
        Assert.False(canManage.ok); // xem được tin, KHÔNG sửa được tin
    }

    [Fact]
    public async Task Assigned_jobs_enter_the_hiring_managers_data_scope()
    {
        var owner = Guid.NewGuid();
        var mine = Job(owner);
        var theirs = Job(Guid.NewGuid());
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(mine).Seed(theirs).Seed(hm);
        await Add(uow, mine.Id, hm.Id, owner);

        var scope = await JobAccess.ScopedJobIdsAsync(uow, hm.Id, AppRoles.HiringManager, CancellationToken.None);

        Assert.NotNull(scope);
        Assert.Contains(mine.Id, scope!);
        Assert.DoesNotContain(theirs.Id, scope!);
    }

    [Fact]
    public async Task A_job_requires_hm_approval_only_once_someone_is_assigned()
    {
        // Cờ "cần HM duyệt" SUY RA từ việc có người được gán, không phải một cột bật/tắt riêng:
        // cột bool có thể bật mà không ai được gán → phễu chặn ở một người không tồn tại.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        Assert.False(await JobAccess.RequiresHiringManagerApprovalAsync(uow, job.Id, CancellationToken.None));

        await Add(uow, job.Id, hm.Id, owner);

        Assert.True(await JobAccess.RequiresHiringManagerApprovalAsync(uow, job.Id, CancellationToken.None));
    }

    [Fact]
    public async Task An_observer_never_becomes_the_approval_gate()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var watcher = new User
        {
            Id = Guid.NewGuid(), Email = "obs@corp.io", Role = RoleNames.Recruiter, IsActive = true,
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(watcher);

        await Add(uow, job.Id, watcher.Id, owner, isPrimary: true, roleOnJob: JobTeamRoles.Observer);

        // Được gán (nên đọc được tin) nhưng KHÔNG phải cổng duyệt.
        Assert.False(await JobAccess.RequiresHiringManagerApprovalAsync(uow, job.Id, CancellationToken.None));
        var (_, _, level) = await JobAccess.EvaluateAsync(uow, job.Id, watcher.Id, AppRoles.Recruiter, CancellationToken.None);
        Assert.Equal(JobAccessLevel.TeamMember, level);
    }

    // ===== Gợi ý theo phòng ban =====

    [Fact]
    public async Task Hiring_manager_options_rank_matching_department_first()
    {
        var job = Job(Guid.NewGuid(), department: "Engineering");
        var sameDept = Hm("Cùng phòng ban", Engineering);
        var otherDept = Hm("Phòng ban khác", Marketing);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(otherDept).Seed(sameDept)
            .Seed(Engineering, Marketing);

        var res = await new GetHiringManagerOptionsQueryHandler(uow)
            .Handle(new GetHiringManagerOptionsQuery(job.Id), CancellationToken.None);

        Assert.Equal(sameDept.Id, res.Value![0].Id);
        Assert.True(res.Value[0].MatchesJobDepartment);
        // Phòng ban khác VẪN nằm trong danh sách — đây là gợi ý, không phải bộ lọc quyền.
        Assert.Contains(res.Value, o => o.Id == otherDept.Id);
    }

    [Fact]
    public async Task Hiring_manager_options_exclude_other_roles_and_locked_accounts()
    {
        var locked = Hm("Đang khoá");
        locked.IsActive = false;
        var recruiter = new User
        {
            Id = Guid.NewGuid(), Email = "r@corp.io", Role = RoleNames.Recruiter, IsActive = true,
        };
        var ok = Hm("Hợp lệ");
        var uow = new InMemoryUnitOfWork().Seed(locked).Seed(recruiter).Seed(ok);

        var res = await new GetHiringManagerOptionsQueryHandler(uow)
            .Handle(new GetHiringManagerOptionsQuery(null), CancellationToken.None);

        Assert.Equal(ok.Id, Assert.Single(res.Value!).Id);
    }
}
