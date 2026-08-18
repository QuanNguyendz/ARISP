using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Playbooks.Commands.DeletePlaybook;
using ARI.Application.Playbooks.Queries.GetPlaybooks;
using ARI.Application.UnitTests.Auth;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.Playbooks;

/// <summary>
/// Xoá playbook (<see cref="DeletePlaybookCommandHandler"/>, test-plan B29): đặt DeletedAt/UpdatedAt,
/// <b>và gỡ chunk khỏi kho vector</b> — xoá mà chunk còn thì tài liệu vẫn điều khiển AI dù đã biến mất
/// khỏi màn hình (ADR-025). Id lạ → NotFound.
/// </summary>
public class DeletePlaybookCommandHandlerTests
{
    [Fact]
    public async Task Soft_deletes_the_document_and_removes_its_chunks()
    {
        var doc = new PlaybookDocument { Scope = "job", DocumentType = "style", FileName = "pb.pdf", UploadedByUserId = Guid.NewGuid() };
        var uow = new InMemoryUnitOfWork().Seed(doc);
        var rag = new RecordingRagIngestionService();
        var before = DateTimeOffset.UtcNow;

        var res = await new DeletePlaybookCommandHandler(uow, rag).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(doc.DeletedAt);
        Assert.True(doc.UpdatedAt >= before);
        Assert.Single(uow.Repo<PlaybookDocument>().Items);   // soft delete — vẫn còn row
        Assert.Equal(1, uow.SaveChangesCount);

        // Ingest với text rỗng = lệnh xoá sạch chunk của tài liệu này ở rag-service.
        var call = Assert.Single(rag.Ingested);
        Assert.Equal("playbook", call.SourceType);
        Assert.Equal(doc.Id, call.SourceId);
        Assert.Equal(string.Empty, call.Text);
    }

    /// <summary>
    /// Gỡ chunk hỏng thì KHÔNG được xoá mềm: thà để tài liệu còn hiện cho nhân sự bấm lại, còn hơn
    /// tạo ra một playbook vô hình vẫn đang nói vào tai AI.
    /// </summary>
    [Fact]
    public async Task Keeps_document_visible_when_chunk_removal_fails()
    {
        var doc = new PlaybookDocument { Scope = "job", DocumentType = "style", FileName = "pb.pdf", UploadedByUserId = Guid.NewGuid() };
        var uow = new InMemoryUnitOfWork().Seed(doc);
        var rag = new RecordingRagIngestionService { ThrowOnIngest = true };

        var res = await new DeletePlaybookCommandHandler(uow, rag).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Null(doc.DeletedAt);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Unknown_id_returns_not_found()
    {
        var res = await new DeletePlaybookCommandHandler(new InMemoryUnitOfWork(), new RecordingRagIngestionService())
            .Handle(new DeletePlaybookCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
        Assert.Contains("Không tìm thấy playbook", res.Error);
    }
}

/// <summary>Danh sách playbook (<see cref="GetPlaybooksQueryHandler"/>, test-plan B29): lọc scope (case-insensitive), mới nhất trước, resolve tên người tải.</summary>
public class GetPlaybooksQueryHandlerTests
{
    private static PlaybookDocument Doc(string scope, Guid uploader, DateTimeOffset at)
        => new() { Scope = scope, DocumentType = "style", FileName = $"{scope}.pdf", FileFormat = "pdf", Status = "ready", UploadedByUserId = uploader, ParsedText = "nội dung lớn", CreatedAt = at };

    [Fact]
    public async Task Filters_by_scope_case_insensitively_and_resolves_uploader()
    {
        var now = DateTimeOffset.UtcNow;
        var uploader = AuthData.Staff(fullName: "Uploader");
        var jobDoc = Doc("job", uploader.Id, now);
        var orgDoc = Doc("org", uploader.Id, now.AddMinutes(-1));
        var uow = new InMemoryUnitOfWork().Seed(uploader).Seed(jobDoc, orgDoc);

        var res = await new GetPlaybooksQueryHandler(uow).Handle(new GetPlaybooksQuery("JOB"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        var item = Assert.Single(res.Value);
        Assert.Equal("job", item.Scope);
        Assert.Equal("Uploader", item.UploadedBy);
    }

    [Fact]
    public async Task No_scope_returns_all_sorted_newest_first()
    {
        var now = DateTimeOffset.UtcNow;
        var uploader = Guid.NewGuid();
        var newer = Doc("job", uploader, now);
        var older = Doc("org", uploader, now.AddMinutes(-5));
        var uow = new InMemoryUnitOfWork().Seed(newer, older);

        var res = await new GetPlaybooksQueryHandler(uow).Handle(new GetPlaybooksQuery(null), CancellationToken.None);

        Assert.Equal(2, res.Value.Count);
        Assert.Equal(newer.Id, res.Value[0].Id);   // mới nhất trước
    }
}
