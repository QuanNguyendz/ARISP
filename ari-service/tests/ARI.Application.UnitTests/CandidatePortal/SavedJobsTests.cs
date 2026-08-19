using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>Hằng số + builder dùng chung cho test Saved Jobs (bám GUID cố định của Report5 Unit v1.2).</summary>
internal static class SavedJobData
{
    public static readonly Guid CandidateA = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public static readonly Guid JobA = Guid.Parse("51000000-0000-0000-0000-000000000001");

    public static JobPosting Job(Guid id, string status = "active", bool isPublic = true)
        => new() { Id = id, Title = "Backend Developer", Status = status, IsPublicListing = isPublic, CreatedByUserId = Guid.NewGuid() };

    public static SavedJob Saved(Guid candidate, Guid job, DateTimeOffset? at = null)
        => new() { CandidateAccountId = candidate, JobPostingId = job, CreatedAt = at ?? DateTimeOffset.UtcNow };
}

/// <summary>
/// Danh sách job đã lưu (<see cref="GetSavedJobsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetSavedJobs" (UTCID01–09): rỗng, chỉ tin active+public, loại tin đóng/private/không tồn tại,
/// gộp trùng lấy SavedAt mới nhất, sắp SavedAt giảm dần, và lỗi repo (saved / job).
/// </summary>
public class GetSavedJobsQueryHandlerTests
{
    private static Task<Result<System.Collections.Generic.List<SavedJobItemDto>>> Run(InMemoryUnitOfWork uow)
        => new GetSavedJobsQueryHandler(uow).Handle(new GetSavedJobsQuery(SavedJobData.CandidateA), CancellationToken.None);

    [Fact]
    public async Task UTCID01_No_saved()
    {
        var res = await Run(new InMemoryUnitOfWork());
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID02_One_active_public_saved_job()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA)).Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));
        var res = await Run(uow);
        Assert.Equal(SavedJobData.JobA, Assert.Single(res.Value).Id);
    }

    [Fact]
    public async Task UTCID03_Inactive_excluded()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA, status: "inactive")).Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));
        var res = await Run(uow);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID04_Private_excluded()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA, isPublic: false)).Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));
        var res = await Run(uow);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID05_Missing_job_excluded()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));   // no job seeded
        var res = await Run(uow);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID06_Duplicate_uses_latest_saved_at()
    {
        var now = DateTimeOffset.UtcNow;
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA))
            .Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA, now.AddMinutes(-5)),
                  SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA, now));
        var res = await Run(uow);
        var item = Assert.Single(res.Value);
        Assert.Equal(now, item.SavedAt);
    }

    [Fact]
    public async Task UTCID07_Ordered_by_saved_at_desc()
    {
        var now = DateTimeOffset.UtcNow;
        var jobB = Guid.Parse("51000000-0000-0000-0000-000000000002");
        var uow = new InMemoryUnitOfWork()
            .Seed(SavedJobData.Job(SavedJobData.JobA), SavedJobData.Job(jobB))
            .Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA, now.AddMinutes(-5)),
                  SavedJobData.Saved(SavedJobData.CandidateA, jobB, now));
        var res = await Run(uow);
        Assert.Equal(jobB, res.Value[0].Id);   // mới nhất trước
    }

    [Fact]
    public async Task UTCID08_Saved_repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<SavedJob>("Saved Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Saved Job DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID09_Job_repo_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA)).FailFindFor<JobPosting>("Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Job DB Error", ex.Message);
    }
}

/// <summary>
/// ID job đã lưu (<see cref="GetSavedJobIdsQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetSavedJobIds" (UTCID01–05): rỗng, 1 id, khử trùng, nhiều id, và lỗi repo.
/// </summary>
public class GetSavedJobIdsQueryHandlerTests
{
    private static Task<Result<System.Collections.Generic.List<Guid>>> Run(InMemoryUnitOfWork uow)
        => new GetSavedJobIdsQueryHandler(uow).Handle(new GetSavedJobIdsQuery(SavedJobData.CandidateA), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Empty()
    {
        var res = await Run(new InMemoryUnitOfWork());
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    [Fact]
    public async Task UTCID02_One_id()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));
        var res = await Run(uow);
        Assert.Equal(SavedJobData.JobA, Assert.Single(res.Value));
    }

    [Fact]
    public async Task UTCID03_Duplicates_distinct()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA), SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));
        var res = await Run(uow);
        Assert.Equal(SavedJobData.JobA, Assert.Single(res.Value));
    }

    [Fact]
    public async Task UTCID04_Multiple_ids()
    {
        var jobB = Guid.Parse("51000000-0000-0000-0000-000000000002");
        var jobC = Guid.Parse("51000000-0000-0000-0000-000000000003");
        var uow = new InMemoryUnitOfWork().Seed(
            SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA),
            SavedJobData.Saved(SavedJobData.CandidateA, jobB),
            SavedJobData.Saved(SavedJobData.CandidateA, jobC));
        var res = await Run(uow);
        Assert.Equal(3, res.Value.Count);
    }

    [Fact]
    public async Task UTCID05_Repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<SavedJob>("Saved Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Saved Job DB Error", ex.Message);
    }
}

/// <summary>
/// Lưu job (<see cref="SaveJobCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "SaveJob" (UTCID01–08): tin không tồn tại → not_found; chưa lưu → thêm; đã lưu → idempotent;
/// tin đóng/private vẫn lưu được (chỉ kiểm tồn tại); và lỗi phụ thuộc (job/saved lookup/add/save).
/// </summary>
public class SaveJobCommandHandlerTests
{
    private static Task<Result> Run(InMemoryUnitOfWork uow)
        => new SaveJobCommandHandler(uow).Handle(new SaveJobCommand(SavedJobData.CandidateA, SavedJobData.JobA), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Job_not_found()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tin tuyển dụng.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Empty(uow.Repo<SavedJob>().Items);
    }

    [Fact]
    public async Task UTCID02_Saves_new()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA));
        var res = await Run(uow);
        Assert.True(res.IsSuccess);
        var saved = Assert.Single(uow.Repo<SavedJob>().Items);
        Assert.Equal(SavedJobData.CandidateA, saved.CandidateAccountId);
        Assert.Equal(SavedJobData.JobA, saved.JobPostingId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID03_Already_saved_idempotent()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA)).Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));
        var res = await Run(uow);
        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<SavedJob>().Items);   // không thêm
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID04_Inactive_job_still_saved()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA, status: "inactive", isPublic: false));
        var res = await Run(uow);
        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<SavedJob>().Items);
    }

    [Fact]
    public async Task UTCID05_Job_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<JobPosting>("Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Job DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID06_Saved_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA)).FailFindFor<SavedJob>("Saved Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Saved Job DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID07_Add_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA)).FailAddFor<SavedJob>("Add Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Add Error", ex.Message);
    }

    [Fact]
    public async Task UTCID08_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Job(SavedJobData.JobA)).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Bỏ lưu job (<see cref="UnsaveJobCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "UnsaveJob" (UTCID01–07): chưa lưu → no-op; 1/nhiều bản ghi → xoá hết; không có bản ghi active → no-op;
/// và lỗi phụ thuộc (find/delete/save).
/// </summary>
public class UnsaveJobCommandHandlerTests
{
    private static Task<Result> Run(InMemoryUnitOfWork uow)
        => new UnsaveJobCommandHandler(uow).Handle(new UnsaveJobCommand(SavedJobData.CandidateA, SavedJobData.JobA), CancellationToken.None);

    [Fact]
    public async Task UTCID01_Not_saved_noop()
    {
        var uow = new InMemoryUnitOfWork();
        var res = await Run(uow);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID02_Single_deleted()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));
        var res = await Run(uow);
        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<SavedJob>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID03_Multiple_deleted()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA),
            SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA));
        var res = await Run(uow);
        Assert.True(res.IsSuccess);
        Assert.Empty(uow.Repo<SavedJob>().Items);
    }

    [Fact]
    public async Task UTCID04_No_active_matching_noop()
    {
        // Không có bản ghi active khớp (bản mềm đã bị repo lọc) → FindAsync rỗng → no-op.
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Saved(Guid.NewGuid(), SavedJobData.JobA));   // của ứng viên khác
        var res = await Run(uow);
        Assert.True(res.IsSuccess);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID05_Find_error()
    {
        var uow = new InMemoryUnitOfWork().FailFindFor<SavedJob>("Saved Job DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Saved Job DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID06_Delete_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA)).FailDeleteFor<SavedJob>("Delete Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Delete Error", ex.Message);
    }

    [Fact]
    public async Task UTCID07_Save_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(SavedJobData.Saved(SavedJobData.CandidateA, SavedJobData.JobA)).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow));
        Assert.Equal("Save Error", ex.Message);
    }
}
