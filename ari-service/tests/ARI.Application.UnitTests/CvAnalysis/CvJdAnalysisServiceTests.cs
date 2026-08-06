using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CvAnalysis;

/// <summary>
/// CV-JD Match Analysis (Gemini, ADR-030) — <see cref="CvJdAnalysisService"/>.
/// Chốt: chạy 1 lần / (job + CvHash) rồi TÁI DÙNG (không gọi lại Gemini), CV không hợp lệ lưu bản "failed",
/// lỗi AI không persist, các truy vấn theo id/application, kiểm tra sở hữu, và xoá cache.
/// </summary>
public class CvJdAnalysisServiceTests
{
    private const string CvContent = "PDF-BINARY-CONTENT";

    private static MemoryStream Cv(string content = CvContent) => new(Encoding.UTF8.GetBytes(content));

    /// <summary>MD5 hex lowercase — khớp hệt <c>ComputeFileHash</c> của service để seed cache hit.</summary>
    private static string Md5Hex(string content)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static CvJdAnalysisService NewService(InMemoryUnitOfWork uow, FakeGeminiProvider gemini, FakeDocumentParser? parser = null)
        => new(uow, gemini, parser ?? new FakeDocumentParser());

    private static JobPosting Job() => new() { CreatedByUserId = Guid.NewGuid(), Title = "Backend Developer" };

    private static CvJdAnalysisResultDto ValidDto() => new()
    {
        IsValidCv = true,
        MatchScore = 82,
        Summary = "Ứng viên phù hợp",
        SkillsMatched = { "C#", "PostgreSQL" },
        SkillsGaps = { "Kubernetes" },
        ExperienceRelevance = "5 năm backend",
        OverallRecommendation = "Nên phỏng vấn",
        Provider = "Gemini",
        RawResponse = "{}",
    };

    // ---------- AnalyzeAndCacheAsync ----------

    [Fact]
    public async Task Job_not_found_returns_failure_without_calling_gemini()
    {
        var uow = new InMemoryUnitOfWork();
        var gemini = new FakeGeminiProvider();

        var res = await NewService(uow, gemini).AnalyzeAndCacheAsync(Guid.NewGuid(), Cv(), "cv.pdf");

        Assert.True(res.IsFailure);
        Assert.Contains("Không tìm thấy tin tuyển dụng", res.Error);
        Assert.Equal(0, gemini.AnalyzeCallCount);
    }

    [Fact]
    public async Task Valid_cv_is_analyzed_and_persisted_as_completed()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var gemini = new FakeGeminiProvider { AnalyzeResult = Result.Success(ValidDto()) };

        var res = await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.True(res.IsSuccess);
        Assert.Equal(1, gemini.AnalyzeCallCount);
        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal("completed", saved.Status);
        Assert.Equal(82, saved.MatchScore);
        Assert.Equal("Gemini", saved.AiModel);
        Assert.Equal(Md5Hex(CvContent), saved.CvHash);
        Assert.Contains("C#", JsonSerializer.Deserialize<string[]>(saved.SkillsMatched)!);
        Assert.Contains("Kubernetes", JsonSerializer.Deserialize<string[]>(saved.SkillsGaps)!);
    }

    [Fact]
    public async Task Invalid_cv_persists_failed_record_and_returns_failure()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var gemini = new FakeGeminiProvider
        {
            AnalyzeResult = Result.Success(new CvJdAnalysisResultDto { IsValidCv = false, Summary = "not a cv", Provider = "Gemini" }),
        };

        var res = await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.True(res.IsFailure);
        Assert.Contains("không phải là một CV hợp lệ", res.Error);
        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal("failed", saved.Status);
        Assert.Equal(0, saved.MatchScore);
        Assert.False(string.IsNullOrEmpty(saved.ErrorMessage));
    }

    [Fact]
    public async Task Gemini_failure_returns_error_and_persists_nothing()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var gemini = new FakeGeminiProvider { AnalyzeResult = Result.Failure<CvJdAnalysisResultDto>("quota exceeded") };

        var res = await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.True(res.IsFailure);
        Assert.Contains("Lỗi AI", res.Error);
        Assert.Empty(uow.Repo<CvJdAnalysis>().Items);
    }

    [Fact]
    public async Task Completed_analysis_with_same_hash_is_reused_without_calling_gemini()
    {
        var job = Job();
        var existing = new CvJdAnalysis
        {
            JobPostingId = job.Id,
            CvHash = Md5Hex(CvContent),
            Status = "completed",
            MatchScore = 90,
            RawResponse = "{}",
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(existing);
        var gemini = new FakeGeminiProvider();

        var res = await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.True(res.IsSuccess);
        Assert.Equal(existing.Id, res.Value.Id);          // trả bản đã có
        Assert.Equal(0, gemini.AnalyzeCallCount);         // KHÔNG gọi lại AI
        Assert.Single(uow.Repo<CvJdAnalysis>().Items);    // không tạo bản mới
    }

    [Fact]
    public async Task Second_call_for_same_cv_hits_cache()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var gemini = new FakeGeminiProvider { AnalyzeResult = Result.Success(ValidDto()) };
        var service = NewService(uow, gemini);

        await service.AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");
        await service.AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.Equal(1, gemini.AnalyzeCallCount);         // lần 2 dùng cache
        Assert.Single(uow.Repo<CvJdAnalysis>().Items);
    }

    [Fact]
    public async Task Failed_analysis_does_not_cache_and_reruns_gemini()
    {
        var job = Job();
        var existingFailed = new CvJdAnalysis
        {
            JobPostingId = job.Id,
            CvHash = Md5Hex(CvContent),
            Status = "failed",
            RawResponse = "{}",
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(existingFailed);
        var gemini = new FakeGeminiProvider { AnalyzeResult = Result.Success(ValidDto()) };

        var res = await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.True(res.IsSuccess);
        Assert.Equal(1, gemini.AnalyzeCallCount);         // bản "failed" không chặn → chạy lại
        Assert.Equal(2, uow.Repo<CvJdAnalysis>().Items.Count);
    }

    [Fact]
    public async Task Cache_hit_enriches_reasoning_from_raw_response()
    {
        var job = Job();
        var inner = "{\"analysis_reasoning\":\"R\",\"seniority_alignment\":\"S\",\"tech_depth_analysis\":\"T\"}";
        var envelope = new { candidates = new[] { new { content = new { parts = new[] { new { text = "```json" + inner + "```" } } } } } };
        var existing = new CvJdAnalysis
        {
            JobPostingId = job.Id,
            CvHash = Md5Hex(CvContent),
            Status = "completed",
            RawResponse = JsonSerializer.Serialize(envelope),
        };
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(existing);

        var res = await NewService(uow, new FakeGeminiProvider()).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.True(res.IsSuccess);
        Assert.Equal("R", res.Value.AnalysisReasoning);
        Assert.Equal("S", res.Value.SeniorityAlignment);
        Assert.Equal("T", res.Value.TechDepthAnalysis);
    }

    // ---------- Truy vấn ----------

    [Fact]
    public async Task GetById_returns_found_or_failure()
    {
        var analysis = new CvJdAnalysis { JobPostingId = Guid.NewGuid() };
        var uow = new InMemoryUnitOfWork().Seed(analysis);
        var service = NewService(uow, new FakeGeminiProvider());

        Assert.True((await service.GetAnalysisByIdAsync(analysis.Id)).IsSuccess);
        Assert.True((await service.GetAnalysisByIdAsync(Guid.NewGuid())).IsFailure);
    }

    [Fact]
    public async Task GetByApplication_resolves_linked_analysis()
    {
        var analysis = new CvJdAnalysis { JobPostingId = Guid.NewGuid() };
        var app = new ARI.Domain.Entities.Application { CvJdAnalysisId = analysis.Id, CandidateEmail = "c@x.io" };
        var uow = new InMemoryUnitOfWork().Seed(analysis).Seed(app);

        var res = await NewService(uow, new FakeGeminiProvider()).GetAnalysisByApplicationIdAsync(app.Id);

        Assert.True(res.IsSuccess);
        Assert.Equal(analysis.Id, res.Value.Id);
    }

    [Fact]
    public async Task GetByApplication_without_link_fails()
    {
        var app = new ARI.Domain.Entities.Application { CvJdAnalysisId = null, CandidateEmail = "c@x.io" };
        var uow = new InMemoryUnitOfWork().Seed(app);

        var res = await NewService(uow, new FakeGeminiProvider()).GetAnalysisByApplicationIdAsync(app.Id);

        Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task CheckOwnership_true_only_when_application_links_analysis_and_account()
    {
        var analysisId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var app = new ARI.Domain.Entities.Application { CvJdAnalysisId = analysisId, CandidateAccountId = accountId };
        var uow = new InMemoryUnitOfWork().Seed(app);
        var service = NewService(uow, new FakeGeminiProvider());

        Assert.True(await service.CheckCandidateOwnershipAsync(analysisId, accountId));
        Assert.False(await service.CheckCandidateOwnershipAsync(analysisId, Guid.NewGuid())); // account khác
        Assert.False(await service.CheckCandidateOwnershipAsync(Guid.NewGuid(), accountId));  // analysis khác
    }

    [Fact]
    public async Task ClearAllCache_deletes_every_analysis()
    {
        var uow = new InMemoryUnitOfWork().Seed(
            new CvJdAnalysis(), new CvJdAnalysis(), new CvJdAnalysis());

        await NewService(uow, new FakeGeminiProvider()).ClearAllCacheAsync();

        Assert.Empty(uow.Repo<CvJdAnalysis>().Items);
    }
}
