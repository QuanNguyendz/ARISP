using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Commands.SubmitApplication;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>
/// Nộp hồ sơ từ Job Board — wrapper CQRS (<see cref="SubmitApplicationCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "SubmitApplication" (UTCID01–10): chọn MIME theo đuôi file, làm sạch ký tự null, lưu file rồi ủy quyền
/// IApplicationService, lỗi parse/lưu trả Failure, DB fail thì dọn file, và propagate lỗi service/xoá file.
/// </summary>
/// <remarks>
/// Report dùng thông điệp exception đặt chỗ ("Parse Error"/"Storage Error"). Fake ném thông điệp riêng
/// ("parse failed"/"storage down") nên test assert TIỀN TỐ tiếng Việt + mã lỗi (phần có ý nghĩa, ổn định).
/// </remarks>
public class SubmitApplicationCommandHandlerTests
{
    private const string PdfMime = "application/pdf";
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static readonly byte[] CvBytes = { 1, 2, 3 };

    private static SubmitApplicationCommand Command(string fileName = "cv.pdf", string ext = ".pdf")
        => new(Guid.NewGuid(), Guid.NewGuid(), "candidate@example.com", "Candidate User", "0901234567", CvBytes, fileName, ext);

    private static Task<Result<ApplicationResponse>> Run(
        FakeApplicationService svc, StubDocumentParser parser, RecordingFileStorage storage, SubmitApplicationCommand cmd)
        => new SubmitApplicationCommandHandler(svc, parser, storage).Handle(cmd, CancellationToken.None);

    // UTCID01 — PDF hợp lệ, mọi phụ thuộc OK → Success; MIME=application/pdf; Source=job_board
    [Fact]
    public async Task UTCID01_Valid_pdf()
    {
        var svc = new FakeApplicationService();
        var storage = new RecordingFileStorage();

        var res = await Run(svc, new StubDocumentParser { Text = "CV text" }, storage, Command("cv.pdf", ".pdf"));

        Assert.True(res.IsSuccess);
        Assert.Equal(PdfMime, Assert.Single(storage.Saved).ContentType);
        Assert.Equal("job_board", svc.LastSource);
    }

    // UTCID02 — DOCX → dùng MIME docx
    [Fact]
    public async Task UTCID02_Valid_docx()
    {
        var storage = new RecordingFileStorage();
        var res = await Run(new FakeApplicationService(), new StubDocumentParser(), storage, Command("cv.docx", ".docx"));
        Assert.True(res.IsSuccess);
        Assert.Equal(DocxMime, Assert.Single(storage.Saved).ContentType);
    }

    // UTCID03 — đuôi lạ (.txt) → MIME=text/plain
    [Fact]
    public async Task UTCID03_Unknown_extension_text_plain()
    {
        var storage = new RecordingFileStorage();
        var res = await Run(new FakeApplicationService(), new StubDocumentParser(), storage, Command("cv.txt", ".txt"));
        Assert.True(res.IsSuccess);
        Assert.Equal("text/plain", Assert.Single(storage.Saved).ContentType);
    }

    // UTCID04 — text có ký tự null → bị loại khỏi CvText
    [Fact]
    public async Task UTCID04_Null_characters_stripped()
    {
        var svc = new FakeApplicationService();
        await Run(svc, new StubDocumentParser { Text = "a\0b\0c" }, new RecordingFileStorage(), Command());
        Assert.Equal("abc", svc.LastRequest!.CvText);
    }

    // UTCID05 — parser trả text rỗng → CvText=""
    [Fact]
    public async Task UTCID05_Empty_parsed_text()
    {
        var svc = new FakeApplicationService();
        await Run(svc, new StubDocumentParser { Text = "" }, new RecordingFileStorage(), Command());
        Assert.Equal("", svc.LastRequest!.CvText);
    }

    // UTCID06 — parser ném lỗi → Failure "Không thể phân tích file CV: ..."
    [Fact]
    public async Task UTCID06_Parser_error()
    {
        var svc = new FakeApplicationService();
        var storage = new RecordingFileStorage();

        var res = await Run(svc, new StubDocumentParser { ThrowOnParse = true }, storage, Command());

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể phân tích file CV", res.Error);
        Assert.Empty(storage.Saved);
        Assert.Null(svc.LastRequest);
    }

    // UTCID07 — lưu file ném lỗi → Failure "Không thể lưu file CV: ...", server_error
    [Fact]
    public async Task UTCID07_Storage_error()
    {
        var svc = new FakeApplicationService();

        var res = await Run(svc, new StubDocumentParser(), new RecordingFileStorage { ThrowOnSave = true }, Command());

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể lưu file CV", res.Error);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
        Assert.Null(svc.LastRequest);
    }

    // UTCID08 — service trả Failure → dọn file đã lưu + trả nguyên failure
    [Fact]
    public async Task UTCID08_Service_failure_cleans_up()
    {
        var svc = new FakeApplicationService { SubmitResult = Result.Failure<ApplicationResponse>("Submit Error") };
        var storage = new RecordingFileStorage();

        var res = await Run(svc, new StubDocumentParser(), storage, Command("cv.pdf"));

        Assert.True(res.IsFailure);
        Assert.Contains("Submit Error", res.Error);
        Assert.Contains("cv/cv.pdf", storage.Deleted);
    }

    // UTCID09 — service ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID09_Service_throws()
    {
        var svc = new FakeApplicationService { SubmitThrows = new Exception("Service Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(svc, new StubDocumentParser(), new RecordingFileStorage(), Command()));
        Assert.Equal("Service Error", ex.Message);
    }

    // UTCID10 — service Failure + DeleteAsync (dọn dẹp) ném lỗi → thoát ra ngoài
    [Fact]
    public async Task UTCID10_Cleanup_delete_throws()
    {
        var svc = new FakeApplicationService { SubmitResult = Result.Failure<ApplicationResponse>("Submit Error") };
        var storage = new RecordingFileStorage { DeleteThrows = new Exception("Delete Error") };

        var ex = await Assert.ThrowsAsync<Exception>(() => Run(svc, new StubDocumentParser(), storage, Command()));
        Assert.Equal("Delete Error", ex.Message);
    }
}
