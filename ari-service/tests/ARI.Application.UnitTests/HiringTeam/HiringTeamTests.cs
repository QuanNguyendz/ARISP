using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Admin.Commands.DeactivateUser;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.HiringTeam;
using ARI.Application.Scheduling;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.HiringTeam;

/// <summary>
/// Đội tuyển dụng của tin (ADR-061) và bất biến "mọi tin luôn có đúng một Hiring Manager chính" (ADR-068).
/// Gán THEO TIN chứ không theo phòng ban: <c>department</c> là chuỗi tự do (trên tin thì do Gemini
/// trích từ JD), chỉ dùng để xếp gợi ý lúc chọn người.
/// </summary>
public class HiringTeamTests
{
    /// <summary>Đội dùng chung cho các test gợi ý — ADR-065 đổi phòng ban thành khoá ngoại.</summary>
    private static readonly Department Engineering = new() { Id = Guid.NewGuid(), Name = "Engineering" };
    private static readonly Department Marketing = new() { Id = Guid.NewGuid(), Name = "Marketing" };

    private const string Reason = "Hiring Manager cũ chuyển sang dự án khác";

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

    /// <summary>Gieo thẳng dòng HM chính — đúng trạng thái mà <c>CreateJobCommand</c> để lại.</summary>
    private static JobHiringTeamMember SeedPrimary(InMemoryUnitOfWork uow, JobPosting job, User hm)
    {
        var row = new JobHiringTeamMember
        {
            JobPostingId = job.Id, UserId = hm.Id, RoleOnJob = JobTeamRoles.HiringManager,
            IsPrimary = true, AddedByUserId = job.CreatedByUserId,
        };
        uow.Seed(row);
        return row;
    }

    private static Task<Result<HiringTeamMemberDto>> Add(
        InMemoryUnitOfWork uow, Guid jobId, Guid userId, Guid actorId, string? actorRole = null, string? roleOnJob = null)
        => new AddHiringTeamMemberCommandHandler(uow, new RecordingNotificationService()).Handle(
            new AddHiringTeamMemberCommand(jobId, userId, roleOnJob, actorId, actorRole ?? AppRoles.Recruiter),
            CancellationToken.None);

    private static Task<Result<bool>> Remove(InMemoryUnitOfWork uow, Guid jobId, Guid memberId, Guid actorId)
        => new RemoveHiringTeamMemberCommandHandler(uow, new RecordingNotificationService())
            .Handle(new RemoveHiringTeamMemberCommand(jobId, memberId, actorId, AppRoles.Recruiter), CancellationToken.None);

    private static Task<Result<HiringTeamMemberDto>> SetHm(
        InMemoryUnitOfWork uow, Guid jobId, Guid userId, string? reason = Reason, string role = AppRoles.HrAdmin, Guid? actor = null)
        => new SetPrimaryHiringManagerCommandHandler(uow, new RecordingNotificationService()).Handle(
            new SetPrimaryHiringManagerCommand(jobId, userId, reason, actor ?? Guid.NewGuid(), role),
            CancellationToken.None);

    // ===== Thêm thành viên phụ =====

    [Fact]
    public async Task Owner_adds_secondary_members_who_default_to_interviewer()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var person = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(person);

        var res = await Add(uow, job.Id, person.Id, owner);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value!.IsPrimary);
        Assert.Equal(JobTeamRoles.Interviewer, res.Value.RoleOnJob);
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
        SeedPrimary(uow, job, hm);

        var res = await Add(uow, job.Id, other.Id, hm.Id, AppRoles.HiringManager);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Cannot_add_a_recruiter_account_in_the_hiring_manager_role()
    {
        // Cổng duyệt sẽ chờ một người không có màn hình nào để duyệt.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var recruiter = new User
        {
            Id = Guid.NewGuid(), Email = "r@corp.io", Role = RoleNames.Recruiter, IsActive = true,
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(recruiter);

        var res = await Add(uow, job.Id, recruiter.Id, owner, roleOnJob: JobTeamRoles.HiringManager);

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
    public async Task Adding_members_never_touches_the_primary_hiring_manager()
    {
        // Trước ADR-068, thêm người với `isPrimary: true` hạ HM thật xuống — kể cả khi người mới là
        // "observer". Recruiter tự mở mọi cổng đang kiểm mình bằng đúng một lời gọi.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm("HM thật");
        var second = Hm("HM phụ");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm).Seed(second);
        SeedPrimary(uow, job, hm);

        var res = await Add(uow, job.Id, second.Id, owner, roleOnJob: JobTeamRoles.HiringManager);

        Assert.True(res.IsSuccess);
        var primary = Assert.Single(uow.Repo<JobHiringTeamMember>().Items, m => m.IsPrimary);
        Assert.Equal(hm.Id, primary.UserId);
    }

    [Fact]
    public async Task Add_cannot_rewrite_the_primary_hiring_managers_row()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);
        var row = SeedPrimary(uow, job, hm);

        var res = await Add(uow, job.Id, hm.Id, owner, roleOnJob: JobTeamRoles.Observer);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.True(row.IsPrimary);
        Assert.Equal(JobTeamRoles.HiringManager, row.RoleOnJob);
    }

    [Fact]
    public async Task Re_adding_a_removed_member_revives_the_same_row()
    {
        // Ràng buộc UNIQUE (job, user) CỐ Ý không lọc deleted_at: chèn dòng thứ hai sẽ vi phạm.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var person = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(person);

        await Add(uow, job.Id, person.Id, owner);
        var memberId = uow.Repo<JobHiringTeamMember>().Items.Single().Id;

        await Remove(uow, job.Id, memberId, owner);
        await Add(uow, job.Id, person.Id, owner);

        var row = Assert.Single(uow.Repo<JobHiringTeamMember>().Items);
        Assert.Equal(memberId, row.Id);
        Assert.Null(row.DeletedAt);
    }

    // ===== Gỡ thành viên =====

    [Fact]
    public async Task The_primary_hiring_manager_cannot_be_removed()
    {
        // ADR-068: tin không bao giờ được rơi vào tình trạng không có HM chính — chỉ CHUYỂN được.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);
        var row = SeedPrimary(uow, job, hm);

        var res = await Remove(uow, job.Id, row.Id, owner);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.True(row.IsPrimary);
        Assert.Null(row.DeletedAt);
    }

    [Fact]
    public async Task Removing_a_secondary_member_soft_deletes_it()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var person = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(person);
        await Add(uow, job.Id, person.Id, owner);
        var memberId = uow.Repo<JobHiringTeamMember>().Items.Single().Id;

        var res = await Remove(uow, job.Id, memberId, owner);

        Assert.True(res.IsSuccess);
        Assert.NotNull(Assert.Single(uow.Repo<JobHiringTeamMember>().Items).DeletedAt);
    }

    // ===== Gán / chuyển Hiring Manager chính (HR Leader) =====

    [Fact]
    public async Task Only_an_admin_can_set_the_primary_hiring_manager()
    {
        // Recruiter không được tự chọn người kiểm mình ở các cổng duyệt — kể cả là chủ tin.
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        var res = await SetHm(uow, job.Id, hm.Id, role: AppRoles.Recruiter, actor: owner);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Empty(uow.Repo<JobHiringTeamMember>().Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("gấp")]
    public async Task Setting_the_primary_hiring_manager_requires_a_reason(string? reason)
    {
        var job = Job(Guid.NewGuid());
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        var res = await SetHm(uow, job.Id, hm.Id, reason);

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<JobHiringTeamMember>().Items);
    }

    [Fact]
    public async Task Assigning_a_job_without_a_hiring_manager_makes_them_primary()
    {
        // Tin cũ (trước ADR-063) không có HM: HR Leader gán qua đúng lệnh này.
        var job = Job(Guid.NewGuid());
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        var res = await SetHm(uow, job.Id, hm.Id);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value!.IsPrimary);
        var (_, _, state) = await JobAccess.HiringManagerStatusAsync(uow, job.Id, CancellationToken.None);
        Assert.Equal(HiringManagerState.Active, state);
    }

    [Fact]
    public async Task Transferring_demotes_the_old_hm_keeps_their_row_and_tells_both_and_the_owner()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var oldHm = Hm("HM cũ");
        var newHm = Hm("HM mới");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldHm).Seed(newHm)
            .Seed(new User { Id = owner, Email = "owner@corp.io", Role = RoleNames.Recruiter, IsActive = true });
        var oldRow = SeedPrimary(uow, job, oldHm);

        var res = await SetHm(uow, job.Id, newHm.Id);

        Assert.True(res.IsSuccess);
        var primary = Assert.Single(uow.Repo<JobHiringTeamMember>().Items, m => m.IsPrimary);
        Assert.Equal(newHm.Id, primary.UserId);
        // Dòng cũ còn nguyên (dấu vết ai từng duyệt tin này), chỉ hạ cờ.
        Assert.False(oldRow.IsPrimary);
        Assert.Null(oldRow.DeletedAt);

        var notices = uow.Repo<Notification>().Items;
        Assert.Contains(notices, n => n.RecipientUserId == newHm.Id);
        Assert.Contains(notices, n => n.RecipientUserId == oldHm.Id);
        Assert.Contains(notices, n => n.RecipientUserId == owner && n.Link == $"/recruiter/my-jobs/{job.Id}");
        Assert.Contains(uow.Repo<AuditLog>().Items, a => a.Action == "hiring_manager_transferred");
    }

    [Fact]
    public async Task The_new_hiring_manager_must_be_an_active_hiring_manager_account()
    {
        var job = Job(Guid.NewGuid());
        var locked = Hm("Đang khoá");
        locked.IsActive = false;
        var recruiter = new User { Id = Guid.NewGuid(), Email = "r@corp.io", Role = RoleNames.Recruiter, IsActive = true };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(locked).Seed(recruiter);

        Assert.True((await SetHm(uow, job.Id, locked.Id)).IsFailure);
        Assert.True((await SetHm(uow, job.Id, recruiter.Id)).IsFailure);
        Assert.Empty(uow.Repo<JobHiringTeamMember>().Items);
    }

    [Fact]
    public async Task After_a_transfer_the_old_hiring_managers_availability_no_longer_counts()
    {
        // Lịch rảnh là lịch của MỘT NGƯỜI: để lại thì Recruiter xếp ca vào giờ của người không còn dự.
        var job = Job(Guid.NewGuid());
        var oldHm = Hm("HM cũ");
        var newHm = Hm("HM mới");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldHm).Seed(newHm);
        SeedPrimary(uow, job, oldHm);
        uow.Seed(new HiringManagerAvailability
        {
            JobPostingId = job.Id, RoundNumber = 1, HiringManagerUserId = oldHm.Id,
            StartTime = DateTimeOffset.UtcNow.AddDays(1), EndTime = DateTimeOffset.UtcNow.AddDays(1).AddHours(4),
        });
        Assert.Single(await HmAvailabilitySupport.ActiveWindowsAsync(uow, job.Id, 1, CancellationToken.None));

        await SetHm(uow, job.Id, newHm.Id);

        Assert.Empty(await HmAvailabilitySupport.ActiveWindowsAsync(uow, job.Id, 1, CancellationToken.None));
    }

    // ===== Tình trạng vị trí HM chính =====

    [Fact]
    public async Task Hiring_manager_state_distinguishes_missing_active_and_inactive()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);

        Assert.Equal(HiringManagerState.Missing,
            (await JobAccess.HiringManagerStatusAsync(uow, job.Id, CancellationToken.None)).state);

        SeedPrimary(uow, job, hm);
        Assert.Equal(HiringManagerState.Active,
            (await JobAccess.HiringManagerStatusAsync(uow, job.Id, CancellationToken.None)).state);

        hm.IsActive = false;
        Assert.Equal(HiringManagerState.Inactive,
            (await JobAccess.HiringManagerStatusAsync(uow, job.Id, CancellationToken.None)).state);

        // Cả hai trường hợp thiếu người hành động được đều là cổng ĐÓNG kèm câu nói rõ phải làm gì.
        var (member, error) = await JobAccess.RequireActiveHiringManagerAsync(uow, job.Id, CancellationToken.None);
        Assert.Null(member);
        Assert.Equal(JobAccessErrors.HiringManagerInactive, error);
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

        await Add(uow, job.Id, watcher.Id, owner, roleOnJob: JobTeamRoles.Observer);

        // Được gán (nên đọc được tin) nhưng KHÔNG phải cổng duyệt.
        Assert.Null(await JobAccess.PrimaryHiringManagerAsync(uow, job.Id, CancellationToken.None));
        var (_, _, level) = await JobAccess.EvaluateAsync(uow, job.Id, watcher.Id, AppRoles.Recruiter, CancellationToken.None);
        Assert.Equal(JobAccessLevel.TeamMember, level);
    }

    [Fact]
    public async Task Locking_a_hiring_manager_is_allowed_and_alerts_hr_leaders_about_their_jobs()
    {
        // Khoá tài khoản là việc an ninh — KHÔNG bị chặn. Nhưng các tin người đó phụ trách đóng cổng,
        // nên HR Leader phải được báo ngay, kèm danh sách tin.
        var job = Job(Guid.NewGuid());
        var hm = Hm();
        var hrLeader = new User { Id = Guid.NewGuid(), Email = "hr@corp.io", Role = RoleNames.HrAdmin, IsActive = true };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm).Seed(hrLeader);
        SeedPrimary(uow, job, hm);

        var res = await new DeactivateUserCommandHandler(uow)
            .Handle(new DeactivateUserCommand(hm.Id, "Nghỉ việc", Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(hm.IsActive);
        var alert = Assert.Single(uow.Repo<Notification>().Items, n => n.RecipientUserId == hrLeader.Id);
        Assert.Contains(job.Title, alert.Body);
        Assert.Equal($"/hr/jobs/{job.Id}", alert.Link);
    }

    // ===== Ảnh hưởng tới phân quyền =====

    [Fact]
    public async Task Team_membership_grants_read_but_not_write_on_the_job()
    {
        var owner = Guid.NewGuid();
        var job = Job(owner);
        var hm = Hm();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(hm);
        SeedPrimary(uow, job, hm);

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
        SeedPrimary(uow, mine, hm);

        var scope = await JobAccess.ScopedJobIdsAsync(uow, hm.Id, AppRoles.HiringManager, CancellationToken.None);

        Assert.NotNull(scope);
        Assert.Contains(mine.Id, scope!);
        Assert.DoesNotContain(theirs.Id, scope!);
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
