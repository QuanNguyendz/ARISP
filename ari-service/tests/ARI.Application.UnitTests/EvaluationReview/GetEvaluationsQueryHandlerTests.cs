using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Evaluations.Queries.GetEvaluations;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.EvaluationReview;

/// <summary>
/// Danh sách đánh giá cho HR giám sát (UC-84/85, <see cref="GetEvaluationsQueryHandler"/>): ẩn buổi thử,
/// lọc theo job + trạng thái (pending/completed/verdict), phân trang, join tên ứng viên/vị trí/HR review.
/// </summary>
public class GetEvaluationsQueryHandlerTests
{
    /// <summary>Mặc định chạy dưới quyền quản trị viên để các bài test dưới đây kiểm ĐÚNG logic
    /// truy vấn (lọc, phân trang, join) chứ không bị phạm vi dữ liệu che mất. Phần phạm vi có
    /// nhóm test riêng ở cuối file.</summary>
    private static Task<Result<PaginatedResponse<EvaluationListItemResponse>>> Run(
        InMemoryUnitOfWork uow, Guid? jobId = null, string? status = null, int page = 1, int pageSize = 20,
        Guid? userId = null, string? role = null)
        => new GetEvaluationsQueryHandler(uow).Handle(
            new GetEvaluationsQuery(jobId, status, page, pageSize, userId ?? Guid.NewGuid(), role ?? AppRoles.HrAdmin),
            CancellationToken.None);

    [Fact]
    public async Task Excludes_practice_evaluations()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(EvaluationData.Eval(app.Id, type: "real"))
            .Seed(EvaluationData.Eval(app.Id, type: "practice"));

        var res = await Run(uow);

        Assert.Equal(1, res.Value!.Total); // buổi thử không vào danh sách HR (ADR-051)
        Assert.Equal("real", Assert.Single(res.Value.Items).SessionType);
    }

    [Fact]
    public async Task Filters_by_job_posting()
    {
        var jobA = EvaluationData.Job("Job A");
        var jobB = EvaluationData.Job("Job B");
        var appA = EvaluationData.App(jobA.Id);
        var appB = EvaluationData.App(jobB.Id);
        var uow = new InMemoryUnitOfWork().Seed(jobA, jobB).Seed(appA, appB)
            .Seed(EvaluationData.Eval(appA.Id)).Seed(EvaluationData.Eval(appB.Id));

        var res = await Run(uow, jobId: jobA.Id);

        Assert.Equal(appA.Id, Assert.Single(res.Value!.Items).ApplicationId);
    }

    [Fact]
    public async Task Status_pending_returns_only_unreviewed()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var reviewed = EvaluationData.Eval(app.Id);
        var pending = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(reviewed, pending)
            .Seed(EvaluationData.Review(reviewed.Id));

        var res = await Run(uow, status: "pending");

        Assert.Equal(pending.Id, Assert.Single(res.Value!.Items).Id);
        Assert.Equal("pending", res.Value.Items[0].Status);
    }

    [Fact]
    public async Task Status_completed_returns_only_reviewed_with_final_verdict()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var reviewed = EvaluationData.Eval(app.Id, verdict: "not_pass");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(reviewed)
            .Seed(EvaluationData.Review(reviewed.Id, finalVerdict: "pass")); // HR override

        var res = await Run(uow, status: "completed");

        var item = Assert.Single(res.Value!.Items);
        Assert.Equal("completed", item.Status);
        Assert.Equal("pass", item.FinalVerdict); // lấy verdict HR đã chốt (không phải AI)
    }

    [Fact]
    public async Task Status_verdict_filter_respects_hr_override()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var aiPass = EvaluationData.Eval(app.Id, verdict: "pass");                 // AI pass, chưa review
        var overridden = EvaluationData.Eval(app.Id, verdict: "not_pass");         // AI not_pass, HR chốt pass
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(aiPass, overridden)
            .Seed(EvaluationData.Review(overridden.Id, finalVerdict: "pass"));

        var res = await Run(uow, status: "pass");

        Assert.Equal(2, res.Value!.Total); // cả AI-pass lẫn HR-override-pass
    }

    [Fact]
    public async Task Paginates_and_reports_total()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app)
            .Seed(EvaluationData.Eval(app.Id, createdAt: DateTimeOffset.UtcNow.AddMinutes(-3)))
            .Seed(EvaluationData.Eval(app.Id, createdAt: DateTimeOffset.UtcNow.AddMinutes(-2)))
            .Seed(EvaluationData.Eval(app.Id, createdAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var page1 = await Run(uow, page: 1, pageSize: 2);
        var page2 = await Run(uow, page: 2, pageSize: 2);

        Assert.Equal(3, page1.Value!.Total);
        Assert.Equal(2, page1.Value.TotalPages);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.Single(page2.Value!.Items);
        // Mới nhất trước: item đầu trang 1 mới hơn item trang 2.
        Assert.True(page1.Value.Items[0].CreatedAt > page2.Value.Items[0].CreatedAt);
    }

    [Fact]
    public async Task Joins_candidate_job_and_review_status()
    {
        var job = EvaluationData.Job("Backend Developer");
        var app = EvaluationData.App(job.Id, name: "Trần B", email: "b@example.io");
        var eval = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval).Seed(EvaluationData.Review(eval.Id, "pass"));

        var item = Assert.Single((await Run(uow)).Value!.Items);

        Assert.Equal("Trần B", item.CandidateName);
        Assert.Equal("Backend Developer", item.JobTitle);
        Assert.Equal("completed", item.Status);
        Assert.Equal("pass", item.FinalVerdict);
    }

    [Fact]
    public async Task Empty_returns_zero_total()
    {
        var res = await Run(new InMemoryUnitOfWork());

        Assert.True(res.IsSuccess);
        Assert.Equal(0, res.Value!.Total);
        Assert.Empty(res.Value.Items);
    }

    // ===== Phạm vi dữ liệu (Phase 1 của ADR-061) =====
    //
    // Trước đây query này KHÔNG nhận danh tính nào: bất kỳ ai qua được policy InternalStaff đều
    // phân trang được toàn bộ báo cáo phỏng vấn của công ty, kèm tên, email ứng viên và điểm số.

    private static (InMemoryUnitOfWork uow, Guid ownerA, Guid ownerB) TwoRecruitersWithOneEvalEach()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var jobA = EvaluationData.Job("Tin của A", ownerA);
        var jobB = EvaluationData.Job("Tin của B", ownerB);
        var appA = EvaluationData.App(jobA.Id, name: "Ứng viên của A");
        var appB = EvaluationData.App(jobB.Id, name: "Ứng viên của B");

        var uow = new InMemoryUnitOfWork()
            .Seed(jobA).Seed(jobB).Seed(appA).Seed(appB)
            .Seed(EvaluationData.Eval(appA.Id)).Seed(EvaluationData.Eval(appB.Id));

        return (uow, ownerA, ownerB);
    }

    [Fact]
    public async Task Recruiter_only_sees_evaluations_of_their_own_jobs()
    {
        var (uow, ownerA, _) = TwoRecruitersWithOneEvalEach();

        var res = await Run(uow, userId: ownerA, role: AppRoles.Recruiter);

        Assert.Equal(1, res.Value!.Total);
        Assert.Equal("Ứng viên của A", Assert.Single(res.Value.Items).CandidateName);
    }

    [Fact]
    public async Task Total_count_is_scoped_too_not_just_the_page()
    {
        // Lọc sau khi phân trang sẽ cho ra những trang trống lỗ chỗ và một con số tổng vẫn là
        // tổng của cả công ty — tự nó đã là rò rỉ thông tin.
        var (uow, ownerA, _) = TwoRecruitersWithOneEvalEach();

        var res = await Run(uow, userId: ownerA, role: AppRoles.Recruiter);

        Assert.Equal(1, res.Value!.Total);
    }

    [Fact]
    public async Task Admin_still_sees_every_evaluation()
    {
        var (uow, _, _) = TwoRecruitersWithOneEvalEach();

        var res = await Run(uow, role: AppRoles.HrAdmin);

        Assert.Equal(2, res.Value!.Total);
    }

    [Fact]
    public async Task Filtering_by_a_job_outside_your_scope_returns_nothing()
    {
        // Không đủ nếu chỉ lọc theo phạm vi rồi để nguyên bộ lọc jobPostingId: người dùng đưa
        // thẳng id tin của người khác vào query string vẫn phải ra rỗng.
        var (uow, ownerA, ownerB) = TwoRecruitersWithOneEvalEach();
        var jobOfB = uow.Repo<JobPosting>().Items.Single(j => j.CreatedByUserId == ownerB);

        var res = await Run(uow, jobId: jobOfB.Id, userId: ownerA, role: AppRoles.Recruiter);

        Assert.Equal(0, res.Value!.Total);
        Assert.Empty(res.Value.Items);
    }

    [Fact]
    public async Task Hiring_manager_with_no_assigned_jobs_sees_nothing()
    {
        var (uow, _, _) = TwoRecruitersWithOneEvalEach();

        var res = await Run(uow, userId: Guid.NewGuid(), role: AppRoles.HiringManager);

        Assert.Equal(0, res.Value!.Total);
    }
}
