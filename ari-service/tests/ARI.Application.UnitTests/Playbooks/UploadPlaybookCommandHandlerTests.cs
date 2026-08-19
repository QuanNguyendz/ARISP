using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Playbooks;
using ARI.Application.Playbooks.Commands.UploadPlaybook;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.Playbooks;

/// <summary>
/// Upload playbook (<see cref="UploadPlaybookCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "UploadPlaybook" (UTCID01–07): parse→lưu→tạo document→ingest RAG; text rỗng bỏ RAG; và các lỗi phụ thuộc
/// (parse/lưu/DB/RAG) được CHUYỂN THÀNH Result.Failure (không ném), lỗi persist/ingest thì dọn file đã lưu.
/// </summary>
/// <remarks>
/// Report dùng thông điệp exception đặt chỗ ("Parse Error"/"Storage Error"/"RAG Error"); fake ném thông điệp riêng
/// nên test assert TIỀN TỐ tiếng Việt + mã lỗi (phần có ý nghĩa). Case "DB Error" điều khiển được nên assert đúng.
/// </remarks>
public class UploadPlaybookCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid JobRefId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly byte[] PdfBytes = { 37, 80, 68, 70, 45, 49, 46, 55 };
    private static readonly byte[] DocxBytes = { 80, 75, 3, 4 };

    private static UploadPlaybookCommand PdfCmd()
        => new(UserId, "company", null, null, "guide", "guide.pdf", PdfBytes, ".pdf");

    private static UploadPlaybookCommand DocxCmd()
        => new(UserId, "job_posting", JobRefId, 1, "interview", "interview.docx", DocxBytes, ".docx");

    private static Task<Result<UploadedPlaybookDto>> Run(
        StubDocumentParser parser, RecordingFileStorage storage, RecordingRagIngestionService rag, UploadPlaybookCommand cmd)
        => new UploadPlaybookCommandHandler(new InMemoryUnitOfWork(), parser, storage, rag).Handle(cmd, CancellationToken.None);

    // UTCID01 — PDF hợp lệ → Success, ingest RAG
    [Fact]
    public async Task UTCID01_Pdf_success()
    {
        var rag = new RecordingRagIngestionService();
        var res = await Run(new StubDocumentParser { Text = "Interview guide" }, new RecordingFileStorage(), rag, PdfCmd());

        Assert.True(res.IsSuccess);
        Assert.Equal("company", res.Value.Scope);
        Assert.Equal("guide", res.Value.DocumentType);
        Assert.Equal("pdf", res.Value.FileFormat);
        Assert.Equal("ready", res.Value.Status);
        var ingest = Assert.Single(rag.Ingested);
        Assert.Equal("playbook", ingest.SourceType);
    }

    // UTCID02 — DOCX hợp lệ (job_posting, round 1) → Success
    [Fact]
    public async Task UTCID02_Docx_success()
    {
        var res = await Run(new StubDocumentParser { Text = "Interview guide" }, new RecordingFileStorage(), new RecordingRagIngestionService(), DocxCmd());

        Assert.True(res.IsSuccess);
        Assert.Equal("job_posting", res.Value.Scope);
        Assert.Equal(JobRefId, res.Value.ScopeRefId);
        Assert.Equal(1, res.Value.RoundNumber);
        Assert.Equal("docx", res.Value.FileFormat);
    }

    // UTCID03 — parser trả rỗng → Success nhưng KHÔNG ingest RAG
    [Fact]
    public async Task UTCID03_Empty_text_skips_rag()
    {
        var rag = new RecordingRagIngestionService();
        var res = await Run(new StubDocumentParser { Text = "" }, new RecordingFileStorage(), rag, PdfCmd());

        Assert.True(res.IsSuccess);
        Assert.Empty(rag.Ingested);
    }

    // UTCID04 — parser ném lỗi → Failure "Không thể đọc nội dung file: ..."
    [Fact]
    public async Task UTCID04_Parser_error()
    {
        var storage = new RecordingFileStorage();
        var res = await Run(new StubDocumentParser { ThrowOnParse = true }, storage, new RecordingRagIngestionService(), PdfCmd());

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể đọc nội dung file", res.Error);
        Assert.Empty(storage.Saved);
    }

    // UTCID05 — lưu file ném lỗi → Failure "Không thể lưu file: ...", server_error
    [Fact]
    public async Task UTCID05_Storage_error()
    {
        var res = await Run(new StubDocumentParser { Text = "x" }, new RecordingFileStorage { ThrowOnSave = true }, new RecordingRagIngestionService(), PdfCmd());

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể lưu file", res.Error);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
    }

    // UTCID06 — SaveChangesAsync ném lỗi → Failure "Xử lý playbook thất bại: DB Error", server_error + dọn file
    [Fact]
    public async Task UTCID06_Db_error_cleans_up()
    {
        var storage = new RecordingFileStorage();
        var uow = new InMemoryUnitOfWork().FailSaveOn(1, "DB Error");
        var res = await new UploadPlaybookCommandHandler(uow, new StubDocumentParser { Text = "x" }, storage, new RecordingRagIngestionService())
            .Handle(PdfCmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Xử lý playbook thất bại: DB Error", res.Error);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
        Assert.NotEmpty(storage.Deleted);
    }

    // UTCID07 — RAG ingest ném lỗi → Failure "Xử lý playbook thất bại: ...", server_error + dọn file
    [Fact]
    public async Task UTCID07_Rag_error_cleans_up()
    {
        var storage = new RecordingFileStorage();
        var res = await Run(new StubDocumentParser { Text = "x" }, storage, new RecordingRagIngestionService { ThrowOnIngest = true }, PdfCmd());

        Assert.True(res.IsFailure);
        Assert.Contains("Xử lý playbook thất bại", res.Error);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
        Assert.NotEmpty(storage.Deleted);
    }
}
