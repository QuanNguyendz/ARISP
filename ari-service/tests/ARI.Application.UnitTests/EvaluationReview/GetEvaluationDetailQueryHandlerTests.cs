using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Evaluations;
using ARI.Application.Evaluations.Queries.GetEvaluationDetail;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.EvaluationReview;

/// <summary>
/// Chi tiết đánh giá cho HR (UC-64/86/95, <see cref="GetEvaluationDetailQueryHandler"/>): tra theo EvaluationId
/// hoặc fallback SessionId; ẩn buổi thử; kèm HR review + resolve URL video buổi thật (ADR-052).
/// </summary>
public class GetEvaluationDetailQueryHandlerTests
{
    /// <summary>Mặc định chạy dưới quyền quản trị viên: các bài dưới đây kiểm logic tra cứu, phần
    /// phạm vi dữ liệu có test riêng ở cuối file.</summary>
    private static Task<Result<EvaluationDetailResponse>> Run(
        InMemoryUnitOfWork uow, Guid id, RecordingFileStorage? storage = null,
        Guid? userId = null, string? role = null)
        => new GetEvaluationDetailQueryHandler(uow, storage ?? new RecordingFileStorage())
            .Handle(new GetEvaluationDetailQuery(id, userId ?? Guid.NewGuid(), role ?? AppRoles.HrAdmin),
                CancellationToken.None);

    [Fact]
    public async Task Not_found_fails()
    {
        var res = await Run(new InMemoryUnitOfWork(), Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Contains("Evaluation not found", res.Error);
    }

    [Fact]
    public async Task Found_by_evaluation_id()
    {
        var job = EvaluationData.Job("Data Engineer");
        var app = EvaluationData.App(job.Id, name: "Lê C");
        var eval = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval);

        var res = await Run(uow, eval.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(eval.Id, res.Value!.Id);
        Assert.Equal("Lê C", res.Value.CandidateName);
        Assert.Equal("Data Engineer", res.Value.JobTitle);
    }

    [Fact]
    public async Task Falls_back_to_session_id_lookup()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var sessionId = Guid.NewGuid();
        var eval = EvaluationData.Eval(app.Id, sessionId: sessionId);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval);

        var res = await Run(uow, sessionId); // truyền SessionId, không phải EvaluationId

        Assert.True(res.IsSuccess);
        Assert.Equal(eval.Id, res.Value!.Id);
    }

    [Fact]
    public async Task Rejects_practice_evaluation()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var eval = EvaluationData.Eval(app.Id, type: "practice");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval);

        var res = await Run(uow, eval.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("Evaluation not found", res.Error); // ẩn buổi thử khỏi HR
    }

    [Fact]
    public async Task Application_not_found_fails()
    {
        var eval = EvaluationData.Eval(Guid.NewGuid()); // app không seed
        var uow = new InMemoryUnitOfWork().Seed(eval);

        var res = await Run(uow, eval.Id);

        Assert.True(res.IsFailure);
        Assert.Contains("Application associated", res.Error);
    }

    [Fact]
    public async Task Includes_hr_review_when_present()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var eval = EvaluationData.Eval(app.Id, verdict: "not_pass");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval)
            .Seed(EvaluationData.Review(eval.Id, finalVerdict: "pass", isOverride: true));

        var res = await Run(uow, eval.Id);

        Assert.NotNull(res.Value!.HrReview);
        Assert.Equal("pass", res.Value.HrReview!.FinalVerdict);
        Assert.True(res.Value.HrReview.IsOverride);
    }

    [Fact]
    public async Task Resolves_recording_url_from_session()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id);
        var sessionId = Guid.NewGuid();
        var eval = EvaluationData.Eval(app.Id, sessionId: sessionId);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(5);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval)
            .Seed(new InterviewSession
            {
                Id = sessionId, ApplicationId = app.Id, RoundNumber = 1, SessionType = "real",
                RecordingUrl = "rec/interview.webm", RecordingExpiresAt = expiresAt,
            });

        var res = await Run(uow, eval.Id);

        Assert.Equal("/files/rec/interview.webm", res.Value!.RecordingUrl); // resolve qua storage
        Assert.Equal(expiresAt, res.Value.RecordingExpiresAt);
    }

    [Fact]
    public async Task Attaches_cv_jd_match_from_linked_analysis()
    {
        var job = EvaluationData.Job();
        var analysis = new CvJdAnalysis
        {
            JobPostingId = job.Id,
            MatchScore = 73,
            Summary = "Khớp phần lớn kỹ năng backend, thiếu kinh nghiệm Kubernetes.",
        };
        var app = EvaluationData.App(job.Id);
        app.CvJdAnalysisId = analysis.Id;
        var eval = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval).Seed(analysis);

        var res = await Run(uow, eval.Id);

        // Màn duyệt trước đây vẽ cứng số 87 cho mọi hồ sơ — nay phải là điểm thật của hồ sơ này.
        Assert.Equal(73, res.Value!.CvMatchScore);
        Assert.Equal(analysis.Summary, res.Value.CvMatchSummary);
    }

    [Fact]
    public async Task Cv_match_is_null_when_application_has_no_analysis()
    {
        var job = EvaluationData.Job();
        var app = EvaluationData.App(job.Id); // CvJdAnalysisId = null
        var eval = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval);

        var res = await Run(uow, eval.Id);

        // Null để FE ẩn hẳn thẻ, thay vì bịa ra một con số.
        Assert.Null(res.Value!.CvMatchScore);
        Assert.Null(res.Value.CvMatchSummary);
    }

    // ===== Phạm vi dữ liệu (Phase 1 của ADR-061) =====

    [Fact]
    public async Task Refuses_an_evaluation_belonging_to_someone_elses_job()
    {
        // Endpoint này trả transcript, bảng điểm từng tiêu chí và LINK VIDEO buổi phỏng vấn.
        // Trước đây nó không kiểm tra danh tính nào ngoài policy InternalStaff.
        var job = EvaluationData.Job(owner: Guid.NewGuid());
        var app = EvaluationData.App(job.Id);
        var eval = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval);

        var res = await Run(uow, eval.Id, userId: Guid.NewGuid(), role: AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Allows_the_owner_of_the_job()
    {
        var owner = Guid.NewGuid();
        var job = EvaluationData.Job(owner: owner);
        var app = EvaluationData.App(job.Id);
        var eval = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval);

        var res = await Run(uow, eval.Id, userId: owner, role: AppRoles.Recruiter);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Lookup_by_session_id_is_scoped_the_same_way()
    {
        // Endpoint /session/{id} đi qua cùng handler — không được để hở một lối vào thứ hai.
        var job = EvaluationData.Job(owner: Guid.NewGuid());
        var app = EvaluationData.App(job.Id);
        var eval = EvaluationData.Eval(app.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(eval);

        var res = await Run(uow, eval.SessionId, userId: Guid.NewGuid(), role: AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }
}
