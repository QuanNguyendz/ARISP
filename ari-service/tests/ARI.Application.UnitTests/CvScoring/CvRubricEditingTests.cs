using System.Collections.Generic;
using System.Linq;
using ARI.Application.CvScoring;
using ARI.Application.Playbooks;
using Xunit;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// Chuẩn hoá bộ tiêu chí chấm CV (ADR-070) — một cửa cho mọi đường nhập: phiếu, màn tin, Excel, AI.
/// </summary>
public class CvRubricEditingTests
{
    [Theory]
    [InlineData("Kinh nghiệm .NET & Cloud", "kinh_nghiem_net_cloud")]
    [InlineData("Đào tạo / Chứng chỉ", "dao_tao_chung_chi")]
    [InlineData("3 năm React", "tc_3_nam_react")]
    [InlineData("!!!", "tc_")]
    public void Slug_is_ascii_snake_case_and_valid(string name, string expectedPrefix)
    {
        var slug = CvRubricEditing.Slugify(name);
        Assert.StartsWith(expectedPrefix.TrimEnd('_'), slug);
        Assert.Matches("^[a-z][a-z0-9_]{1,39}$", slug);
    }

    [Fact]
    public void New_criteria_get_generated_unique_keys()
    {
        var res = CvRubricEditing.Normalize(new List<CvRubricCriterionInput>
        {
            new() { Name = "Kỹ năng", Weight = 50, Description = "a" },
            new() { Name = "Kỹ năng", Weight = 50, Description = "b" },
        }, RubricPurpose.Cv);

        Assert.True(res.IsValid, string.Join(";", res.Errors));
        Assert.Equal(new[] { "ky_nang", "ky_nang_2" }, res.Criteria.Select(c => c.Key));
    }

    /// <summary>Mã là khoá của điểm đã lưu — đổi tên tiêu chí cũ không được đổi mã.</summary>
    [Fact]
    public void Existing_keys_are_kept_when_renaming()
    {
        var res = CvRubricEditing.Normalize(new List<CvRubricCriterionInput>
        {
            new() { Key = "experience", Name = "Kinh nghiệm thực chiến", Weight = 60, Description = "a" },
            new() { Name = "Experience", Weight = 40, Description = "b" },
        }, RubricPurpose.Cv);

        Assert.True(res.IsValid, string.Join(";", res.Errors));
        Assert.Equal("experience", res.Criteria[0].Key);
        Assert.Equal("experience_2", res.Criteria[1].Key); // tiêu chí mới không cướp mã cũ
    }

    [Fact]
    public void Weights_must_total_100()
    {
        var res = CvRubricEditing.Normalize(CvScoringKit.Inputs(("A", 50), ("B", 40)), RubricPurpose.Cv);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.Contains("100"));
    }

    [Fact]
    public void Each_criterion_needs_a_scoring_guide_or_a_level()
    {
        var res = CvRubricEditing.Normalize(new List<CvRubricCriterionInput>
        {
            new() { Name = "Có neo", Weight = 50, Levels = new RubricLevels { Good = "tốt" } },
            new() { Name = "Trống trơn", Weight = 50 },
        }, RubricPurpose.Cv);

        Assert.False(res.IsValid);
        Assert.Single(res.Errors);
        Assert.Contains("Trống trơn", res.Errors[0]);
    }

    [Fact]
    public void Empty_levels_are_dropped_and_blank_rows_ignored()
    {
        var res = CvRubricEditing.Normalize(new List<CvRubricCriterionInput>
        {
            new() { Name = "A", Weight = 100, Description = "x", Levels = new RubricLevels { Good = "  " } },
            new() { Name = " ", Weight = 0 },
        }, RubricPurpose.Cv);

        Assert.True(res.IsValid, string.Join(";", res.Errors));
        Assert.Single(res.Criteria);
        Assert.Null(res.Criteria[0].Levels);
    }

    [Fact]
    public void Missing_name_is_reported()
    {
        var res = CvRubricEditing.Normalize(new List<CvRubricCriterionInput> { new() { Name = "", Weight = 100, Description = "x" } }, RubricPurpose.Cv);
        Assert.False(res.IsValid);
    }

    [Theory]
    [InlineData(new[] { 33.0, 33.0, 33.0 }, new[] { 34.0, 33.0, 33.0 })]
    [InlineData(new[] { 40.0, 40.0, 15.0 }, new[] { 42.0, 42.0, 16.0 })]
    [InlineData(new[] { 0.0, 0.0 }, new[] { 50.0, 50.0 })]
    public void Rebalance_sums_to_exactly_100(double[] input, double[] expected)
    {
        var rows = input.Select((w, i) => new CvRubricCriterionInput { Name = $"t{i}", Weight = (decimal)w }).ToList();
        CvRubricEditing.RebalanceWeights(rows);
        Assert.Equal(100m, rows.Sum(r => r.Weight));
        Assert.Equal(expected.Select(e => (decimal)e), rows.Select(r => r.Weight));
    }

    [Fact]
    public void Levels_reach_the_prompt_text()
    {
        var text = ScoringRubric.ToPromptText(new List<RubricCriterion>
        {
            new() { Key = "exp", Name = "Kinh nghiệm", Weight = 100, Levels = new RubricLevels { Excellent = "≥4 năm", Poor = "không có" } },
        });

        Assert.Contains("90–100: ≥4 năm", text);
        Assert.Contains("0–39: không có", text);
        Assert.DoesNotContain("70–89", text);
    }

    [Fact]
    public void Sheet_round_trips_levels_and_generates_missing_keys()
    {
        var criteria = new List<RubricCriterion>
        {
            new() { Key = "", Name = "Kinh nghiệm", Weight = 60, Description = "d", Levels = new RubricLevels { Good = "tốt" } },
            new() { Key = "skills", Name = "Kỹ năng", Weight = 40, Description = "e" },
        };

        var parsed = RubricSheet.Parse(RubricSheet.Build(criteria));

        Assert.Empty(parsed.Errors);
        Assert.Equal("kinh_nghiem", parsed.Criteria[0].Key);
        Assert.Equal("tốt", parsed.Criteria[0].Levels!.Good);
        Assert.Equal("skills", parsed.Criteria[1].Key);
        Assert.Null(parsed.Criteria[1].Levels);
        Assert.Empty(ScoringRubric.Validate(parsed.Criteria, RubricPurpose.Cv));
    }

    [Fact]
    public void Cv_template_parses_into_a_valid_rubric()
    {
        var parsed = RubricSheet.Parse(RubricSheet.BuildTemplate(forCv: true));
        var normalized = CvRubricEditing.Normalize(CvRubricEditing.ToInput(parsed.Criteria), RubricPurpose.Cv);
        Assert.True(normalized.IsValid, string.Join(";", normalized.Errors));
        // 4 tiêu chí chấm điểm + 1 dòng ví dụ điều kiện bắt buộc (ADR-075), cùng ví dụ điểm tối thiểu và ý ×2.
        Assert.Equal(5, normalized.Criteria.Count);
        Assert.Single(normalized.Criteria, c => c.IsKnockout);
        Assert.Contains(normalized.Criteria, c => c.MinScore == 50);
        Assert.Contains(normalized.Criteria, c => c.Checks?.Any(x => x.Weight == 2) == true);
        Assert.NotNull(parsed.Policy);
        Assert.True(parsed.Policy!.IsDefault);
    }
}
