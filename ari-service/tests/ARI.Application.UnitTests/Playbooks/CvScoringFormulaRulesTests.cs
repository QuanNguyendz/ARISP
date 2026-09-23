using System.Collections.Generic;
using System.Linq;
using ARI.Application.CvScoring;
using ARI.Application.Playbooks;
using Xunit;

namespace ARI.Application.UnitTests.Playbooks;

/// <summary>
/// Luật của công thức chấm CV do HM quyết định (ADR-075): kiểm công thức cấp tin, luật tiêu chí theo mục đích
/// (CV / phỏng vấn), chuẩn hoá, prompt không có con số, chữ ký "AI được hỏi gì".
/// </summary>
public class CvScoringFormulaRulesTests
{
    private static RubricCriterion C(string key, decimal weight, string? kind = null, int? min = null, string desc = "chuẩn")
        => new() { Key = key, Name = key, Weight = weight, Kind = kind, MinScore = min, Description = desc };

    // ---------------- Công thức cấp tin ----------------

    [Fact]
    public void Default_policy_is_stored_as_null_and_read_back_as_default()
    {
        Assert.Null(CvScoringPolicy.ToStorage(CvScoringPolicy.Default));
        Assert.Null(CvScoringPolicy.ToStorage(null));
        Assert.True(CvScoringPolicy.FromStorage(null).IsDefault);
        Assert.True(CvScoringPolicy.FromStorage("không phải json").IsDefault);
    }

    [Fact]
    public void Custom_policy_round_trips_and_compares_by_meaning()
    {
        var p = new CvScoringPolicy
        {
            Bands = new CvBandCuts { ExcellentFrom = 85, GoodFrom = 65, FairFrom = 35 },
            Tiers = new CvTierCuts { StrongHireFrom = 85, HireFrom = 70, CautionFrom = 55 },
        };

        var stored = CvScoringPolicy.ToStorage(p)!;
        Assert.True(CvScoringPolicy.FromStorage(stored).SameAs(p));
        // jsonb của Postgres sắp lại khoá + thêm khoảng trắng — vẫn phải là "không đổi".
        const string reformatted = "{\"tiers\": {\"hireFrom\": 70, \"cautionFrom\": 55, \"strongHireFrom\": 85}, \"bands\": {\"fairFrom\": 35, \"goodFrom\": 65, \"excellentFrom\": 85}}";
        Assert.True(CvScoringPolicy.FromStorage(reformatted).SameAs(p));
        Assert.Empty(CvScoringPolicy.Validate(p));
    }

    [Theory]
    [InlineData(90, 90, 40, "giảm dần")]        // hai ngưỡng trùng
    [InlineData(70, 80, 40, "giảm dần")]        // đảo thứ tự
    [InlineData(100, 70, 40, "giảm dần")]       // dải Xuất sắc rỗng
    [InlineData(97, 70, 40, "ít nhất")]         // Xuất sắc 97–100 quá hẹp
    [InlineData(90, 86, 40, "ít nhất")]         // Tốt 86–89 quá hẹp
    [InlineData(90, 70, 3, "ít nhất")]          // Chưa đạt 0–2 quá hẹp
    public void Bad_band_cuts_are_rejected(int excellent, int good, int fair, string fragment)
    {
        var errors = CvScoringPolicy.Validate(new CvScoringPolicy
        {
            Bands = new CvBandCuts { ExcellentFrom = excellent, GoodFrom = good, FairFrom = fair },
        });
        Assert.Contains(errors, e => e.Contains(fragment));
    }

    [Theory]
    [InlineData(80, 80, 50)]
    [InlineData(101, 65, 50)]
    [InlineData(80, 65, 0)]
    public void Bad_tiers_are_rejected(int strong, int hire, int caution)
        => Assert.NotEmpty(CvScoringPolicy.Validate(new CvScoringPolicy
        {
            Tiers = new CvTierCuts { StrongHireFrom = strong, HireFrom = hire, CautionFrom = caution },
        }));

    // ---------------- Luật tiêu chí theo mục đích ----------------

    [Fact]
    public void Knockout_has_no_weight_and_weights_sum_over_scored_criteria_only()
    {
        var ok = new[] { C("exp", 60), C("skills", 40), C("jlpt", 0, RubricCriterionKinds.Knockout) };
        Assert.Empty(ScoringRubric.Validate(ok, RubricPurpose.Cv));

        var weighted = new[] { C("a", 100), C("jlpt", 10, RubricCriterionKinds.Knockout) };
        Assert.Contains(ScoringRubric.Validate(weighted, RubricPurpose.Cv), e => e.Contains("không mang trọng số"));
    }

    [Fact]
    public void Rubric_needs_at_least_one_scored_criterion()
        => Assert.Contains(
            ScoringRubric.Validate(new[] { C("jlpt", 0, RubricCriterionKinds.Knockout) }, RubricPurpose.Cv),
            e => e.Contains("ít nhất một tiêu chí chấm điểm"));

    [Fact]
    public void At_most_five_knockouts()
    {
        var rows = new List<RubricCriterion> { C("a", 100) };
        rows.AddRange(Enumerable.Range(1, 6).Select(i => C($"dk{i}", 0, RubricCriterionKinds.Knockout)));
        Assert.Contains(ScoringRubric.Validate(rows, RubricPurpose.Cv), e => e.Contains("Tối đa 5 điều kiện"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Min_score_must_be_between_1_and_100(int min)
        => Assert.Contains(ScoringRubric.Validate(new[] { C("a", 100, min: min) }, RubricPurpose.Cv),
            e => e.Contains("Điểm tối thiểu"));

    [Fact]
    public void Check_weight_is_one_two_or_three()
    {
        var c = C("a", 100);
        c.Checks = new List<RubricCheck> { new() { Key = "k1", Text = "ý", Weight = 4 } };
        Assert.Contains(ScoringRubric.Validate(new[] { c }, RubricPurpose.Cv), e => e.Contains("×1, ×2 hoặc ×3"));
    }

    [Fact]
    public void Interview_rubric_rejects_knockouts_and_min_scores()
    {
        var errors = ScoringRubric.Validate(
            new[] { C("a", 100, min: 50), C("jlpt", 0, RubricCriterionKinds.Knockout) }, RubricPurpose.Interview);
        Assert.Contains(errors, e => e.Contains("không có điều kiện bắt buộc"));
        Assert.Contains(errors, e => e.Contains("không dùng điểm tối thiểu"));
    }

    [Fact]
    public void Normalize_strips_what_a_knockout_cannot_have_and_keeps_defaults_byte_identical()
    {
        var res = CvRubricEditing.Normalize(new List<CvRubricCriterionInput>
        {
            new()
            {
                Name = "Kinh nghiệm", Weight = 100, Description = "x", Kind = "scored", MinScore = 60,
                Checks = new List<RubricCheck> { new() { Text = "ý 1", Weight = 1 }, new() { Text = "ý 2", Weight = 3 } },
            },
            new()
            {
                Name = "JLPT N2", Weight = 25, Kind = "knockout", MinScore = 50,
                Levels = new RubricLevels { Excellent = "x" },
                Checks = new List<RubricCheck> { new() { Text = "ý" } },
            },
        }, RubricPurpose.Cv);

        Assert.True(res.IsValid, string.Join(";", res.Errors));
        var scored = res.Criteria[0];
        Assert.Null(scored.Kind);                         // "scored" lưu là null
        Assert.Equal(60, scored.MinScore);
        Assert.Null(scored.Checks![0].Weight);            // ×1 lưu là null
        Assert.Equal(3, scored.Checks[1].Weight);

        var knockout = res.Criteria[1];
        Assert.True(knockout.IsKnockout);
        Assert.Equal(0, knockout.Weight);
        Assert.Null(knockout.MinScore);
        Assert.Null(knockout.Levels);
        Assert.Null(knockout.Checks);

        // Không khai gì mới → JSON y hệt bộ tiêu chí trước ADR-075 (lưu lại không tạo phiên bản).
        var plain = CvRubricEditing.Normalize(new List<CvRubricCriterionInput>
        {
            new() { Key = "a", Name = "A", Weight = 100, Description = "x" },
        }, RubricPurpose.Cv);
        Assert.Equal("[{\"key\":\"a\",\"name\":\"A\",\"weight\":100,\"description\":\"x\"}]", ScoringRubric.Serialize(plain.Criteria));
    }

    [Fact]
    public void Interview_normalize_drops_min_scores_and_check_weights_but_reports_knockouts()
    {
        var res = CvRubricEditing.Normalize(new List<CvRubricCriterionInput>
        {
            new() { Name = "Chuyên môn", Weight = 100, Description = "x", MinScore = 60 },
            new() { Name = "JLPT", Weight = 0, Kind = "knockout" },
        }, RubricPurpose.Interview);

        Assert.Null(res.Criteria[0].MinScore);
        Assert.Contains(res.Errors, e => e.Contains("không có điều kiện bắt buộc"));
    }

    // ---------------- Excel ----------------

    [Fact]
    public void Excel_round_trips_kind_min_score_check_weights_and_the_policy_sheet()
    {
        var experience = C("kinh_nghiem", 100, min: 60);
        experience.Checks = new List<RubricCheck>
        {
            new() { Key = "k1", Text = "≥ 4 năm .NET", Weight = 2 },
            new() { Key = "k2", Text = "Dẫn dắt nhóm" },
        };
        var policy = new CvScoringPolicy
        {
            Bands = new CvBandCuts { ExcellentFrom = 85, GoodFrom = 65, FairFrom = 40 },
            Tiers = new CvTierCuts { StrongHireFrom = 85, HireFrom = 70, CautionFrom = 55 },
        };

        var parsed = RubricSheet.Parse(RubricSheet.Build(
            new[] { experience, C("jlpt", 0, RubricCriterionKinds.Knockout, desc: "N2") }, policy));

        Assert.Empty(parsed.Errors);
        Assert.Empty(ScoringRubric.Validate(parsed.Criteria, RubricPurpose.Cv));
        var e = parsed.Criteria[0];
        Assert.Equal(60, e.MinScore);
        Assert.Equal(new int?[] { 2, null }, e.Checks!.Select(x => x.Weight));
        Assert.Equal("≥ 4 năm .NET", e.Checks[0].Text);              // hậu tố (x2) không lọt vào chữ
        Assert.True(parsed.Criteria[1].IsKnockout);
        Assert.Equal(0, parsed.Criteria[1].Weight);
        Assert.True(parsed.Policy!.SameAs(policy));
    }

    [Fact]
    public void Old_single_sheet_file_reads_as_the_default_formula()
    {
        // Mẫu phỏng vấn = bố cục cũ A–I, một sheet.
        var parsed = RubricSheet.Parse(RubricSheet.Build(new[] { C("aa", 100) }, null, RubricPurpose.Interview));

        Assert.Empty(parsed.Errors);
        Assert.Null(parsed.Policy);
        Assert.Null(parsed.Criteria[0].Kind);
    }

    [Theory]
    [InlineData("Điều kiện bắt buộc", true)]
    [InlineData("bắt buộc", true)]
    [InlineData("Chấm điểm", false)]
    [InlineData("", false)]
    public void Excel_kind_column_accepts_vietnamese_labels(string label, bool knockout)
    {
        var bytes = TestSupport.RubricSheetBuilder.Build(
            new[] { "", "Tên", knockout ? "" : "100", "chuẩn", "", "", "", "", "", label, "" });

        var parsed = RubricSheet.Parse(bytes);

        Assert.Empty(parsed.Errors);
        Assert.Equal(knockout, parsed.Criteria.Single().IsKnockout);
    }

    // ---------------- Prompt không có con số ----------------

    [Fact]
    public void Cv_prompt_has_no_weights_and_no_band_numbers_but_lists_knockouts()
    {
        var experience = C("kinh_nghiem", 70, min: 60);
        experience.Levels = new RubricLevels { Excellent = "≥ 5 năm", Good = "3–5 năm" };
        experience.Checks = new List<RubricCheck> { new() { Key = "k1", Text = "Dẫn dắt nhóm", Weight = 2 } };
        var text = ScoringRubric.ToCvPromptText(new[] { experience, C("jlpt", 0, RubricCriterionKinds.Knockout, desc: "N2 trở lên") });

        Assert.DoesNotContain("trọng số", text);
        Assert.DoesNotContain("70", text);
        Assert.DoesNotContain("90–100", text);
        Assert.DoesNotContain("60", text);
        Assert.Contains("excellent: ≥ 5 năm", text);
        Assert.Contains("[k1] Dẫn dắt nhóm", text);
        Assert.Contains("ĐIỀU KIỆN BẮT BUỘC", text);
        Assert.Contains("- jlpt | jlpt | ghi chú: N2 trở lên", text);

        // Bộ phỏng vấn / RAG giữ nguyên định dạng cũ.
        Assert.Contains("trọng số 70%", ScoringRubric.ToPromptText(new[] { experience }));
    }

    // ---------------- Chữ ký "AI được hỏi gì" ----------------

    [Fact]
    public void Signature_ignores_every_number_of_the_formula_and_the_order()
    {
        var a = C("a", 60, min: 50);
        a.Checks = new List<RubricCheck> { new() { Key = "k1", Text = "ý", Weight = 2 } };
        var b = C("b", 40);

        var changed = C("a", 30);
        changed.Checks = new List<RubricCheck> { new() { Key = "k1", Text = "ý" } };
        var b2 = C("b", 70, min: 80);

        Assert.True(CvObservationSignature.Covers(
            CvObservationSignature.Of(new[] { a, b }), CvObservationSignature.Of(new[] { b2, changed })));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("level")]
    [InlineData("check")]
    [InlineData("kind")]
    public void Signature_changes_when_what_the_ai_reads_changes(string what)
    {
        var original = C("a", 100);
        original.Levels = new RubricLevels { Good = "tốt" };
        original.Checks = new List<RubricCheck> { new() { Key = "k1", Text = "ý" } };

        var edited = C("a", 100);
        edited.Levels = new RubricLevels { Good = "tốt" };
        edited.Checks = new List<RubricCheck> { new() { Key = "k1", Text = "ý" } };
        switch (what)
        {
            case "name": edited.Name = "khác"; break;
            case "description": edited.Description = "chuẩn khác"; break;
            case "level": edited.Levels = new RubricLevels { Good = "tốt hơn" }; break;
            case "check": edited.Checks = new List<RubricCheck> { new() { Key = "k1", Text = "ý khác" } }; break;
            case "kind": edited = C("a", 0, RubricCriterionKinds.Knockout); break;
        }

        Assert.NotEqual(CvObservationSignature.OfCriterion(original), CvObservationSignature.OfCriterion(edited));
    }

    [Fact]
    public void Removing_a_criterion_is_covered_adding_one_is_not()
    {
        var two = CvObservationSignature.Of(new[] { C("a", 50), C("b", 50) });
        var one = CvObservationSignature.Of(new[] { C("a", 100) });

        Assert.True(CvObservationSignature.Covers(two, one));
        Assert.False(CvObservationSignature.Covers(one, two));
    }
}
