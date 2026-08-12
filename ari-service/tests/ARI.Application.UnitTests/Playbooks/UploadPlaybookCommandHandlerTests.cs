using System;
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
}
