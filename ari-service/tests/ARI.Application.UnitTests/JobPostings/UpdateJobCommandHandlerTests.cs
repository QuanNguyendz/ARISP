using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.UpdateJob;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Cập nhật tin tuyển dụng (<see cref="UpdateJobCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "UpdateJob" (UTCID01–10): tồn tại, phân quyền, chặn archived, validate, happy path (round giữ nguyên/đổi),
/// giữ metadata file khi không đổi JD, phát sự kiện khi active, và lỗi save.
/// </summary>
/// <remarks>
/// Report input UTCID09 ghi Role=recruiter; nhưng handler chặn recruiter sửa tin đã active (chỉ sửa được draft/rejected).
/// Test dùng hr_admin cho case tin active để đúng hành vi hiện tại.
/// </remarks>
public class UpdateJobCommandHandlerTests
{
    private static readonly Guid OwnerA = Guid.Parse("86000000-0000-0000-0000-000000000001");

    private static Task<Result<JobPostingResponse>> Run(InMemoryUnitOfWork uow, Guid id, CreateJobPostingRequest req, Guid userId, string role, RecordingNotificationService? notif = null)
        => new UpdateJobCommandHandler(uow, notif ?? new RecordingNotificationService()).Handle(new UpdateJobCommand(id, req, userId, role), CancellationToken.None);

    // UTCID01 — job không tồn tại → not_found
    [Fact]
    public async Task UTCID01_Job_not_found()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.NewGuid(), JobPostingData.Request(), OwnerA, AppRoles.Recruiter);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tin tuyển dụng.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID02 — không có quyền → forbidden
    [Fact]
    public async Task UTCID02_Unauthorized()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "draft");
        var uow = new InMemoryUnitOfWork().Seed(job);
        var res = await Run(uow, job.Id, JobPostingData.Request(), OwnerA, AppRoles.Recruiter);
        Assert.True(res.IsFailure);
        Assert.Equal("Bạn không có quyền cập nhật tin tuyển dụng này.", res.Error);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    // UTCID03 — tin đã archived → chặn
    [Fact]
    public async Task UTCID03_Archived_blocked()
    {
        var job = JobPostingData.Job(owner: OwnerA, status: "archived");
        var uow = new InMemoryUnitOfWork().Seed(job);
        var res = await Run(uow, job.Id, JobPostingData.Request(), OwnerA, AppRoles.Recruiter);
        Assert.True(res.IsFailure);
        Assert.Equal("Không thể cập nhật tin tuyển dụng đã lưu trữ (archived).", res.Error);
    }

    // UTCID04 — request không hợp lệ → "Title is required."
    [Fact]
    public async Task UTCID04_Invalid_request()
    {
        var job = JobPostingData.Job(owner: OwnerA, status: "draft");
        var uow = new InMemoryUnitOfWork().Seed(job);
        var req = JobPostingData.Request(); req.Title = " ";
        var res = await Run(uow, job.Id, req, OwnerA, AppRoles.Recruiter);
        Assert.Equal("Title is required.", res.Error);
    }

    // UTCID05 — chủ tin cập nhật, round không đổi → Success, round giữ nguyên (không xoá/tạo lại), save 1 lần
    [Fact]
    public async Task UTCID05_Rounds_unchanged_reused()
    {
        var job = JobPostingData.Job(owner: OwnerA, status: "draft");
        var existingRound = JobPostingData.RoundEntity(job.Id, number: 1, type: "screening", language: "vi", codeTtl: 2, maxMinutes: 45);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(existingRound);
        var req = JobPostingData.Request();   // 1 round screening, language null → kế thừa vi

        var res = await Run(uow, job.Id, req, OwnerA, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Contains(uow.Repo<InterviewRoundConfig>().Items, r => r.Id == existingRound.Id);   // không bị xoá
        Assert.Equal(1, uow.SaveChangesCount);                                                     // chỉ save job
        Assert.NotNull(job.UpdatedAt);
    }

    // UTCID06 — HrAdmin cập nhật, số round thay đổi → xoá round cũ + thêm round mới theo thứ tự
    [Fact]
    public async Task UTCID06_Round_count_changes()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "draft");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(JobPostingData.RoundEntity(job.Id, number: 1, type: "screening"));
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, "screening"), JobPostingData.Round(2, "technical") };

        var res = await Run(uow, job.Id, req, Guid.NewGuid(), AppRoles.HrAdmin);

        Assert.True(res.IsSuccess);
        var rounds = uow.Repo<InterviewRoundConfig>().Items.OrderBy(r => r.RoundNumber).ToList();
        Assert.Equal(2, rounds.Count);
        Assert.Equal(1, rounds[0].RoundNumber);
        Assert.Equal(2, rounds[1].RoundNumber);
    }

    // UTCID07 — round cùng số nhưng khác giá trị → xoá + thêm lại
    [Fact]
    public async Task UTCID07_Round_values_differ()
    {
        var job = JobPostingData.Job(owner: OwnerA, status: "draft");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(JobPostingData.RoundEntity(job.Id, number: 1, type: "screening"));
        var req = JobPostingData.Request();
        req.RoundConfigs = new() { JobPostingData.Round(1, "technical") };   // đổi type

        var res = await Run(uow, job.Id, req, OwnerA, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("technical", Assert.Single(uow.Repo<InterviewRoundConfig>().Items).RoundType);
    }

    // UTCID08 — request không có JdFileUrl mới → giữ nguyên metadata file cũ
    [Fact]
    public async Task UTCID08_Keeps_existing_jd_file()
    {
        var job = JobPostingData.Job(owner: OwnerA, status: "draft", jdFileUrl: "jd/old.pdf", jdFileFormat: "pdf");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(JobPostingData.RoundEntity(job.Id, number: 1, type: "screening", language: "vi"));
        var req = JobPostingData.Request();   // JdFileUrl null

        var res = await Run(uow, job.Id, req, OwnerA, AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
        Assert.Equal("jd/old.pdf", job.JdFileUrl);
    }

    // UTCID09 — tin active (HrAdmin sửa) → phát cả user event lẫn public job update
    [Fact]
    public async Task UTCID09_Active_publishes_public_update()
    {
        var job = JobPostingData.Job(owner: Guid.NewGuid(), status: "active");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(JobPostingData.RoundEntity(job.Id, number: 1, type: "screening", language: "vi"));
        var notif = new RecordingNotificationService();

        var res = await Run(uow, job.Id, JobPostingData.Request(), Guid.NewGuid(), AppRoles.HrAdmin, notif);

        Assert.True(res.IsSuccess);
        Assert.Contains("ReceivePublicJobUpdate", notif.AllEvents);
    }

    // UTCID10 — SaveChangesAsync ném lỗi
    [Fact]
    public async Task UTCID10_Save_error()
    {
        var job = JobPostingData.Job(owner: OwnerA, status: "draft");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(JobPostingData.RoundEntity(job.Id, number: 1, type: "screening", language: "vi")).FailSaveOn(1, "Save Error");

        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, job.Id, JobPostingData.Request(), OwnerA, AppRoles.Recruiter));
        Assert.Equal("Save Error", ex.Message);
    }
}
