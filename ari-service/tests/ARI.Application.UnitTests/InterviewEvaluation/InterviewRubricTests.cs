using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.Evaluations;
using ARI.Application.InterviewRubrics;
using ARI.Application.Playbooks;
using ARI.Application.UnitTests.PracticeInterview;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Xunit;

namespace ARI.Application.UnitTests.InterviewEvaluation;

/// <summary>
/// Bộ tiêu chí chấm PHỎNG VẤN theo tin (ADR-073): một đường ghi (<see cref="InterviewRubricService"/>), mỗi (tin, vòng)
/// một bộ sống, không có ý kiểm, lưu xong là các buổi đang chờ bộ tiêu chí tự vào hàng chấm; và lệnh chấm lại của
/// nhân sự cùng trạng thái hiển thị "chờ bộ tiêu chí" / "chấm lỗi".
/// </summary>
public class InterviewRubricTests
{
    private static List<RubricCriterion> Criteria(params (string Key, decimal Weight)[] rows)
        => rows.Select(r => new RubricCriterion
        {
            Key = r.Key,
            Name = r.Key,
            Weight = r.Weight,
            Description = "Chuẩn chấm",
            Checks = new List<RubricCheck> { new() { Key = "k1", Text = "Ý kiểm của CV" } },
        }).ToList();

    private static List<PlaybookDocument> Live(InMemoryUnitOfWork uow, Guid jobId)
        => uow.Repo<PlaybookDocument>().Items
            .Where(p => p.ScopeRefId == jobId && p.DeletedAt == null && p.DocumentType == ScoringRubric.TypeInterviewRubric)
            .ToList();

    // ---------- Đường ghi duy nhất ----------

    [Fact]
    public async Task Saving_creates_one_live_job_set_without_cv_checks()
    {
        var job = PracticeData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await EvaluationKit.RubricService(uow)
            .SaveAsync(job.Id, null, Criteria(("technical", 60), ("communication", 40)), Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        var doc = Assert.Single(Live(uow, job.Id));
        Assert.Equal(PlaybookScope.ScopeJobPosting, doc.Scope);
        Assert.Null(doc.RoundNumber);
        // Ý kiểm là dấu hiệu tra được trên CV — không có nghĩa với câu trả lời phỏng vấn.
        Assert.All(ScoringRubric.Deserialize(doc.RubricJson), c => Assert.Null(c.Checks));
    }

    [Fact]
    public async Task Saving_the_same_criteria_again_creates_no_new_version()
    {
        var job = PracticeData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var service = EvaluationKit.RubricService(uow);
        var criteria = Criteria(("technical", 100));

        await service.SaveAsync(job.Id, null, criteria, Guid.NewGuid(), CancellationToken.None);
        var again = await service.SaveAsync(job.Id, null, criteria, Guid.NewGuid(), CancellationToken.None);

        Assert.False(again.Value.Changed);
        Assert.Single(uow.Repo<PlaybookDocument>().Items);
    }

    [Fact]
    public async Task A_new_version_retires_the_previous_one_and_its_knowledge_chunks()
    {
        var job = PracticeData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var rag = new RecordingRagIngestionService();
        var service = EvaluationKit.RubricService(uow, rag: rag);

        var first = await service.SaveAsync(job.Id, null, Criteria(("technical", 100)), Guid.NewGuid(), CancellationToken.None);
        var second = await service.SaveAsync(job.Id, null, Criteria(("technical", 50), ("teamwork", 50)), Guid.NewGuid(), CancellationToken.None);

        Assert.True(second.Value.Changed);
        Assert.NotNull(first.Value.Document.DeletedAt);
        Assert.Equal(second.Value.Document.Id, Assert.Single(Live(uow, job.Id)).Id);
        // Chunk của bản cũ được gỡ khỏi kho tri thức (ingest văn bản rỗng) TRƯỚC khi xoá mềm (ADR-025).
        Assert.Contains(rag.Ingested, c => c.SourceId == first.Value.Document.Id && c.Text == string.Empty);
    }

    [Fact]
    public async Task Saving_releases_interviews_waiting_for_criteria_into_the_queue()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var waiting = PracticeData.Session(app.Id, type: "real", status: "completed");
        waiting.EvaluationStatus = EvaluationStatuses.BlockedNoRubric;
        var done = PracticeData.Session(app.Id, round: 2, type: "real", status: "completed");
        done.EvaluationStatus = EvaluationStatuses.Done;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(waiting, done);
        var queue = new RecordingEvaluationQueue();

        await EvaluationKit.RubricService(uow, queue)
            .SaveAsync(job.Id, null, Criteria(("technical", 100)), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(new[] { waiting.Id }, queue.Enqueued);
        Assert.Equal(EvaluationStatuses.Pending, waiting.EvaluationStatus);
        Assert.Equal(EvaluationStatuses.Done, done.EvaluationStatus);
    }

    [Fact]
    public async Task A_round_set_is_separate_from_the_job_set_and_can_be_removed()
    {
        var job = PracticeData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var service = EvaluationKit.RubricService(uow);

        await service.SaveAsync(job.Id, null, Criteria(("technical", 100)), Guid.NewGuid(), CancellationToken.None);
        await service.SaveAsync(job.Id, 2, Criteria(("system_design", 100)), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("system_design", Assert.Single(await InterviewRubricStore.ResolveAsync(uow, job.Id, 2)).Key);
        Assert.Equal("technical", Assert.Single(await InterviewRubricStore.ResolveAsync(uow, job.Id, 1)).Key);

        var removed = await service.RemoveRoundAsync(job.Id, 2, CancellationToken.None);

        Assert.True(removed.Value);
        Assert.Equal("technical", Assert.Single(await InterviewRubricStore.ResolveAsync(uow, job.Id, 2)).Key);
    }

    [Fact]
    public async Task Missing_rounds_ignore_online_tests_and_count_round_sets()
    {
        var job = PracticeData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 1, RoundType = InterviewRoundTypes.Screening },
                  new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 2, RoundType = InterviewRoundTypes.OnlineTest },
                  new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 3, RoundType = InterviewRoundTypes.Technical });

        Assert.Equal(new[] { 1, 3 }, await InterviewRubricStore.MissingRoundsAsync(uow, job.Id));

        await EvaluationKit.RubricService(uow).SaveAsync(job.Id, 3, Criteria(("system_design", 100)), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(new[] { 1 }, await InterviewRubricStore.MissingRoundsAsync(uow, job.Id));
    }

    // ---------- Quyền ghi (command) ----------

    private sealed class RubricSender : ISender
    {
        private readonly InMemoryUnitOfWork _uow;
        public RubricSender(InMemoryUnitOfWork uow) => _uow = uow;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetJobInterviewRubricQuery q)
                return (TResponse)(object)await new GetJobInterviewRubricQueryHandler(_uow).Handle(q, cancellationToken);
            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static Task<Result<JobInterviewRubricDto>> Save(
        InMemoryUnitOfWork uow, Guid jobId, int? round, Guid actor, string role)
        => new SaveJobInterviewRubricCommandHandler(uow, EvaluationKit.RubricService(uow), new RecordingNotificationService(), new RubricSender(uow))
            .Handle(new SaveJobInterviewRubricCommand(jobId, round,
                CvRubricEditing.ToInput(Criteria(("technical", 100))), actor, role), CancellationToken.None);

    [Fact]
    public async Task Primary_hiring_manager_saves_and_sees_no_missing_round()
    {
        var job = PracticeData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var hm = HiringManagerSeed.Primary(uow, job.Id);

        var res = await Save(uow, job.Id, null, hm.Id, AppRoles.HiringManager);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Single(res.Value!.JobLevel.Criteria);
        Assert.Empty(res.Value.MissingRounds);
        Assert.True(res.Value.CanEdit);
    }

    [Fact]
    public async Task Recruiter_owner_cannot_write_the_interview_criteria()
    {
        var owner = Guid.NewGuid();
        var job = PracticeData.Job();
        job.CreatedByUserId = owner;
        var uow = new InMemoryUnitOfWork().Seed(job);
        HiringManagerSeed.Primary(uow, job.Id);

        var res = await Save(uow, job.Id, null, owner, AppRoles.Recruiter);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
        Assert.Empty(uow.Repo<PlaybookDocument>().Items);
    }

    [Fact]
    public async Task A_round_set_cannot_target_an_online_test_round()
    {
        var job = PracticeData.Job();
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(new InterviewRoundConfig { JobPostingId = job.Id, RoundNumber = 2, RoundType = InterviewRoundTypes.OnlineTest });
        var hm = HiringManagerSeed.Primary(uow, job.Id);

        var res = await Save(uow, job.Id, 2, hm.Id, AppRoles.HiringManager);

        Assert.True(res.IsFailure);
        Assert.Contains("trắc nghiệm", res.Error);
    }

    // ---------- Chấm lại (nhân sự bấm) ----------

    private static Task<Result<bool>> Retry(InMemoryUnitOfWork uow, RecordingEvaluationQueue queue, Guid sessionId, Guid actor, string role)
        => new RetryInterviewEvaluationCommandHandler(uow, queue)
            .Handle(new RetryInterviewEvaluationCommand(sessionId, actor, role), CancellationToken.None);

    [Fact]
    public async Task Hiring_manager_retries_a_failed_interview_and_it_is_queued_afresh()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id, type: "real", status: "completed");
        session.EvaluationStatus = EvaluationStatuses.Failed;
        session.EvaluationAttempts = EvaluationStatuses.MaxAttempts;
        session.EvaluationError = "timeout";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        var hm = HiringManagerSeed.Primary(uow, job.Id);
        var queue = new RecordingEvaluationQueue();

        var res = await Retry(uow, queue, session.Id, hm.Id, AppRoles.HiringManager);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal(EvaluationStatuses.Pending, session.EvaluationStatus);
        Assert.Equal(0, session.EvaluationAttempts);
        Assert.Null(session.EvaluationError);
        Assert.Equal(new[] { session.Id }, queue.Enqueued);
    }

    [Fact]
    public async Task Retry_refuses_an_interview_that_already_has_a_report()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id, type: "real", status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session)
            .Seed(PracticeData.Eval(session.Id, app.Id, type: "real"));
        var hm = HiringManagerSeed.Primary(uow, job.Id);
        var queue = new RecordingEvaluationQueue();

        var res = await Retry(uow, queue, session.Id, hm.Id, AppRoles.HiringManager);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Conflict, res.ErrorCode);
        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task Practice_sessions_do_not_exist_for_staff_retry()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id, type: "practice", status: "completed");
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        var hm = HiringManagerSeed.Primary(uow, job.Id);

        var res = await Retry(uow, new RecordingEvaluationQueue(), session.Id, hm.Id, AppRoles.HiringManager);

        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task Retry_is_refused_to_staff_outside_the_job()
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id, type: "real", status: "completed");
        session.EvaluationStatus = EvaluationStatuses.Failed;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);
        HiringManagerSeed.Primary(uow, job.Id);

        var res = await Retry(uow, new RecordingEvaluationQueue(), session.Id, Guid.NewGuid(), AppRoles.Recruiter);

        Assert.True(res.IsFailure);
    }

    // ---------- Trạng thái hiển thị ----------

    [Theory]
    [InlineData(EvaluationStatuses.BlockedNoRubric, 0, "needs_rubric")]
    [InlineData(EvaluationStatuses.Failed, EvaluationStatuses.MaxAttempts, "evaluation_failed")]
    [InlineData(EvaluationStatuses.Failed, 1, "evaluating")]           // còn lượt thử tự động → vẫn "đang chấm"
    [InlineData(EvaluationStatuses.Pending, 0, "evaluating")]
    public async Task Staff_result_rows_say_why_there_is_no_report_yet(string status, int attempts, string expected)
    {
        var job = PracticeData.Job();
        var app = PracticeData.App(job.Id);
        var session = PracticeData.Session(app.Id, type: "real", status: "completed");
        session.EvaluationStatus = status;
        session.EvaluationAttempts = attempts;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(app).Seed(session);

        var res = await new GetApplicationInterviewResultsQueryHandler(uow)
            .Handle(new GetApplicationInterviewResultsQuery(app.Id, Guid.NewGuid(), AppRoles.HrAdmin), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal(expected, Assert.Single(res.Value!).State);
    }

    [Theory]
    [InlineData("completed", EvaluationStatuses.BlockedNoRubric, 0, false, "needs_rubric")]
    [InlineData("completed", EvaluationStatuses.NoAnswers, 0, false, "no_answers")]
    [InlineData("completed", EvaluationStatuses.Failed, 1, false, "pending")]
    [InlineData("completed", EvaluationStatuses.Failed, EvaluationStatuses.MaxAttempts, false, "failed")]
    [InlineData("completed", null, 0, true, "done")]
    [InlineData("active", null, 0, false, null)]
    public void Candidate_display_state_is_derived_in_one_place(
        string sessionStatus, string? evaluationStatus, int attempts, bool hasEvaluation, string? expected)
        => Assert.Equal(expected, EvaluationProgress.ForDisplay(sessionStatus, evaluationStatus, attempts, hasEvaluation));
}
