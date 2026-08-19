using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Playbooks;
using ARI.Application.Services;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CvAnalysis;

/// <summary>
/// Chấm CV–JD theo bộ tiêu chí của doanh nghiệp (ADR-060).
///
/// Trước đây khi tin không khai rubric, prompt rơi về câu viết cứng "Kinh nghiệm 40%, Kỹ năng 40%,
/// Học vấn 20%" và <c>match_score</c> là con số Gemini tự đưa — không suy ra từ tiêu chí nào cả.
/// Nay backend cộng có trọng số từ điểm từng tiêu chí; không khai rubric thì giữ nguyên hành vi cũ.
/// </summary>
public class CvRubricScoringTests
{
    private static MemoryStream Cv() => new(Encoding.UTF8.GetBytes("PDF-BINARY-CONTENT"));

    private static CvJdAnalysisService NewService(InMemoryUnitOfWork uow, FakeGeminiProvider gemini)
        => new(uow, gemini, new FakeDocumentParser());

    private static JobPosting Job() => new() { CreatedByUserId = Guid.NewGuid(), Title = "Backend Developer" };

    private static PlaybookDocument CvRubric(Guid jobId, params (string Key, string Name, decimal Weight)[] rows)
        => new()
        {
            Scope = PlaybookScope.ScopeJobPosting,
            ScopeRefId = jobId,
            DocumentType = ScoringRubric.TypeCvRubric,
            FileName = "cv-rubric.xlsx",
            UploadedByUserId = Guid.NewGuid(),
            RubricJson = ScoringRubric.Serialize(
                rows.Select(r => new RubricCriterion { Key = r.Key, Name = r.Name, Weight = r.Weight }).ToList()),
        };

    private static FakeGeminiProvider Gemini(int matchScore, Dictionary<string, decimal>? criterionScores = null)
        => new()
        {
            AnalyzeResult = Result.Success(new CvJdAnalysisResultDto
            {
                IsValidCv = true,
                MatchScore = matchScore,
                Summary = "Ứng viên phù hợp",
                Provider = "Gemini",
                RawResponse = "{}",
                CriterionScores = criterionScores,
            }),
        };

    [Fact]
    public async Task Match_score_is_recomputed_from_criteria_ignoring_the_model_number()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(CvRubric(job.Id, ("experience", "Kinh nghiệm", 70), ("education", "Học vấn", 30)));
        // Gemini tự khai 95 — bỏ qua; 80*0.7 + 40*0.3 = 68.
        var gemini = Gemini(95, new Dictionary<string, decimal> { ["experience"] = 80, ["education"] = 40 });

        var res = await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.True(res.IsSuccess);
        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal(68, saved.MatchScore);
    }

    [Fact]
    public async Task Rubric_reaches_the_prompt_with_names_and_weights()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(CvRubric(job.Id, ("experience", "Kinh nghiệm liên quan", 70), ("education", "Học vấn", 30)));
        var gemini = Gemini(80, new Dictionary<string, decimal> { ["experience"] = 80 });

        await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.NotNull(gemini.LastRubricInstruction);
        Assert.Contains("experience", gemini.LastRubricInstruction);
        Assert.Contains("Kinh nghiệm liên quan", gemini.LastRubricInstruction);
        Assert.Contains("70", gemini.LastRubricInstruction);
    }

    [Fact]
    public async Task Saved_snapshot_keeps_label_and_weight()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(CvRubric(job.Id, ("experience", "Kinh nghiệm", 70), ("education", "Học vấn", 30)));
        var gemini = Gemini(95, new Dictionary<string, decimal> { ["experience"] = 80, ["education"] = 40 });

        await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        using var doc = JsonDocument.Parse(saved.CriterionScores!);
        var exp = doc.RootElement.GetProperty("experience");
        Assert.Equal(80m, exp.GetProperty("score").GetDecimal());
        Assert.Equal("Kinh nghiệm", exp.GetProperty("label").GetString());
        Assert.Equal(70m, exp.GetProperty("weight").GetDecimal());
    }

    /// <summary>Không khai rubric → không nhồi chỉ dẫn, giữ nguyên điểm của Gemini (hành vi trước ADR-060).</summary>
    [Fact]
    public async Task Without_a_rubric_nothing_changes()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var gemini = Gemini(82);

        await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.Null(gemini.LastRubricInstruction);
        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal(82, saved.MatchScore);
        Assert.Equal("{}", saved.CriterionScores);
    }

    /// <summary>Có rubric mà AI không chấm tiêu chí nào → giữ điểm AI, không cho 0.</summary>
    [Fact]
    public async Task Rubric_present_but_no_criterion_scored_keeps_the_ai_score()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(CvRubric(job.Id, ("experience", "Kinh nghiệm", 100)));
        var gemini = Gemini(77, new Dictionary<string, decimal>());

        await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal(77, saved.MatchScore);
    }

    /// <summary>Bộ tiêu chí PHỎNG VẤN không được dùng để chấm CV — hai thước đo tách rời.</summary>
    [Fact]
    public async Task Interview_rubric_is_not_used_for_cv_scoring()
    {
        var job = Job();
        var interviewRubric = CvRubric(job.Id, ("technical", "Chuyên môn", 100));
        interviewRubric.DocumentType = ScoringRubric.TypeInterviewRubric;
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(interviewRubric);
        var gemini = Gemini(82, new Dictionary<string, decimal> { ["technical"] = 20 });

        await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.Null(gemini.LastRubricInstruction);
        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal(82, saved.MatchScore);
    }

    /// <summary>File không phải CV vẫn lưu bản "failed" với điểm 0 — rubric không đổi được điều đó.</summary>
    [Fact]
    public async Task Invalid_cv_still_fails_even_with_a_rubric()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job)
            .Seed(CvRubric(job.Id, ("experience", "Kinh nghiệm", 100)));
        var gemini = new FakeGeminiProvider
        {
            AnalyzeResult = Result.Success(new CvJdAnalysisResultDto
            {
                IsValidCv = false,
                Summary = "không phải CV",
                Provider = "Gemini",
                CriterionScores = new Dictionary<string, decimal> { ["experience"] = 90 },
            }),
        };

        var res = await NewService(uow, gemini).AnalyzeAndCacheAsync(job.Id, Cv(), "cv.pdf");

        Assert.True(res.IsFailure);
        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal("failed", saved.Status);
        Assert.Equal(0, saved.MatchScore);
    }
}
