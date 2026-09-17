using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.CvScoring;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>Scope factory tối giản — chỉ được gọi trong task nền (đã bọc try/catch); ném để nhánh nền dừng.</summary>
internal sealed class ThrowingScopeFactory : IServiceScopeFactory
{
    public IServiceScope CreateScope() => throw new NotSupportedException("no background scope in unit test");
}

/// <summary>Dựng <see cref="GetCvMatchQueryHandler"/> với bộ chấm thật trên kho in-memory.</summary>
internal static class CvMatchHandlerFactory
{
    public static GetCvMatchQueryHandler Create(InMemoryUnitOfWork uow, RecordingFileStorage storage, CvScoringInFlight? inFlight = null)
    {
        inFlight ??= new CvScoringInFlight();
        return new GetCvMatchQueryHandler(uow, storage, new ThrowingScopeFactory(),
            CvScoringKit.Service(uow, new FakeGeminiProvider(), storage: storage, inFlight: inFlight), inFlight);
    }
}

/// <summary>
/// Độ phù hợp CV–JD ở Portal (<see cref="GetCvMatchQueryHandler"/>) — test-plan Report5 Unit, tab "GetCvMatch":
/// thiếu tài khoản/CV, không đọc được CV, tin chưa có bộ tiêu chí (ADR-070), kết quả sẵn có
/// (completed / invalid_cv), đang chấm, vừa hỏng, khởi chạy chấm nền, và các lỗi phụ thuộc.
/// </summary>
public class GetCvMatchQueryHandlerTests
{
    private static readonly Guid CandidateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly byte[] Bytes = { 1, 2, 3 };

    private static CandidateAccount Account(string? profileCvUrl = null, string? profileCvFileName = null)
        => new() { Id = CandidateId, Email = "candidate@example.com", ProfileCvUrl = profileCvUrl, ProfileCvFileName = profileCvFileName };

    private static Task<Result<CvMatchResponse>> Run(
        InMemoryUnitOfWork uow, RecordingFileStorage storage, Guid jobId, CvScoringInFlight? inFlight = null)
        => CvMatchHandlerFactory.Create(uow, storage, inFlight).Handle(new GetCvMatchQuery(jobId, CandidateId), CancellationToken.None);

    private static (JobPosting Job, PlaybookDocument Rubric, InMemoryUnitOfWork Uow) JobWithRubric(string? cvUrl = "stored/cv.pdf")
    {
        var job = CvScoringKit.Job();
        var rubric = CvScoringKit.DefaultRubric(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: cvUrl)).Seed(job).Seed(rubric);
        return (job, rubric, uow);
    }

    private static CvJdAnalysis Cached(Guid jobId, Guid rubricId, string status, string? errorMessage = null) => new()
    {
        JobPostingId = jobId, RubricDocumentId = rubricId, CvHash = CvScoringService.ComputeHash(Bytes), Status = status,
        MatchScore = 77, Summary = "ok", SkillsMatched = "[]", SkillsGaps = "[]", RedFlags = "[]",
        ExperienceRelevance = "", OverallRecommendation = "Hire", AiModel = "Gemini", RawResponse = "{}", ErrorMessage = errorMessage,
    };

    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Run(new InMemoryUnitOfWork(), new RecordingFileStorage(), Guid.NewGuid());
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tài khoản ứng viên.", res.Error);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Theory]
    [InlineData(null)]   // UTCID02 — ProfileCvUrl null
    [InlineData("")]     // UTCID03 — ProfileCvUrl rỗng
    public async Task UTCID02_03_no_cv(string? profileCvUrl)
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl));
        var res = await Run(uow, new RecordingFileStorage(), Guid.NewGuid());
        Assert.True(res.IsSuccess);
        Assert.False(res.Value.HasCv);
        Assert.Equal("none", res.Value.Status);
    }

    [Theory]
    [InlineData(false)]  // UTCID04 — storage trả null
    [InlineData(true)]   // UTCID05 — storage trả mảng rỗng
    public async Task UTCID04_05_unreadable_cv(bool empty)
    {
        var (job, _, uow) = JobWithRubric();
        var storage = new RecordingFileStorage { FileBytes = empty ? Array.Empty<byte>() : null };
        var res = await Run(uow, storage, job.Id);
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.HasCv);
        Assert.False(res.Value.AiAvailable);
        Assert.Equal("failed", res.Value.Status);
        Assert.Equal("Không đọc được file CV đã lưu.", res.Value.Message);
    }

    [Fact]
    public async Task UTCID06_Cached_completed()
    {
        var (job, rubric, uow) = JobWithRubric();
        uow.Seed(Cached(job.Id, rubric.Id, CvAnalysisStatuses.Completed));
        var res = await Run(uow, new RecordingFileStorage { FileBytes = Bytes }, job.Id);
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.AiAvailable);
        Assert.Equal("completed", res.Value.Status);
        Assert.Equal(77, res.Value.Analysis!.MatchScore);
        Assert.Equal("Gemini", res.Value.Analysis.ReviewedBy);
    }

    [Fact]
    public async Task UTCID07_Cached_invalid_cv_with_message()
    {
        var (job, rubric, uow) = JobWithRubric();
        uow.Seed(Cached(job.Id, rubric.Id, CvAnalysisStatuses.InvalidCv, errorMessage: "File tải lên không phải là CV."));
        var res = await Run(uow, new RecordingFileStorage { FileBytes = Bytes }, job.Id);
        Assert.False(res.Value.AiAvailable);
        Assert.Equal("failed", res.Value.Status);
        Assert.Equal("File tải lên không phải là CV.", res.Value.Message);
    }

    [Fact]
    public async Task UTCID08_Cached_invalid_cv_without_message()
    {
        var (job, rubric, uow) = JobWithRubric();
        uow.Seed(Cached(job.Id, rubric.Id, CvAnalysisStatuses.InvalidCv, errorMessage: null));
        var res = await Run(uow, new RecordingFileStorage { FileBytes = Bytes }, job.Id);
        Assert.Equal("File CV không hợp lệ.", res.Value.Message);
    }

    [Fact]
    public async Task UTCID09_Scoring_in_progress_returns_processing()
    {
        var (job, rubric, uow) = JobWithRubric();
        var inFlight = new CvScoringInFlight();
        using var held = await inFlight.AcquireAsync(
            CvScoringInFlight.Key(job.Id, CvScoringService.ComputeHash(Bytes), rubric.Id), CancellationToken.None);

        var res = await Run(uow, new RecordingFileStorage { FileBytes = Bytes }, job.Id, inFlight);

        Assert.Equal("processing", res.Value.Status);
    }

    [Fact]
    public async Task UTCID10_Recent_failure_is_reported_without_rescoring()
    {
        var (job, rubric, uow) = JobWithRubric();
        var inFlight = new CvScoringInFlight();
        inFlight.RecordFailure(CvScoringInFlight.Key(job.Id, CvScoringService.ComputeHash(Bytes), rubric.Id), "AI thất bại");

        var res = await Run(uow, new RecordingFileStorage { FileBytes = Bytes }, job.Id, inFlight);

        Assert.Equal("failed", res.Value.Status);
        Assert.Equal("AI thất bại", res.Value.Message);
    }

    [Fact]
    public async Task UTCID11_No_result_starts_background_scoring()
    {
        var (job, _, uow) = JobWithRubric();
        var res = await Run(uow, new RecordingFileStorage { FileBytes = Bytes }, job.Id);
        Assert.True(res.IsSuccess);
        Assert.Equal("cv.pdf", res.Value.CvFileName);
        Assert.Equal("processing", res.Value.Status);
    }

    [Fact]
    public async Task UTCID12_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, new RecordingFileStorage(), Guid.NewGuid()));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID13_GetUrl_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { GetUrlThrows = new Exception("URL Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, storage, Guid.NewGuid()));
        Assert.Equal("URL Error", ex.Message);
    }

    [Fact]
    public async Task UTCID14_GetDownloadUrl_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { GetDownloadUrlThrows = new Exception("Download Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, storage, Guid.NewGuid()));
        Assert.Equal("Download Error", ex.Message);
    }

    [Fact]
    public async Task UTCID15_Read_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { ReadThrows = new Exception("Read Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, storage, Guid.NewGuid()));
        Assert.Equal("Read Error", ex.Message);
    }

    [Fact]
    public async Task UTCID16_Analysis_lookup_error()
    {
        var (job, _, uow) = JobWithRubric();
        uow.FailFindFor<CvJdAnalysis>("Analysis DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, new RecordingFileStorage { FileBytes = Bytes }, job.Id));
        Assert.Equal("Analysis DB Error", ex.Message);
    }

    /// <summary>ADR-070: tin chưa có bộ tiêu chí → không chấm, báo "chưa sẵn sàng", ứng viên vẫn ứng tuyển được.</summary>
    [Fact]
    public async Task UTCID17_Job_without_rubric_is_not_scored()
    {
        var job = CvScoringKit.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf")).Seed(job);
        var inFlight = new CvScoringInFlight();

        var res = await Run(uow, new RecordingFileStorage { FileBytes = Bytes }, job.Id, inFlight);

        Assert.Equal("rubric_pending", res.Value.Status);
        Assert.False(res.Value.AiAvailable);
        Assert.Contains("ứng tuyển", res.Value.Message);
    }
}
