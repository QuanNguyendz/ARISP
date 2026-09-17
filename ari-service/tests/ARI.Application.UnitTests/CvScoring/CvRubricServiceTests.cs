using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.Playbooks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using MediatR;
using Xunit;
using static ARI.Application.UnitTests.CvScoring.CvScoringKit;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// Lưu phiên bản bộ tiêu chí của tin + trình soạn (ADR-070).
/// </summary>
public class CvRubricServiceTests
{
    private static RubricCriterion[] TwoCriteria() => new[]
    {
        new RubricCriterion { Key = "experience", Name = "Kinh nghiệm", Weight = 60, Description = "a" },
        new RubricCriterion { Key = "skills", Name = "Kỹ năng", Weight = 40, Description = "b" },
    };

    [Fact]
    public async Task First_save_creates_a_live_rubric_file_and_queues_rescoring()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var queue = new RecordingCvScoringQueue();
        var rag = new RecordingRagIngestionService();
        var storage = new RecordingFileStorage();

        var res = await RubricService(uow, queue, rag, storage).SaveForJobAsync(job.Id, TwoCriteria(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.Changed);
        var live = await CvRubricStore.LiveAsync(uow, job.Id);
        Assert.NotNull(live);
        Assert.Equal("xlsx", live!.FileFormat);
        Assert.Single(storage.Saved);
        Assert.Contains(rag.Ingested, i => i.SourceId == live.Id && i.DocumentType == ScoringRubric.TypeCvRubric);
        Assert.Equal(new[] { job.Id }, queue.Jobs);
    }

    [Fact]
    public async Task New_version_soft_deletes_the_old_one_after_removing_its_chunks()
    {
        var job = Job();
        var old = DefaultRubric(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(old);
        var rag = new RecordingRagIngestionService();

        var res = await RubricService(uow, rag: rag).SaveForJobAsync(job.Id, TwoCriteria(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(old.DeletedAt);
        Assert.Contains(rag.Ingested, i => i.SourceId == old.Id && i.Text == string.Empty);
        var live = await CvRubricStore.LiveAsync(uow, job.Id);
        Assert.NotEqual(old.Id, live!.Id);
    }

    /// <summary>Lưu lại đúng bộ đang dùng → không tạo phiên bản, không chấm lại.</summary>
    [Fact]
    public async Task Saving_the_same_rubric_is_a_no_op()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var queue = new RecordingCvScoringQueue();
        var service = RubricService(uow, queue);

        await service.SaveForJobAsync(job.Id, TwoCriteria(), Guid.NewGuid(), CancellationToken.None);
        var again = await service.SaveForJobAsync(job.Id, TwoCriteria(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(again.Value.Changed);
        Assert.Single(uow.Repo<PlaybookDocument>().Items);
        Assert.Single(queue.Jobs);
    }

    /// <summary>Gỡ chunk cũ hỏng → dừng, bản cũ còn nguyên (ADR-025).</summary>
    [Fact]
    public async Task Rag_failure_on_old_version_keeps_it_live()
    {
        var job = Job();
        var old = DefaultRubric(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(old);
        var rag = new RecordingRagIngestionService { ThrowOnIngest = true };

        var res = await RubricService(uow, rag: rag).SaveForJobAsync(job.Id, TwoCriteria(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Null(old.DeletedAt);
        Assert.Equal(old.Id, (await CvRubricStore.LiveAsync(uow, job.Id))!.Id);
    }

    [Fact]
    public async Task Invalid_criteria_are_rejected()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var bad = new[] { new RubricCriterion { Key = "a", Name = "A", Weight = 50 } };

        var res = await RubricService(uow).SaveForJobAsync(job.Id, bad, Guid.NewGuid(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<PlaybookDocument>().Items);
    }

    [Fact]
    public async Task Request_rubric_is_copied_with_the_requester_as_author()
    {
        var job = Job();
        var request = new RecruitmentRequest
        {
            RequestedByUserId = Guid.NewGuid(),
            CvRubricJson = ScoringRubric.Serialize(TwoCriteria()),
        };
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RubricService(uow).CopyFromRequestAsync(job, request, CancellationToken.None);

        Assert.True(res.IsSuccess);
        var live = await CvRubricStore.LiveAsync(uow, job.Id);
        Assert.Equal(request.RequestedByUserId, live!.UploadedByUserId);
        Assert.Equal(new[] { "experience", "skills" }, CvRubricStore.Criteria(live).Select(c => c.Key));
    }

    [Fact]
    public async Task Legacy_request_without_rubric_copies_nothing()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await RubricService(uow).CopyFromRequestAsync(job, new RecruitmentRequest(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(await CvRubricStore.HasLiveAsync(uow, job.Id));
    }

    // ---------------- SaveJobCvRubricCommand (quyền) ----------------

    private sealed class NoopSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse)(object)Result.Success(new JobCvRubricDto(
                Array.Empty<CvRubricCriterionInput>(), null, null, null, true, 0, 0)));
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
        public System.Collections.Generic.IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public System.Collections.Generic.IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static SaveJobCvRubricCommandHandler SaveHandler(InMemoryUnitOfWork uow, RecordingNotificationService notif)
        => new(uow, RubricService(uow), notif, new NoopSender());

    /// <summary>Recruiter chủ tin chỉ đọc — bộ tiêu chí là quyết định chuyên môn của HM (ADR-069/070).</summary>
    [Fact]
    public async Task Recruiter_owner_cannot_save()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);

        var res = await SaveHandler(uow, new RecordingNotificationService()).Handle(
            new SaveJobCvRubricCommand(job.Id, Inputs(("A", 100)), job.CreatedByUserId, RoleNames.Recruiter), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task Primary_hiring_manager_saves_and_the_owner_is_notified()
    {
        var job = Job();
        var hmId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(job);
        HiringManagerSeed.Primary(uow, job.Id, hmId);
        var notif = new RecordingNotificationService();

        var res = await SaveHandler(uow, notif).Handle(
            new SaveJobCvRubricCommand(job.Id, Inputs(("Kinh nghiệm", 70), ("Kỹ năng", 30)), hmId, RoleNames.HiringManager),
            CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.True(await CvRubricStore.HasLiveAsync(uow, job.Id));
        Assert.Contains(uow.Repo<Notification>().Items, n => n.RecipientUserId == job.CreatedByUserId);
        Assert.Contains(notif.UserEvents, e => e.UserId == job.CreatedByUserId && e.EventType == "ReceiveJobPostingUpdate");

        // Màn đang mở hồ sơ của tin chuyển sang "Đang chấm lại" ngay, không chờ hồ sơ đầu tiên chấm xong.
        Assert.Contains(notif.UserEvents, e => e.UserId == hmId && e.EventType == "ReceiveApplicationStatusUpdate");
        Assert.Contains(notif.UserEvents, e => e.UserId == job.CreatedByUserId && e.EventType == "ReceiveApplicationStatusUpdate");
        Assert.Contains(notif.GroupEvents, e => e.Group == "hr_admin" && e.EventType == "ReceiveApplicationStatusUpdate");
    }

    [Fact]
    public async Task Saving_the_same_rubric_again_does_not_announce_rescoring()
    {
        var job = Job();
        var hmId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(job);
        HiringManagerSeed.Primary(uow, job.Id, hmId);
        var inputs = Inputs(("Kinh nghiệm", 70), ("Kỹ năng", 30));

        Assert.True((await SaveHandler(uow, new RecordingNotificationService()).Handle(
            new SaveJobCvRubricCommand(job.Id, inputs, hmId, RoleNames.HiringManager), CancellationToken.None)).IsSuccess);

        var notif = new RecordingNotificationService();
        var again = await SaveHandler(uow, notif).Handle(
            new SaveJobCvRubricCommand(job.Id, inputs, hmId, RoleNames.HiringManager), CancellationToken.None);

        Assert.True(again.IsSuccess, again.Error);
        Assert.DoesNotContain(notif.UserEvents, e => e.EventType == "ReceiveApplicationStatusUpdate");
        Assert.Empty(notif.GroupEvents);
    }

    [Fact]
    public async Task Hiring_manager_cannot_save_an_invalid_rubric()
    {
        var job = Job();
        var hmId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(job);
        HiringManagerSeed.Primary(uow, job.Id, hmId);

        var res = await SaveHandler(uow, new RecordingNotificationService()).Handle(
            new SaveJobCvRubricCommand(job.Id, Inputs(("A", 30)), hmId, RoleNames.HiringManager), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.False(await CvRubricStore.HasLiveAsync(uow, job.Id));
    }

    // ---------------- Công cụ điền nhanh ----------------

    [Fact]
    public async Task Suggestion_is_rebalanced_and_keyed()
    {
        var gemini = new FakeGeminiProvider
        {
            SuggestResult = Result.Success(new System.Collections.Generic.List<CvRubricSuggestionItem>
            {
                new() { Name = "Kinh nghiệm", Weight = 50, Description = "d", Good = "tốt" },
                new() { Name = "Kỹ năng", Weight = 45, Description = "e" },
            }),
        };

        var res = await new SuggestCvRubricCommandHandler(gemini).Handle(
            new SuggestCvRubricCommand(new CvRubricSuggestionInput("Backend", "mô tả", null, "senior", null)),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(100m, res.Value!.Criteria.Sum(c => c.Weight));
        Assert.Equal("kinh_nghiem", res.Value.Criteria[0].Key);
        Assert.Equal("tốt", res.Value.Criteria[0].Levels!.Good);
        Assert.Empty(res.Value.Warnings);
    }

    [Fact]
    public async Task Suggestion_needs_some_content()
    {
        var res = await new SuggestCvRubricCommandHandler(new FakeGeminiProvider()).Handle(
            new SuggestCvRubricCommand(new CvRubricSuggestionInput("Backend", null, " ", null, null)), CancellationToken.None);
        Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task Sheet_import_returns_a_draft_with_warnings_instead_of_failing()
    {
        var bytes = RubricSheet.Build(new[]
        {
            new RubricCriterion { Name = "Kinh nghiệm", Weight = 50, Description = "d" },
            new RubricCriterion { Name = "Kỹ năng", Weight = 30, Description = "e" },
        });

        var res = await new ParseCvRubricSheetCommandHandler().Handle(new ParseCvRubricSheetCommand(bytes), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value!.Criteria.Count);
        Assert.Contains(res.Value.Warnings, w => w.Contains("100"));
    }

    [Fact]
    public async Task Templates_are_org_level_cv_rubrics_only()
    {
        var job = Job();
        var org = DefaultRubric(job.Id);
        org.Scope = PlaybookScope.ScopeOrg;
        org.ScopeRefId = null;
        org.FileName = "mau-backend.xlsx";
        var jobLevel = DefaultRubric(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(org, jobLevel);

        var res = await new GetCvRubricTemplatesQueryHandler(uow).Handle(new GetCvRubricTemplatesQuery(), CancellationToken.None);

        var t = Assert.Single(res.Value!);
        Assert.Equal("mau-backend", t.Name);
        Assert.Equal(2, t.Criteria.Count);
    }
}
