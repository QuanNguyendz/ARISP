using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Playbooks;
using ARI.Application.Playbooks.Commands.UploadPlaybook;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
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

    private static UploadPlaybookCommandHandler Handler(
        InMemoryUnitOfWork uow, StubDocumentParser parser, RecordingFileStorage storage, RecordingRagIngestionService rag)
        => new(uow, parser, storage, rag);

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

    // ---------- Bộ tiêu chí chấm điểm (ADR-060) ----------

    private static UploadPlaybookCommand RubricCmd(byte[] bytes, string ext = ".xlsx", string type = "interview_rubric")
        => new(Guid.NewGuid(), "org", null, null, type, "rubric" + ext, bytes, ext);

    /// <summary>
    /// Rubric là DỮ LIỆU chứ không phải văn bản tự do: file sai định dạng phải bị chặn tại cổng, chứ
    /// không đẩy qua parser rồi lưu một tài liệu không đọc được tiêu chí nào.
    /// </summary>
    [Fact]
    public async Task Rubric_must_be_xlsx()
    {
        var uow = new InMemoryUnitOfWork();
        var storage = new RecordingFileStorage();
        var rag = new RecordingRagIngestionService();

        var res = await Handler(uow, new StubDocumentParser { Text = "Tiêu chí" }, storage, rag)
            .Handle(RubricCmd(new byte[] { 1, 2, 3 }, ext: ".pdf"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Excel (.xlsx)", res.Error);
        Assert.Empty(storage.Saved);
        Assert.Empty(uow.Repo<PlaybookDocument>().Items);
    }

    /// <summary>Tổng trọng số ≠ 100 → chặn ngay, kèm thông báo đọc được. Lọt xuống là mọi điểm đều sai.</summary>
    [Fact]
    public async Task Rubric_with_weights_not_summing_to_100_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();
        var storage = new RecordingFileStorage();
        var sheet = RubricSheetBuilder.Build(("technical", "Chuyên môn", "60"), ("communication", "Giao tiếp", "30"));

        var res = await Handler(uow, new StubDocumentParser(), storage, new RecordingRagIngestionService())
            .Handle(RubricCmd(sheet), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Tổng trọng số phải bằng 100", res.Error);
        Assert.Contains("90", res.Error);                      // nói rõ đang là bao nhiêu
        Assert.Empty(storage.Saved);
        Assert.Empty(uow.Repo<PlaybookDocument>().Items);
    }

    [Fact]
    public async Task Rubric_file_that_is_not_excel_at_all_is_rejected()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await Handler(uow, new StubDocumentParser(), new RecordingFileStorage(), new RecordingRagIngestionService())
            .Handle(RubricCmd(new byte[] { 9, 9, 9, 9 }), CancellationToken.None);   // .xlsx nhưng nội dung rác

        Assert.True(res.IsFailure);
        Assert.Empty(uow.Repo<PlaybookDocument>().Items);
    }

    /// <summary>
    /// Rubric hợp lệ: lưu <c>RubricJson</c> để backend cộng điểm, và ingest CHUẨN CHẤM vào RAG để AI
    /// truy hồi khi diễn giải — hai việc khác nhau trên cùng một file.
    /// </summary>
    [Fact]
    public async Task Valid_rubric_stores_json_and_ingests_the_standards_text()
    {
        var uow = new InMemoryUnitOfWork();
        var rag = new RecordingRagIngestionService();
        var parser = new StubDocumentParser { ThrowOnParse = true };   // rubric KHÔNG đi qua parser văn bản
        var sheet = RubricSheetBuilder.Build(
            ("technical", "Chuyên môn", "60", "Trả lời đúng và sâu."),
            ("communication", "Giao tiếp", "40", "Diễn đạt mạch lạc."));

        var res = await Handler(uow, parser, new RecordingFileStorage(), rag)
            .Handle(RubricCmd(sheet), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.CriteriaCount);                     // FE hiện được "2 tiêu chí"

        var doc = Assert.Single(uow.Repo<PlaybookDocument>().Items);
        Assert.Equal("xlsx", doc.FileFormat);
        Assert.NotNull(doc.RubricJson);
        var criteria = ARI.Application.Playbooks.ScoringRubric.Deserialize(doc.RubricJson);
        Assert.Equal(2, criteria.Count);
        Assert.Equal(60m, criteria.Single(c => c.Key == "technical").Weight);

        var ingest = Assert.Single(rag.Ingested);
        Assert.Contains("Chuyên môn", ingest.Text);
        Assert.Contains("Trả lời đúng và sâu.", ingest.Text);          // chuẩn chấm vào RAG
    }

    [Fact]
    public async Task Cv_rubric_type_goes_through_the_same_gate()
    {
        var uow = new InMemoryUnitOfWork();
        var sheet = RubricSheetBuilder.Build(("experience", "Kinh nghiệm", "100"));

        var res = await Handler(uow, new StubDocumentParser { ThrowOnParse = true },
                new RecordingFileStorage(), new RecordingRagIngestionService())
            .Handle(RubricCmd(sheet, type: "cv_rubric"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(1, res.Value.CriteriaCount);
        Assert.NotNull(Assert.Single(uow.Repo<PlaybookDocument>().Items).RubricJson);
    }
}
