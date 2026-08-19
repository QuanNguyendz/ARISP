using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Playbooks.Commands.UploadPlaybook;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Playbooks;

/// <summary>
/// Upload playbook vào RAG (<see cref="UploadPlaybookCommandHandler"/>, test-plan B14, ADR-039):
/// parse fail → không lưu/không ingest; happy path lưu document 'ready' + ingest 1 lần (playbook/scope/type);
/// parse rỗng → lưu nhưng bỏ ingest (guard); ingest lỗi → ServerError + xoá file bù trừ.
/// </summary>
public class UploadPlaybookCommandHandlerTests
{
    private static UploadPlaybookCommandHandler Handler(
        InMemoryUnitOfWork uow, StubDocumentParser parser, RecordingFileStorage storage, RecordingRagIngestionService rag)
        => new(uow, parser, storage, rag);

    private static UploadPlaybookCommand Cmd(
        string scope = "job", string documentType = "question_bank", string ext = ".pdf", string fileName = "pb.pdf")
        => new(Guid.NewGuid(), scope, null, null, documentType, fileName, new byte[] { 1, 2, 3 }, ext);

    [Fact]
    public async Task Parser_failure_saves_nothing_and_does_not_ingest()
    {
        var uow = new InMemoryUnitOfWork();
        var parser = new StubDocumentParser { ThrowOnParse = true };
        var storage = new RecordingFileStorage();
        var rag = new RecordingRagIngestionService();

        var res = await Handler(uow, parser, storage, rag).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể đọc nội dung file", res.Error);
        Assert.Empty(storage.Saved);
        Assert.Empty(rag.Ingested);
        Assert.Empty(uow.Repo<PlaybookDocument>().Items);
    }

    [Fact]
    public async Task Valid_upload_saves_ready_document_and_ingests_once()
    {
        var uow = new InMemoryUnitOfWork();
        var parser = new StubDocumentParser { Text = "Nội dung playbook" };
        var storage = new RecordingFileStorage();
        var rag = new RecordingRagIngestionService();

        var res = await Handler(uow, parser, storage, rag)
            .Handle(Cmd(scope: "job", documentType: "  question_bank  ", ext: ".pdf"), CancellationToken.None);

        Assert.True(res.IsSuccess);

        var doc = Assert.Single(uow.Repo<PlaybookDocument>().Items);
        Assert.Equal("ready", doc.Status);
        Assert.Equal("pdf", doc.FileFormat);            // ext bỏ dấu chấm
        Assert.Equal("question_bank", doc.DocumentType); // trim
        Assert.Equal("job", doc.Scope);
        Assert.Equal("playbooks/pb.pdf", doc.FileUrl);

        var ingest = Assert.Single(rag.Ingested);
        Assert.Equal("playbook", ingest.SourceType);
        Assert.Equal(doc.Id, ingest.SourceId);
        Assert.Equal("job", ingest.Scope);
        Assert.Equal("question_bank", ingest.DocumentType);

        Assert.Equal("ready", res.Value.Status);
        Assert.Equal("pdf", res.Value.FileFormat);
    }

    [Fact]
    public async Task Empty_parsed_text_saves_document_but_skips_ingest()
    {
        var uow = new InMemoryUnitOfWork();
        var parser = new StubDocumentParser { Text = "" };
        var storage = new RecordingFileStorage();
        var rag = new RecordingRagIngestionService();

        var res = await Handler(uow, parser, storage, rag).Handle(Cmd(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(uow.Repo<PlaybookDocument>().Items); // vẫn lưu tài liệu
        Assert.Empty(rag.Ingested);                        // nhưng không ingest (guard rỗng)
    }

    [Fact]
    public async Task Ingest_failure_returns_server_error_and_deletes_the_file()
    {
        var uow = new InMemoryUnitOfWork();
        var parser = new StubDocumentParser { Text = "Nội dung playbook" };
        var storage = new RecordingFileStorage();
        var rag = new RecordingRagIngestionService { ThrowOnIngest = true };

        var res = await Handler(uow, parser, storage, rag).Handle(Cmd(fileName: "pb.pdf"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
        Assert.Contains("Xử lý playbook thất bại", res.Error);
        Assert.Contains("playbooks/pb.pdf", storage.Deleted);   // bù trừ xoá file đã lưu
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
