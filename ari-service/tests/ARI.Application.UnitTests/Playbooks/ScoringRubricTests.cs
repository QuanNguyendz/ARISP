using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ARI.Application.Playbooks;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Playbooks;

/// <summary>
/// Bộ tiêu chí chấm điểm (ADR-060). Nguyên tắc: AI chỉ chấm TỪNG tiêu chí, điểm cuối do backend cộng
/// có trọng số — trước đây model vừa tự chọn tiêu chí trong danh sách viết cứng, vừa tự cho điểm tổng.
/// </summary>
public class ScoringRubricValidateTests
{
    private static RubricCriterion C(string key, decimal weight, string name = "Tiêu chí")
        => new() { Key = key, Name = name, Weight = weight };

    [Fact]
    public void Accepts_a_well_formed_rubric()
    {
        var errors = ScoringRubric.Validate(new[] { C("technical", 60), C("communication", 40) });

        Assert.Empty(errors);
    }

    /// <summary>Tổng ≠ 100 là lỗi chặn: sai ở đây mà lọt xuống thì mọi điểm về sau đều sai.</summary>
    [Theory]
    [InlineData(60, 30)]   // thiếu
    [InlineData(60, 50)]   // thừa
    public void Rejects_weights_not_summing_to_100(int a, int b)
    {
        var errors = ScoringRubric.Validate(new[] { C("technical", a), C("communication", b) });

        Assert.Contains(errors, e => e.Contains("Tổng trọng số phải bằng 100"));
    }

    [Fact]
    public void Rejects_empty_rubric()
        => Assert.Contains(ScoringRubric.Validate(Array.Empty<RubricCriterion>()),
            e => e.Contains("trống"));

    [Fact]
    public void Rejects_bad_key_duplicate_key_missing_name_and_zero_weight()
    {
        var errors = ScoringRubric.Validate(new[]
        {
            C("Kỹ Thuật", 50),                       // khoá không phải snake_case
            C("technical", 50),
            C("technical", 0, name: ""),             // trùng khoá + thiếu tên + trọng số 0
        });

        Assert.Contains(errors, e => e.Contains("không hợp lệ"));
        Assert.Contains(errors, e => e.Contains("bị trùng"));
        Assert.Contains(errors, e => e.Contains("thiếu tên"));
        Assert.Contains(errors, e => e.Contains("trọng số lớn hơn 0"));
    }

    /// <summary>Số thập phân cộng tròn 100 vẫn phải qua — HR hay chia 33.33/33.33/33.34.</summary>
    [Fact]
    public void Accepts_decimal_weights_that_sum_to_100()
        => Assert.Empty(ScoringRubric.Validate(new[]
        {
            C("technical", 33.33m), C("communication", 33.33m), C("culture_fit", 33.34m),
        }));
}

public class ScoringRubricComputeTests
{
    private static readonly List<RubricCriterion> Rubric = new()
    {
        new() { Key = "technical", Name = "Chuyên môn", Weight = 60 },
        new() { Key = "communication", Name = "Giao tiếp", Weight = 40 },
    };

    [Fact]
    public void Weighted_average_is_the_final_score()
    {
        var score = ScoringRubric.ComputeOverall(Rubric,
            new Dictionary<string, decimal> { ["technical"] = 90, ["communication"] = 50 });

        Assert.Equal(74m, score); // 90*0.6 + 50*0.4
    }

    /// <summary>
    /// Tiêu chí AI quên chấm bị loại khỏi CẢ tử lẫn mẫu. Nếu tính 0 điểm thì một mục bị bỏ sót sẽ
    /// đánh trượt oan ứng viên — thiếu dữ liệu không phải là điểm kém.
    /// </summary>
    [Fact]
    public void Missing_criterion_is_excluded_not_scored_zero()
    {
        var score = ScoringRubric.ComputeOverall(Rubric,
            new Dictionary<string, decimal> { ["technical"] = 90 });

        Assert.Equal(90m, score); // không phải 54 (= 90*0.6 + 0*0.4)
    }

    [Fact]
    public void Returns_null_when_ai_scored_nothing()
        => Assert.Null(ScoringRubric.ComputeOverall(Rubric, new Dictionary<string, decimal>()));

    [Fact]
    public void Clamps_out_of_range_scores()
    {
        var score = ScoringRubric.ComputeOverall(Rubric,
            new Dictionary<string, decimal> { ["technical"] = 150, ["communication"] = -20 });

        Assert.Equal(60m, score); // 100*0.6 + 0*0.4
    }

    /// <summary>Model hay trả khoá viết hoa/có khoảng trắng — đừng vứt điểm vì hình thức.</summary>
    [Fact]
    public void Matches_keys_case_insensitively_and_with_spaces()
    {
        var score = ScoringRubric.ComputeOverall(Rubric,
            new Dictionary<string, decimal> { ["Technical"] = 80, ["Communication"] = 80 });

        Assert.Equal(80m, score);
    }

    [Fact]
    public void Snapshot_keeps_label_and_weight_at_scoring_time()
    {
        var json = ScoringRubric.SerializeScoreSnapshot(Rubric,
            new Dictionary<string, decimal> { ["technical"] = 90, ["communication"] = 50 });

        using var doc = JsonDocument.Parse(json);
        var tech = doc.RootElement.GetProperty("technical");
        Assert.Equal(90m, tech.GetProperty("score").GetDecimal());
        Assert.Equal("Chuyên môn", tech.GetProperty("label").GetString());
        Assert.Equal(60m, tech.GetProperty("weight").GetDecimal());
    }
}

/// <summary>
/// Đọc điểm để hiển thị: phải hiểu CẢ dạng phẳng cũ lẫn dạng ảnh chụp mới, nếu không mọi bản đánh giá
/// cũ sẽ mất bảng điểm ngay khi đổi định dạng.
/// </summary>
public class ScoringRubricSupportTests
{
    [Fact]
    public void Reads_legacy_flat_shape()
    {
        var views = ScoringRubricSupport.ParseForDisplay("{\"technical\": 88}");

        var only = Assert.Single(views);
        Assert.Equal("technical", only.Key);
        Assert.Equal(88m, only.Score);
        Assert.Null(only.Label);
        Assert.Null(only.Weight);
    }

    [Fact]
    public void Reads_snapshot_shape_with_label_and_weight()
    {
        var views = ScoringRubricSupport.ParseForDisplay(
            "{\"technical\": {\"score\": 88, \"label\": \"Chuyên môn\", \"weight\": 60}}");

        var only = Assert.Single(views);
        Assert.Equal(88m, only.Score);
        Assert.Equal("Chuyên môn", only.Label);
        Assert.Equal(60m, only.Weight);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-phai-json")]
    [InlineData("[1,2,3]")]
    public void Bad_input_yields_empty_instead_of_throwing(string? json)
        => Assert.Empty(ScoringRubricSupport.ParseForDisplay(json));
}

/// <summary>Đọc file Excel bộ tiêu chí — khuôn giống import ngân hàng đề (ADR-049).</summary>
public class RubricSheetTests
{
    [Fact]
    public void Template_round_trips_into_a_valid_rubric()
    {
        foreach (var forCv in new[] { true, false })
        {
            var parsed = RubricSheet.Parse(RubricSheet.BuildTemplate(forCv));

            Assert.Empty(parsed.Errors);
            Assert.Empty(ScoringRubric.Validate(parsed.Criteria));   // mẫu phải cộng tròn 100
            Assert.All(parsed.Criteria, c => Assert.False(string.IsNullOrWhiteSpace(c.Description)));
        }
    }

    [Fact]
    public void Rejects_a_file_that_is_not_excel()
    {
        var parsed = RubricSheet.Parse(new byte[] { 1, 2, 3, 4 });

        Assert.NotEmpty(parsed.Errors);
        Assert.Empty(parsed.Criteria);
    }
}

/// <summary>Chọn bộ tiêu chí cho (tin, vòng): vòng → tin → công ty.</summary>
public class ResolveRubricTests
{
    private static readonly Guid JobA = Guid.NewGuid();

    private static PlaybookDocument Doc(string scope, string json, Guid? refId = null, int? round = null)
        => new()
        {
            Scope = scope,
            ScopeRefId = refId,
            RoundNumber = round,
            DocumentType = ScoringRubric.TypeInterviewRubric,
            FileName = "rubric.xlsx",
            UploadedByUserId = Guid.NewGuid(),
            RubricJson = json,
        };

    private static string Json(string key) => ScoringRubric.Serialize(new[]
    {
        new RubricCriterion { Key = key, Name = key, Weight = 100 },
    });

    [Fact]
    public async System.Threading.Tasks.Task Round_rubric_wins_over_job_and_org()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(Doc(PlaybookScope.ScopeOrg, Json("org")))
            .Seed(Doc(PlaybookScope.ScopeJobPosting, Json("job"), JobA))
            .Seed(Doc(PlaybookScope.ScopeRound, Json("round"), JobA, round: 2));

        var criteria = await PlaybookScope.ResolveRubricAsync(
            uow, JobA, 2, ScoringRubric.TypeInterviewRubric, CancellationToken.None);

        Assert.Equal("round", criteria.Single().Key);
    }

    [Fact]
    public async System.Threading.Tasks.Task Falls_back_to_job_then_org()
    {
        var uow = new InMemoryUnitOfWork()
            .Seed(Doc(PlaybookScope.ScopeOrg, Json("org")))
            .Seed(Doc(PlaybookScope.ScopeJobPosting, Json("job"), JobA));

        var jobLevel = await PlaybookScope.ResolveRubricAsync(
            uow, JobA, 3, ScoringRubric.TypeInterviewRubric, CancellationToken.None);
        Assert.Equal("job", jobLevel.Single().Key);

        var otherJob = await PlaybookScope.ResolveRubricAsync(
            uow, Guid.NewGuid(), 1, ScoringRubric.TypeInterviewRubric, CancellationToken.None);
        Assert.Equal("org", otherJob.Single().Key);
    }

    /// <summary>Chưa khai bộ nào → rỗng, nơi gọi giữ nguyên hành vi cũ chứ không chấm bừa.</summary>
    [Fact]
    public async System.Threading.Tasks.Task No_rubric_returns_empty()
        => Assert.Empty(await PlaybookScope.ResolveRubricAsync(
            new InMemoryUnitOfWork(), JobA, 1, ScoringRubric.TypeInterviewRubric, CancellationToken.None));
}
