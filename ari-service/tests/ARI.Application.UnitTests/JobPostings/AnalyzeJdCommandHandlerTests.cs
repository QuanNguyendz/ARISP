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
/// Auto-fill tin từ file JD (<see cref="AnalyzeJdCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "AnalyzeJd" (UTCID01–08): PDF/DOCX parse→lưu→Gemini (PDF inline, DOCX fallback text), lỗi parse/lưu
/// trả Failure, làm sạch ký tự null trước khi gọi Gemini, Gemini fail vẫn trả file (IsValidJd=false), Gemini ném lỗi propagate.
/// </summary>
/// <remarks>Report dùng exception đặt chỗ ("Invalid document"/"Storage unavailable"); fake ném thông điệp riêng
/// nên test assert tiền tố tiếng Việt. Case Gemini throws điều khiển được nên assert đúng "Gemini Error".</remarks>
public class AnalyzeJdCommandHandlerTests
{
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("JD-CONTENT");

    private static Task<Result<AnalyzeJdResponse>> Run(
        StubDocumentParser parser, RecordingFileStorage storage, FakeJdGeminiProvider gemini, string fileName = "backend-jd.pdf", string ext = ".pdf")
        => new AnalyzeJdCommandHandler(parser, storage, gemini).Handle(new AnalyzeJdCommand(Bytes, fileName, ext), CancellationToken.None);

    // UTCID01 — PDF: parse→lưu→trích xuất; Gemini nhận PDF bytes + application/pdf + text
    [Fact]
    public async Task UTCID01_Pdf_success()
    {
        var gemini = new FakeJdGeminiProvider
        {
            ExtractResult = Result.Success(new JdExtractionResultDto { IsValidJd = true, Title = "Senior Backend Engineer", JobCategory = "backend", Skills = { "C#" } })
        };
        var storage = new RecordingFileStorage();

        var res = await Run(new StubDocumentParser { Text = "jd text" }, storage, gemini);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsValidJd);
        Assert.Equal("Senior Backend Engineer", res.Value.Title);
        Assert.Equal("pdf", res.Value.JdFileFormat);
        Assert.Equal("application/pdf", Assert.Single(storage.Saved).ContentType);
        Assert.NotNull(gemini.LastPdfBytes);
        Assert.Equal("application/pdf", gemini.LastMimeType);
        Assert.Equal("jd text", gemini.LastFallbackText);
    }

    // UTCID02 — DOCX: Gemini nhận pdfBytes=null, mime=null, text DOCX
    [Fact]
    public async Task UTCID02_Docx_success()
    {
        var gemini = new FakeJdGeminiProvider();
        var storage = new RecordingFileStorage();

        var res = await Run(new StubDocumentParser { Text = "docx text" }, storage, gemini, "backend-jd.docx", ".docx");

        Assert.True(res.IsSuccess);
        Assert.Equal("docx", res.Value.JdFileFormat);
        Assert.Equal(DocxMime, Assert.Single(storage.Saved).ContentType);
        Assert.Null(gemini.LastPdfBytes);
        Assert.Null(gemini.LastMimeType);
        Assert.Equal("docx text", gemini.LastFallbackText);
    }

    // UTCID03 — parser ném lỗi → Failure, không lưu, không gọi Gemini
    [Fact]
    public async Task UTCID03_Parser_error()
    {
        var storage = new RecordingFileStorage();
        var gemini = new FakeJdGeminiProvider();

        var res = await Run(new StubDocumentParser { ThrowOnParse = true }, storage, gemini);

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể đọc nội dung file JD", res.Error);
        Assert.Empty(storage.Saved);
        Assert.Equal(0, gemini.ExtractCallCount);
    }

    // UTCID04 — lưu file ném lỗi → Failure, server_error
    [Fact]
    public async Task UTCID04_Storage_error()
    {
        var res = await Run(new StubDocumentParser(), new RecordingFileStorage { ThrowOnSave = true }, new FakeJdGeminiProvider());
        Assert.True(res.IsFailure);
        Assert.Contains("Không thể lưu file JD", res.Error);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
    }

    // UTCID05 — text có ký tự null → loại bỏ trước khi gọi Gemini
    [Fact]
    public async Task UTCID05_Null_chars_stripped()
    {
        var gemini = new FakeJdGeminiProvider();
        var res = await Run(new StubDocumentParser { Text = "a\0b\0c" }, new RecordingFileStorage(), gemini);
        Assert.True(res.IsSuccess);
        Assert.Equal("abc", gemini.LastFallbackText);
        Assert.Equal("abc", res.Value.JobDescription);   // Gemini default không có JD → fallback text đã sạch
    }

    // UTCID06 — Gemini trả failure → vẫn Success, IsValidJd=false + file đã lưu + JobDescription=text parse
    [Fact]
    public async Task UTCID06_Gemini_failure_returns_file()
    {
        var gemini = new FakeJdGeminiProvider { ExtractResult = Result.Failure<JdExtractionResultDto>("quota") };
        var res = await Run(new StubDocumentParser { Text = "raw jd text" }, new RecordingFileStorage(), gemini);
        Assert.True(res.IsSuccess);
        Assert.False(res.Value.IsValidJd);
        Assert.Equal("jd/backend-jd.pdf", res.Value.JdFileUrl);
        Assert.Equal("raw jd text", res.Value.JobDescription);
    }

    // UTCID07 — Gemini thành công nhưng JobDescription rỗng → dùng text parse làm fallback
    [Fact]
    public async Task UTCID07_Blank_jd_uses_parsed_fallback()
    {
        var gemini = new FakeJdGeminiProvider { ExtractResult = Result.Success(new JdExtractionResultDto { IsValidJd = true, JobDescription = null }) };
        var res = await Run(new StubDocumentParser { Text = "raw parsed jd" }, new RecordingFileStorage(), gemini);
        Assert.Equal("raw parsed jd", res.Value.JobDescription);
    }

    // UTCID08 — Gemini ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID08_Gemini_throws()
    {
        var gemini = new FakeJdGeminiProvider { ExtractThrows = new Exception("Gemini Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(new StubDocumentParser { Text = "x" }, new RecordingFileStorage(), gemini));
        Assert.Equal("Gemini Error", ex.Message);
    }
}
