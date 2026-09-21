using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.Playbooks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;
using static ARI.Application.UnitTests.CvScoring.CvScoringKit;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// Ý kiểm quyết định điểm TRONG dải: AI chọn dải + trả lời có/không từng ý, backend tính
/// <c>đáy + (ý đạt ÷ ý đã trả lời) × (đỉnh − đáy)</c>. Hai CV cùng dải chênh nhau luôn chỉ ra được là khác ở ý nào.
/// </summary>
public class CvChecklistScoringTests
{
    private static RubricCriterion Criterion(params string[] checks) => new()
    {
        Key = "kinh_nghiem",
        Name = "Kinh nghiệm",
        Weight = 100,
        Description = "chuẩn",
        Checks = checks.Select((text, i) => new RubricCheck { Key = $"k{i + 1}", Text = text }).ToList(),
    };

    private static CvCriterionAiResult Ai(string? band, decimal? score = null, params (string Key, bool? Met, string? Evidence)[] checks) => new()
    {
        Key = "kinh_nghiem",
        Band = band,
        Score = score,
        Evidence = "trích CV",
        Reasoning = "lý do",
        Checks = checks.Select(c => new CvCheckAiResult { Key = c.Key, Met = c.Met, Evidence = c.Evidence }).ToList(),
    };

    // ---------------- Công thức vị trí trong dải ----------------

    [Theory]
    [InlineData("excellent", 2, 4, 95)] // 90 + 2/4 × 10
    [InlineData("excellent", 0, 4, 90)] // đạt dải nhưng không có ý cộng → đáy dải
    [InlineData("excellent", 4, 4, 100)]
    [InlineData("good", 2, 4, 80)]      // 70 + 2/4 × 19 = 79,5 → 80
    [InlineData("good", 1, 3, 76)]      // 70 + 1/3 × 19 = 76,33 → 76
    [InlineData("fair", 3, 4, 62)]      // 40 + 3/4 × 29 = 61,75 → 62
    [InlineData("poor", 1, 4, 10)]      // 0 + 1/4 × 39 = 9,75 → 10
    public void Score_inside_the_band_comes_from_the_share_of_checks_met(string band, int met, int total, int expected)
    {
        var texts = Enumerable.Range(1, total).Select(i => $"ý {i}").ToArray();
        var answers = Enumerable.Range(1, total)
            .Select(i => ($"k{i}", (bool?)(i <= met), i <= met ? $"bằng chứng {i}" : null)).ToArray();

        var outcome = CvCriterionScoring.Resolve(Criterion(texts), Ai(band, null, answers));

        Assert.Equal(expected, outcome.Score);
        Assert.Equal(band, outcome.Band);
        Assert.Equal(CvCriterionScoring.SourceChecklist, outcome.Source);
        Assert.Equal(met, outcome.Met);
        Assert.Equal(total, outcome.Answered);
    }

    [Fact]
    public void Two_cvs_in_the_same_band_differ_exactly_by_the_checks_they_meet()
    {
        var criterion = Criterion("≥ 4 năm .NET", "Dẫn dắt kỹ thuật", "Hệ thống lớn", "Có số liệu kết quả");

        var a = CvCriterionScoring.Resolve(criterion, Ai("excellent", 97, ("k1", true, "5 năm"), ("k2", false, null), ("k3", false, null), ("k4", false, null)));
        var b = CvCriterionScoring.Resolve(criterion, Ai("excellent", 91, ("k1", true, "4 năm"), ("k2", true, "tech lead"), ("k3", true, "2 triệu người dùng"), ("k4", false, null)));

        // Số AI tự đưa ra (97 / 91) bị bỏ qua — thứ tự do ý kiểm quyết định.
        Assert.Equal(93, a.Score); // 90 + 1/4 × 10 = 92,5 → 93
        Assert.Equal(98, b.Score); // 90 + 3/4 × 10 = 97,5 → 98
    }

    [Fact]
    public void A_check_marked_met_without_a_quote_does_not_count()
    {
        var outcome = CvCriterionScoring.Resolve(Criterion("ý 1", "ý 2"),
            Ai("excellent", null, ("k1", true, "   "), ("k2", true, "có trích dẫn")));

        Assert.Equal(95, outcome.Score); // chỉ 1/2 ý được tính
        var unsupported = Assert.Single(outcome.Checks, c => c.Key == "k1");
        Assert.False(unsupported.Met);
        Assert.True(unsupported.Unsupported);
    }

    [Fact]
    public void Unanswered_checks_are_left_out_of_the_share()
    {
        var outcome = CvCriterionScoring.Resolve(Criterion("ý 1", "ý 2", "ý 3", "ý 4"),
            Ai("good", null, ("k1", true, "x"), ("k2", false, null))); // k3, k4 không trả lời

        Assert.Equal(2, outcome.Answered);
        Assert.Equal(80, outcome.Score); // 70 + 1/2 × 19 = 79,5 → 80
        Assert.Null(outcome.Checks.Single(c => c.Key == "k3").Met);
    }

    [Fact]
    public void Without_a_checklist_the_ai_estimate_is_clamped_into_its_band()
    {
        var noChecks = Criterion();

        Assert.Equal(89m, CvCriterionScoring.Resolve(noChecks, Ai("good", 95)).Score);   // kẹp về đỉnh dải
        Assert.Equal(CvCriterionScoring.SourceAi, CvCriterionScoring.Resolve(noChecks, Ai("good", 95)).Source);
        Assert.Equal(84m, CvCriterionScoring.Resolve(noChecks, Ai(null, 84)).Score);     // không có dải → suy từ số
        Assert.Equal("good", CvCriterionScoring.Resolve(noChecks, Ai(null, 84)).Band);
        Assert.Equal(90m, CvCriterionScoring.Resolve(noChecks, Ai("90–100", 70)).Score); // dải viết kiểu khoảng số
    }

    [Fact]
    public void Without_a_band_the_checklist_positions_inside_the_band_implied_by_the_ai_estimate()
    {
        // AI quên ghi dải nhưng có số 72 → dải "good" (70–89); vị trí trong dải vẫn do ý kiểm: đạt 1/1 → 89.
        var outcome = CvCriterionScoring.Resolve(Criterion("ý 1"), Ai(null, 72, ("k1", true, "x")));

        Assert.Equal(89m, outcome.Score);
        Assert.Equal("good", outcome.Band);
        Assert.Equal(CvCriterionScoring.SourceChecklist, outcome.Source);
    }

    [Fact]
    public void Nothing_usable_means_the_criterion_is_excluded()
    {
        Assert.Null(CvCriterionScoring.Resolve(Criterion("ý 1"), Ai("excellent")).Score);
        Assert.Null(CvCriterionScoring.Resolve(Criterion("ý 1"), null).Score);
    }

    [Fact]
    public void Lenient_booleans_from_the_model_are_understood()
    {
        const string json = @"{""is_valid_cv"":true,""criteria"":[{""key"":""a"",""band"":""good"",""checks"":[
            {""key"":""k1"",""met"":""có"",""evidence"":""x""},{""key"":""k2"",""met"":0},{""key"":""k3"",""met"":""maybe""}]}]}";

        var dto = JsonSerializer.Deserialize<CvJdAnalysisResultDto>(json)!;
        var checks = dto.Criteria[0].Checks!;

        Assert.True(checks[0].Met);
        Assert.False(checks[1].Met);
        Assert.Null(checks[2].Met);
    }

    // ---------------- Chuẩn hoá · Excel · prompt ----------------

    [Fact]
    public void Checks_are_trimmed_deduplicated_and_keyed_while_existing_keys_are_kept()
    {
        var input = new List<CvRubricCriterionInput>
        {
            new()
            {
                Name = "Kinh nghiệm", Weight = 100, Description = "d",
                Checks = new()
                {
                    new() { Key = "k2", Text = "  Dẫn dắt kỹ thuật " },
                    new() { Text = "≥ 4 năm .NET" },
                    new() { Text = "≥ 4 NĂM .net" },  // trùng chữ → bỏ
                    new() { Text = "   " },            // trống → bỏ
                    new() { Key = "k2", Text = "Hệ thống lớn" }, // trùng mã → sinh mã mới
                },
            },
        };

        var normalized = CvRubricEditing.Normalize(input, RubricPurpose.Cv);

        Assert.True(normalized.IsValid, string.Join(";", normalized.Errors));
        var checks = normalized.Criteria[0].Checks!;
        Assert.Equal(new[] { "k2", "k1", "k3" }, checks.Select(c => c.Key));
        Assert.Equal(new[] { "Dẫn dắt kỹ thuật", "≥ 4 năm .NET", "Hệ thống lớn" }, checks.Select(c => c.Text));
    }

    [Fact]
    public void Too_many_checks_are_refused()
    {
        var input = new List<CvRubricCriterionInput>
        {
            new()
            {
                Name = "Kinh nghiệm", Weight = 100, Description = "d",
                Checks = Enumerable.Range(1, ScoringRubric.MaxChecks + 1).Select(i => new RubricCheck { Text = $"ý {i}" }).ToList(),
            },
        };

        var normalized = CvRubricEditing.Normalize(input, RubricPurpose.Cv);

        Assert.False(normalized.IsValid);
        Assert.Contains(normalized.Errors, e => e.Contains("ý kiểm"));
    }

    [Fact]
    public void Checks_round_trip_through_the_excel_sheet_one_per_line()
    {
        var criteria = new List<RubricCriterion>
        {
            new()
            {
                Key = "kinh_nghiem", Name = "Kinh nghiệm", Weight = 100, Description = "d",
                Checks = new() { new() { Key = "k1", Text = "≥ 4 năm .NET" }, new() { Key = "k2", Text = "Dẫn dắt kỹ thuật" } },
            },
        };

        var parsed = RubricSheet.Parse(RubricSheet.Build(criteria));

        Assert.Empty(parsed.Errors);
        Assert.Equal(new[] { "≥ 4 năm .NET", "Dẫn dắt kỹ thuật" }, parsed.Criteria[0].Checks!.Select(c => c.Text));
        Assert.Equal(new[] { "k1", "k2" }, parsed.Criteria[0].Checks!.Select(c => c.Key));
        Assert.Empty(ScoringRubric.Validate(parsed.Criteria, RubricPurpose.Cv));
    }

    [Fact]
    public void The_cv_template_ships_with_checks()
    {
        var parsed = RubricSheet.Parse(RubricSheet.BuildTemplate(forCv: true));
        Assert.True(parsed.Criteria[0].Checks is { Count: > 0 });
    }

    [Fact]
    public void Check_keys_and_texts_reach_the_prompt()
    {
        var text = ScoringRubric.ToPromptText(new List<RubricCriterion> { Criterion("≥ 4 năm .NET", "Dẫn dắt kỹ thuật") });

        Assert.Contains("[k1] ≥ 4 năm .NET", text);
        Assert.Contains("[k2] Dẫn dắt kỹ thuật", text);
    }

    [Fact]
    public async Task Suggested_checks_reach_the_draft()
    {
        var gemini = new FakeGeminiProvider
        {
            SuggestResult = Result.Success(new List<CvRubricSuggestionItem>
            {
                new() { Name = "Kinh nghiệm", Weight = 100, Description = "d", Checks = new() { "≥ 3 năm C#", "Có số liệu kết quả" } },
            }),
        };

        var res = await new SuggestCvRubricCommandHandler(gemini).Handle(
            new SuggestCvRubricCommand(new CvRubricSuggestionInput("Backend", "mô tả", null, null, null)), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Equal(new[] { "k1", "k2" }, res.Value!.Criteria[0].Checks!.Select(c => c.Key));
    }

    // ---------------- Từ đầu tới cuối: chấm → ảnh chụp → bảng giải thích ----------------

    [Fact]
    public async Task Checklist_scores_are_saved_and_explained_with_the_band_and_each_answer()
    {
        var job = Job();
        var criteria = new List<RubricCriterion>
        {
            new()
            {
                Key = "experience", Name = "Kinh nghiệm", Weight = 60, Description = "d",
                Checks = new()
                {
                    new() { Key = "k1", Text = "≥ 4 năm .NET" }, new() { Key = "k2", Text = "Dẫn dắt kỹ thuật" },
                    new() { Key = "k3", Text = "Hệ thống lớn" }, new() { Key = "k4", Text = "Có số liệu" },
                },
            },
            new() { Key = "education", Name = "Học vấn", Weight = 40, Description = "e" },
        };
        var rubric = Rubric(job.Id);
        rubric.RubricJson = ScoringRubric.Serialize(criteria);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric);

        var gemini = new FakeGeminiProvider
        {
            AnalyzeResult = Result.Success(new CvJdAnalysisResultDto
            {
                IsValidCv = true, Summary = "ok", Provider = "Gemini", RawResponse = "{}",
                Criteria =
                {
                    new()
                    {
                        Key = "experience", Band = "excellent", Score = null, Evidence = "5 năm", Reasoning = "r",
                        Checks = new()
                        {
                            new() { Key = "k1", Met = true, Evidence = "2019–2025 .NET" },
                            new() { Key = "k2", Met = true, Evidence = "Tech lead" },
                            new() { Key = "k3", Met = false },
                            new() { Key = "k4", Met = true }, // không trích dẫn → không tính
                        },
                    },
                    new() { Key = "education", Band = "good", Score = 75, Evidence = "Đại học", Reasoning = "r" },
                },
            }),
        };

        var analysis = (await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf")).Value!;

        // experience: 90 + 2/4 × 10 = 95 · education: 75 → (95×60 + 75×40) ÷ 100 = 87
        Assert.Equal(87, analysis.MatchScore);

        var app = new ARI.Domain.Entities.Application { JobPostingId = job.Id, CvFileUrl = "cv/a.pdf", CvJdAnalysisId = analysis.Id };
        var dto = await CvScoreBreakdownBuilder.BuildAsync(uow, app, analysis, CancellationToken.None);

        var exp = dto.Criteria.Single(c => c.Key == "experience");
        Assert.Equal(95m, exp.Score);
        Assert.Equal("excellent", exp.Band);
        Assert.Equal(90m, exp.BandMin);
        Assert.Equal(100m, exp.BandMax);
        Assert.Equal(CvCriterionScoring.SourceChecklist, exp.ScoreSource);
        Assert.Equal(2, exp.ChecksMet);
        Assert.Equal(4, exp.ChecksAnswered);
        Assert.Equal("2019–2025 .NET", exp.Checks.Single(c => c.Key == "k1").Evidence);
        Assert.True(exp.Checks.Single(c => c.Key == "k4").Unsupported);

        var edu = dto.Criteria.Single(c => c.Key == "education");
        Assert.Equal(CvCriterionScoring.SourceAi, edu.ScoreSource);
        Assert.Empty(edu.Checks);
    }

    [Fact]
    public async Task Rubric_checks_reach_the_ai_request()
    {
        var job = Job();
        var rubric = Rubric(job.Id);
        rubric.RubricJson = ScoringRubric.Serialize(new List<RubricCriterion> { Criterion("≥ 4 năm .NET") });
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric);
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("kinh_nghiem", 80)) };

        await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.Contains("[k1] ≥ 4 năm .NET", gemini.LastRequest!.RubricInstruction);
    }
}
