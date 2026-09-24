using System;
using System.Collections.Generic;
using System.Linq;
using ARI.Application.CvScoring;
using ARI.Application.Playbooks;
using ARI.Domain.Constants;
using Xunit;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// Công thức chấm CV do HM quyết định (ADR-075): trung bình có trọng số (bù trừ) + hai loại cổng không bù trừ
/// (điều kiện bắt buộc, điểm tối thiểu của tiêu chí), ngưỡng dải / ngưỡng khuyến nghị theo tin, trọng số ý kiểm.
/// Công thức mặc định phải ra ĐÚNG số như trước ADR-075 (trừ ca làm tròn hai lần).
/// </summary>
public class CvScoreCalculatorTests
{
    private static RubricCriterion Scored(string key, decimal weight, int? min = null, params (string Text, int? Weight)[] checks) => new()
    {
        Key = key,
        Name = key,
        Weight = weight,
        Description = "chuẩn",
        MinScore = min,
        Checks = checks.Length == 0
            ? null
            : checks.Select((c, i) => new RubricCheck { Key = $"k{i + 1}", Text = c.Text, Weight = c.Weight }).ToList(),
    };

    private static RubricCriterion Knockout(string key) => new() { Key = key, Name = key, Weight = 0, Kind = RubricCriterionKinds.Knockout };

    private static CvCriterionObservation Obs(string key, string? band = null, decimal? position = null, decimal? legacy = null,
        params (string Key, bool? Met, string? Evidence)[] checks)
        => new(key, band, position, legacy, checks.Select(c => new CvCheckObservation(c.Key, c.Met, c.Evidence)).ToList(),
            null, "trích CV", "lý do");

    private static CvCriterionObservation Gate(string key, bool? met, string? evidence)
        => new(key, null, null, null, Array.Empty<CvCheckObservation>(), met, evidence, "lý do");

    private static Dictionary<string, CvCriterionObservation> Map(params CvCriterionObservation[] obs)
        => obs.ToDictionary(o => o.Key, StringComparer.OrdinalIgnoreCase);

    // ---------------- Mặc định = hành vi cũ ----------------

    [Fact]
    public void Default_formula_reproduces_the_previous_weighted_average_and_recommendation()
    {
        var criteria = new[] { Scored("experience", 70), Scored("education", 30) };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("experience", legacy: 80), Obs("education", legacy: 40)));

        Assert.Equal(68, r.Score);                        // (80×70 + 40×30) ÷ 100
        Assert.Equal(CvRecommendations.Hire, r.Recommendation);
        Assert.Null(r.GateStatus);                        // không cổng nào
    }

    [Fact]
    public void Default_checklist_position_is_unchanged()
    {
        var criteria = new[] { Scored("kn", 100, null, ("a", null), ("b", null), ("c", null), ("d", null)) };

        var r = CvScoreCalculator.Compute(criteria, null,
            Map(Obs("kn", "excellent", null, null, ("k1", true, "x"), ("k2", true, "y"), ("k3", false, null), ("k4", false, null))));

        Assert.Equal(95, r.Score);                        // 90 + 2/4 × 10
    }

    /// <summary>Làm tròn MỘT lần: trước đây 79,495 → 79,50 → 80.</summary>
    [Fact]
    public void Total_is_rounded_exactly_once()
    {
        var criteria = new[] { Scored("a", 50.5m), Scored("b", 49.5m) };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("a", legacy: 79), Obs("b", legacy: 80)));

        Assert.Equal(79.495m, r.Exact);
        Assert.Equal(79, r.Score);
        Assert.Equal(79.49m, CvScoreCalculator.DisplayExact(r.Exact!.Value)); // hiện khớp số cuối
    }

    [Fact]
    public void No_scored_criterion_gives_no_score()
    {
        var r = CvScoreCalculator.Compute(new[] { Scored("a", 100) }, null, Map(Obs("a", "good")));

        Assert.Null(r.Score);
        Assert.Null(r.Recommendation);
    }

    // ---------------- Điều kiện bắt buộc ----------------

    [Fact]
    public void Knockout_is_not_part_of_the_average()
    {
        var criteria = new[] { Scored("a", 100), Knockout("jlpt") };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("a", legacy: 85), Gate("jlpt", true, "JLPT N2")));

        Assert.Equal(85, r.Score);
        Assert.Equal(100, r.TotalWeight);
        Assert.Equal(CvGateStatuses.Pass, r.GateStatus);
        Assert.Equal(CvRecommendations.StrongHire, r.Recommendation);
    }

    /// <summary>Trượt điều kiện chỉ ép NHÃN — điểm vẫn tính, vẫn hiện (ADR-053: không đụng hồ sơ).</summary>
    [Fact]
    public void Failed_knockout_forces_reject_but_keeps_the_score()
    {
        var criteria = new[] { Scored("a", 100), Knockout("jlpt") };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("a", legacy: 85), Gate("jlpt", false, null)));

        Assert.Equal(85, r.Score);
        Assert.Equal(CvRecommendations.StrongHire, r.ScoreTier);
        Assert.Equal(CvGateStatuses.Fail, r.GateStatus);
        Assert.Equal(CvRecommendations.Reject, r.Recommendation);
        var gate = r.Criteria.Single(c => c.Criterion.Key == "jlpt");
        Assert.Equal(CvGateReasons.KnockoutFailed, gate.GateReason);
    }

    /// <summary>"Đạt" không trích dẫn → chưa xác minh: cần người kiểm, KHÔNG ép Reject.</summary>
    [Fact]
    public void Knockout_met_without_evidence_needs_review_and_does_not_force_reject()
    {
        var criteria = new[] { Scored("a", 100), Knockout("jlpt") };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("a", legacy: 85), Gate("jlpt", true, "  ")));

        Assert.Equal(CvGateStatuses.Review, r.GateStatus);
        Assert.Equal(CvRecommendations.StrongHire, r.Recommendation);
        var gate = r.Criteria.Single(c => c.Criterion.Key == "jlpt");
        Assert.True(gate.KnockoutUnsupported);
        Assert.Equal(CvGateReasons.KnockoutUnverified, gate.GateReason);
    }

    [Fact]
    public void Unanswered_knockout_needs_review()
    {
        var criteria = new[] { Scored("a", 100), Knockout("jlpt") };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("a", legacy: 60)));

        Assert.Equal(CvGateStatuses.Review, r.GateStatus);
        Assert.Equal(CvRecommendations.Caution, r.Recommendation);
    }

    [Fact]
    public void Fail_wins_over_review()
    {
        var criteria = new[] { Scored("a", 100), Knockout("x"), Knockout("y") };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("a", legacy: 90), Gate("x", null, null), Gate("y", false, null)));

        Assert.Equal(CvGateStatuses.Fail, r.GateStatus);
        Assert.Equal(CvRecommendations.Reject, r.Recommendation);
    }

    // ---------------- Điểm tối thiểu của tiêu chí ----------------

    [Theory]
    [InlineData(55, CvGateStatuses.Fail, CvRecommendations.Reject)]
    [InlineData(60, CvGateStatuses.Pass, CvRecommendations.Hire)]   // bằng mức tối thiểu là đạt
    public void Min_score_gate(int experienceScore, string gate, string recommendation)
    {
        var criteria = new[] { Scored("experience", 50, min: 60), Scored("skills", 50) };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("experience", legacy: experienceScore), Obs("skills", legacy: 90)));

        Assert.Equal(gate, r.GateStatus);
        Assert.Equal(recommendation, r.Recommendation);
    }

    [Fact]
    public void Unscored_criterion_with_min_score_needs_review_and_is_excluded()
    {
        var criteria = new[] { Scored("experience", 50, min: 60), Scored("skills", 50) };

        var r = CvScoreCalculator.Compute(criteria, null, Map(Obs("skills", legacy: 90)));

        Assert.Equal(90, r.Score);                        // loại khỏi cả tử lẫn mẫu (ADR-060)
        Assert.Equal(CvGateStatuses.Review, r.GateStatus);
        Assert.Equal(CvGateReasons.MinScoreUnscored, r.Criteria[0].GateReason);
    }

    // ---------------- Trọng số ý kiểm ----------------

    [Fact]
    public void Weighted_checks_move_the_position_inside_the_band()
    {
        var criterion = Scored("kn", 100, null, ("a", 2), ("b", null), ("c", null));

        var r = CvScoreCalculator.Compute(new[] { criterion }, null,
            Map(Obs("kn", "good", null, null, ("k1", true, "x"), ("k2", false, null), ("k3", false, null))));

        Assert.Equal(80, r.Score);                        // 70 + 19 × 2/4 = 79,5 → 80
        Assert.Equal(2, r.Criteria[0].Outcome.MetWeight);
        Assert.Equal(4, r.Criteria[0].Outcome.AnsweredWeight);
    }

    [Fact]
    public void Unanswered_weighted_check_is_left_out_of_the_denominator()
    {
        var criterion = Scored("kn", 100, null, ("a", 2), ("b", null), ("c", null));

        var r = CvScoreCalculator.Compute(new[] { criterion }, null,
            Map(Obs("kn", "good", null, null, ("k1", true, "x"), ("k3", false, null))));

        Assert.Equal(83, r.Score);                        // 70 + 19 × 2/3 = 82,67 → 83
    }

    [Fact]
    public void Unsupported_weighted_check_stays_in_the_denominator()
    {
        var criterion = Scored("kn", 100, null, ("a", 2), ("b", null), ("c", null));

        var r = CvScoreCalculator.Compute(new[] { criterion }, null,
            Map(Obs("kn", "good", null, null, ("k1", true, null), ("k2", true, "y"), ("k3", false, null))));

        Assert.Equal(75, r.Score);                        // 70 + 19 × 1/4 = 74,75 → 75
        Assert.True(r.Criteria[0].Outcome.Checks[0].Unsupported);
    }

    // ---------------- Ngưỡng dải / ngưỡng khuyến nghị / vị trí ----------------

    [Fact]
    public void Custom_band_cuts_change_the_range_of_each_band()
    {
        var policy = new CvScoringPolicy { Bands = new CvBandCuts { ExcellentFrom = 85, GoodFrom = 65, FairFrom = 40 } };
        var criterion = Scored("kn", 100, null, ("a", null), ("b", null));

        var r = CvScoreCalculator.Compute(new[] { criterion }, policy,
            Map(Obs("kn", "excellent", null, null, ("k1", true, "x"), ("k2", false, null))));

        Assert.Equal(93, r.Score);                        // 85 + 15 × 1/2 = 92,5 → 93
        Assert.Equal(ScoringRubric.BandGood, policy.Bands.BandOf(84));
        Assert.Equal((65m, 84m), policy.Bands.Range(ScoringRubric.BandGood));
    }

    [Fact]
    public void Position_is_mapped_onto_the_band()
    {
        var r = CvScoreCalculator.Compute(new[] { Scored("kn", 100) }, null, Map(Obs("kn", "good", position: 0.5m)));

        Assert.Equal(80, r.Score);                        // 70 + 19 × 0,5 = 79,5 → 80
        Assert.Equal(CvCriterionScoring.SourceAi, r.Criteria[0].Outcome.Source);
    }

    [Fact]
    public void Custom_tiers_decide_the_recommendation()
    {
        var policy = new CvScoringPolicy { Tiers = new CvTierCuts { StrongHireFrom = 90, HireFrom = 75, CautionFrom = 60 } };

        var r = CvScoreCalculator.Compute(new[] { Scored("a", 100) }, policy, Map(Obs("a", legacy: 80)));

        Assert.Equal(CvRecommendations.Hire, r.Recommendation);          // mặc định 80 là "Strong Hire"
    }

    [Fact]
    public void Position_given_on_a_percent_scale_is_normalised()
    {
        Assert.Equal(0.75m, CvObservations.NormalizePosition(75));
        Assert.Equal(1m, CvObservations.NormalizePosition(1.5m * 100));
        Assert.Equal(0m, CvObservations.NormalizePosition(-0.2m));
        Assert.Null(CvObservations.NormalizePosition(null));
    }
}
