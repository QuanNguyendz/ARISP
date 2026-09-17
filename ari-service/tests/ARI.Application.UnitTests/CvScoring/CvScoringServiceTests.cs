using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvScoring;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;
using static ARI.Application.UnitTests.CvScoring.CvScoringKit;

namespace ARI.Application.UnitTests.CvScoring;

/// <summary>
/// Chấm CV theo bộ tiêu chí BẮT BUỘC (ADR-070) — <see cref="CvScoringService"/>.
///
/// Chốt: không có bộ tiêu chí thì không có lời gọi AI nào; điểm cuối LUÔN do backend cộng có trọng số;
/// dùng lại kết quả theo (tin, file CV, bộ tiêu chí); bộ tiêu chí đổi thì chấm lại; file không phải CV
/// lưu <c>invalid_cv</c>; lỗi AI không lưu gì.
/// </summary>
public class CvScoringServiceTests
{
    [Fact]
    public async Task Job_not_found_fails_without_calling_ai()
    {
        var gemini = new FakeGeminiProvider();
        var res = await Service(new InMemoryUnitOfWork(), gemini).ScoreAsync(Guid.NewGuid(), CvBytes(), "cv.pdf");

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Equal(0, gemini.AnalyzeCallCount);
    }

    /// <summary>Luật trung tâm của ADR-070: tin chưa có bộ tiêu chí → không chấm, không gọi AI.</summary>
    [Fact]
    public async Task Without_a_rubric_nothing_is_scored_and_ai_is_never_called()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job);
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 90)) };

        var res = await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.True(res.IsFailure);
        Assert.Equal(CvScoringErrors.RubricRequired, res.ErrorCode);
        Assert.Equal(0, gemini.AnalyzeCallCount);
        Assert.Empty(uow.Repo<CvJdAnalysis>().Items);
    }

    /// <summary>Bộ tiêu chí công ty / bộ tiêu chí phỏng vấn KHÔNG thay được bộ tiêu chí CV của tin.</summary>
    [Fact]
    public async Task Org_template_and_interview_rubric_do_not_count_as_the_job_cv_rubric()
    {
        var job = Job();
        var orgTemplate = Rubric(job.Id, ("experience", "Kinh nghiệm", 100));
        orgTemplate.Scope = "org";
        orgTemplate.ScopeRefId = null;
        var interview = Rubric(job.Id, ("technical", "Chuyên môn", 100));
        interview.DocumentType = "interview_rubric";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(orgTemplate, interview);
        var gemini = new FakeGeminiProvider();

        var res = await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.Equal(CvScoringErrors.RubricRequired, res.ErrorCode);
        Assert.Equal(0, gemini.AnalyzeCallCount);
    }

    [Fact]
    public async Task Score_is_the_weighted_average_computed_by_the_backend()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric);
        // 80×70 + 40×30 = 6800 → ÷ 100 = 68.
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80), ("education", 40)) };

        var res = await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.True(res.IsSuccess);
        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal(68, saved.MatchScore);
        Assert.Equal(CvAnalysisStatuses.Completed, saved.Status);
        Assert.Equal(rubric.Id, saved.RubricDocumentId);
        Assert.Equal("Hire", saved.OverallRecommendation); // 68 ≥ 65 — suy từ điểm, không hỏi AI
        Assert.Equal("Đúng cấp bậc", saved.SeniorityAlignment);
        Assert.Contains("C#", JsonSerializer.Deserialize<string[]>(saved.SkillsMatched)!);
    }

    /// <summary>Tiêu chí AI bỏ sót bị loại khỏi CẢ tử lẫn mẫu (80×70 ÷ 70 = 80), và vẫn có mặt trong ảnh chụp.</summary>
    [Fact]
    public async Task Missing_criterion_is_excluded_from_both_sides_and_kept_in_the_snapshot()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80), ("education", null)) };

        await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        var saved = Assert.Single(uow.Repo<CvJdAnalysis>().Items);
        Assert.Equal(80, saved.MatchScore);
        var views = CvScoreSnapshot.Parse(saved.CriterionScores);
        Assert.Equal(new[] { "experience", "education" }, views.Select(v => v.Key));
        Assert.Null(views[1].Score);
        Assert.Equal("bằng chứng experience", views[0].Evidence);
        Assert.Equal("lý do experience", views[0].Reasoning);
        Assert.Equal("chuẩn experience", views[0].Description);
    }

    /// <summary>AI không chấm được tiêu chí nào → hỏng, KHÔNG lưu, không có đường lùi về điểm AI.</summary>
    [Fact]
    public async Task No_scored_criterion_fails_and_saves_nothing()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("unknown_key", 90)) };

        var res = await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<CvJdAnalysis>().Items);
    }

    [Fact]
    public async Task Rubric_names_weights_and_keys_reach_the_ai()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80)) };

        await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        var req = gemini.LastRequest!;
        Assert.Contains("Kinh nghiệm", req.RubricInstruction);
        Assert.Contains("70", req.RubricInstruction);
        Assert.Equal(new[] { "experience", "education" }, req.CriterionKeys);
        Assert.DoesNotContain("40/40/20", req.JdText);           // bỏ câu trọng số mặc định cũ
        Assert.NotNull(req.CvPdf);                                // CV PDF gửi nguyên file
    }

    /// <summary>Lệch 1 (quy tắc 17): file JD gốc PDF phải tới tay AI.</summary>
    [Fact]
    public async Task Original_jd_pdf_is_attached()
    {
        var job = Job();
        job.JdFileUrl = "jd/jd.pdf";
        job.JdFileFormat = "pdf";
        job.JdFileName = "jd.pdf";
        job.JobDescription = "<p>Mô tả <b>JD</b></p><ul><li>C#</li></ul>";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        var storage = new RecordingFileStorage { FileBytes = new byte[] { 1, 2, 3 } };
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80)) };

        await Service(uow, gemini, storage: storage).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        var req = gemini.LastRequest!;
        Assert.NotNull(req.JdPdf);
        Assert.Equal("application/pdf", req.JdPdf!.MimeType);
        Assert.DoesNotContain("<p>", req.JdText);                 // mô tả HTML gửi dạng văn bản thuần
        Assert.Contains("Mô tả JD", req.JdText);
    }

    [Fact]
    public async Task Docx_jd_is_sent_as_extracted_text()
    {
        var job = Job();
        job.JdFileUrl = "jd/jd.docx";
        job.JdFileFormat = "docx";
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        var storage = new RecordingFileStorage { FileBytes = new byte[] { 1, 2, 3 } };
        var parser = new FakeDocumentParser { Text = "NỘI DUNG DOCX" };
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80)) };

        await Service(uow, gemini, parser, storage).ScoreAsync(job.Id, CvBytes(), "cv.docx");

        var req = gemini.LastRequest!;
        Assert.Null(req.JdPdf);
        Assert.Contains("NỘI DUNG DOCX", req.JdText);
        Assert.Null(req.CvPdf);                                   // CV DOCX chỉ gửi text
        Assert.Equal("NỘI DUNG DOCX", req.CvText);
    }

    [Fact]
    public async Task Same_file_and_rubric_is_reused_without_calling_ai()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 80)) };
        var service = Service(uow, gemini);

        var first = await service.ScoreAsync(job.Id, CvBytes(), "cv.pdf");
        var second = await service.ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Single(uow.Repo<CvJdAnalysis>().Items);
    }

    /// <summary>Bộ tiêu chí mới → cùng file được chấm lại (bản cũ giữ làm lịch sử).</summary>
    [Fact]
    public async Task New_rubric_version_rescores_the_same_file()
    {
        var job = Job();
        var oldRubric = DefaultRubric(job.Id);
        oldRubric.DeletedAt = DateTimeOffset.UtcNow;
        var old = new CvJdAnalysis
        {
            JobPostingId = job.Id,
            CvHash = CvScoringService.ComputeHash(CvBytes()),
            RubricDocumentId = oldRubric.Id,
            MatchScore = 50,
        };
        var newRubric = Rubric(job.Id, ("experience", "Kinh nghiệm", 100));
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(oldRubric, newRubric).Seed(old);
        var gemini = new FakeGeminiProvider { AnalyzeResult = FakeGeminiProvider.Scored(("experience", 90)) };

        var res = await Service(uow, gemini).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.Equal(newRubric.Id, res.Value!.RubricDocumentId);
        Assert.Equal(90, res.Value.MatchScore);
        Assert.Equal(2, uow.Repo<CvJdAnalysis>().Items.Count);
    }

    /// <summary>Lệch 5: file không phải CV lưu <c>invalid_cv</c> — là kết cục chắc chắn, dùng lại được.</summary>
    [Fact]
    public async Task Invalid_cv_is_saved_as_invalid_cv_and_reused()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        var gemini = new FakeGeminiProvider
        {
            AnalyzeResult = Result.Success(new CvJdAnalysisResultDto { IsValidCv = false, Summary = "không phải CV", Provider = "Gemini", RawResponse = "{}" }),
        };
        var service = Service(uow, gemini);

        var res = await service.ScoreAsync(job.Id, CvBytes(), "cv.pdf");
        await service.ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.True(res.IsSuccess);
        Assert.Equal(CvAnalysisStatuses.InvalidCv, res.Value!.Status);
        Assert.Equal(1, gemini.AnalyzeCallCount);
        Assert.False(CvScoreState.IsDisplayable(res.Value.Status, res.Value.RubricDocumentId));
    }

    [Fact]
    public async Task Ai_failure_saves_nothing_and_is_remembered_for_backoff()
    {
        var job = Job();
        var rubric = DefaultRubric(job.Id);
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(rubric);
        var gemini = new FakeGeminiProvider { AnalyzeResult = Result.Failure<CvJdAnalysisResultDto>("quota exceeded") };
        var inFlight = new CvScoringInFlight();

        var res = await Service(uow, gemini, inFlight: inFlight).ScoreAsync(job.Id, CvBytes(), "cv.pdf");

        Assert.True(res.IsFailure);
        Assert.Contains("Lỗi AI", res.Error);
        Assert.Empty(uow.Repo<CvJdAnalysis>().Items);
        var key = CvScoringInFlight.Key(job.Id, CvScoringService.ComputeHash(CvBytes()), rubric.Id);
        Assert.True(inFlight.ShouldBackOff(key));
        Assert.Contains("quota", inFlight.LastFailure(key)!.Message);
    }

    [Fact]
    public async Task Unreadable_non_pdf_cv_fails_without_calling_ai()
    {
        var job = Job();
        var uow = new InMemoryUnitOfWork().Seed(job).Seed(DefaultRubric(job.Id));
        var gemini = new FakeGeminiProvider();

        var res = await Service(uow, gemini, new FakeDocumentParser { Text = "" }).ScoreAsync(job.Id, CvBytes(), "cv.docx");

        Assert.True(res.IsFailure);
        Assert.Equal(0, gemini.AnalyzeCallCount);
    }

    [Fact]
    public void Html_description_is_flattened_for_the_prompt()
    {
        var text = CvScoringService.HtmlToText("<h3>Yêu cầu</h3><ul><li>C# &amp; .NET</li><li>SQL</li></ul>");
        Assert.Equal("Yêu cầu\n\nC# & .NET\n\nSQL", text);
    }
}
