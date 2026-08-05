using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Jobs.Commands.AnalyzeJd;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.JobPostings;

/// <summary>
/// Auto-fill tin từ file JD (UC-45, <see cref="AnalyzeJdCommandHandler"/>, ADR-042): parse text → lưu file →
/// gọi Gemini trích xuất. PDF gửi inline, DOCX dùng text fallback; lỗi parse/lưu trả Failure; Gemini lỗi
/// vẫn trả file đã lưu (IsValidJd=false) để người dùng nhập tay.
/// </summary>
public class AnalyzeJdCommandHandlerTests
{
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("PDF-OR-DOCX-CONTENT");

    private static Task<Result<AnalyzeJdResponse>> Run(
        StubDocumentParser parser, RecordingFileStorage storage, FakeJdGeminiProvider gemini,
        string fileName = "jd.pdf", string ext = ".pdf")
        => new AnalyzeJdCommandHandler(parser, storage, gemini)
            .Handle(new AnalyzeJdCommand(Bytes, fileName, ext), CancellationToken.None);

    [Fact]
    public async Task Successful_extraction_returns_autofill_fields()
    {
        var gemini = new FakeJdGeminiProvider
        {
            ExtractResult = Result.Success(new JdExtractionResultDto
            {
                IsValidJd = true,
                Title = "Senior Backend Engineer",
                JobCategory = "backend",
                Skills = { "C#", "PostgreSQL" },
            }),
        };

        var res = await Run(new StubDocumentParser(), new RecordingFileStorage(), gemini);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsValidJd);
        Assert.Equal("Senior Backend Engineer", res.Value.Title);
        Assert.Equal("backend", res.Value.JobCategory);
        Assert.Equal("stored/jd.pdf", res.Value.JdFileUrl);
        Assert.Equal("pdf", res.Value.JdFileFormat);
        Assert.Contains("C#", res.Value.Skills);
    }

    [Fact]
    public async Task Parse_failure_returns_error_without_saving_or_calling_gemini()
    {
        var storage = new RecordingFileStorage();
        var gemini = new FakeJdGeminiProvider();

        var res = await Run(new StubDocumentParser { ThrowOnParse = true }, storage, gemini);

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể đọc nội dung file JD", res.Error);
        Assert.Empty(storage.Saved);
        Assert.Equal(0, gemini.ExtractCallCount);
    }

    [Fact]
    public async Task Storage_failure_returns_server_error()
    {
        var res = await Run(new StubDocumentParser(), new RecordingFileStorage { ThrowOnSave = true }, new FakeJdGeminiProvider());

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể lưu file JD", res.Error);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
    }

    [Fact]
    public async Task Gemini_failure_still_returns_saved_file_with_isvalid_false()
    {
        var parser = new StubDocumentParser { Text = "raw jd text" };
        var gemini = new FakeJdGeminiProvider { ExtractResult = Result.Failure<JdExtractionResultDto>("quota exceeded") };

        var res = await Run(parser, new RecordingFileStorage(), gemini);

        Assert.True(res.IsSuccess);           // vẫn success để người dùng nhập tay
        Assert.False(res.Value.IsValidJd);
        Assert.Equal("stored/jd.pdf", res.Value.JdFileUrl);
        Assert.Equal("raw jd text", res.Value.JobDescription); // fallback text đã parse
    }

    [Fact]
    public async Task Pdf_is_sent_inline_to_gemini()
    {
        var gemini = new FakeJdGeminiProvider();

        await Run(new StubDocumentParser(), new RecordingFileStorage(), gemini, "jd.pdf", ".pdf");

        Assert.NotNull(gemini.LastPdfBytes);
        Assert.Equal("application/pdf", gemini.LastMimeType);
    }

    [Fact]
    public async Task Docx_uses_text_fallback_not_inline_bytes()
    {
        var gemini = new FakeJdGeminiProvider();
        var parser = new StubDocumentParser { Text = "docx parsed text" };

        await Run(parser, new RecordingFileStorage(), gemini, "jd.docx", ".docx");

        Assert.Null(gemini.LastPdfBytes);           // DOCX không gửi bytes inline
        Assert.Null(gemini.LastMimeType);
        Assert.Equal("docx parsed text", gemini.LastFallbackText);
    }

    [Fact]
    public async Task JobDescription_prefers_gemini_over_parsed_text()
    {
        var parser = new StubDocumentParser { Text = "raw parsed" };
        var gemini = new FakeJdGeminiProvider
        {
            ExtractResult = Result.Success(new JdExtractionResultDto { IsValidJd = true, JobDescription = "clean jd from gemini" }),
        };

        var res = await Run(parser, new RecordingFileStorage(), gemini);

        Assert.Equal("clean jd from gemini", res.Value.JobDescription);
    }

    [Fact]
    public async Task JobDescription_falls_back_to_parsed_when_gemini_empty()
    {
        var parser = new StubDocumentParser { Text = "raw parsed jd" };
        var gemini = new FakeJdGeminiProvider
        {
            ExtractResult = Result.Success(new JdExtractionResultDto { IsValidJd = true, JobDescription = null }),
        };

        var res = await Run(parser, new RecordingFileStorage(), gemini);

        Assert.Equal("raw parsed jd", res.Value.JobDescription);
    }
}
