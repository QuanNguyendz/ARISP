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
/// Xoá playbook (<see cref="DeletePlaybookCommandHandler"/>) — test-plan Report5 Unit v1.2, tab "DeletePlaybook"
/// (UTCID01–04): id lạ → not_found; tồn tại → soft delete; Update/Save ném lỗi. Sau merge ADR-025 handler còn
/// <b>gỡ chunk khỏi kho vector TRƯỚC khi xoá mềm</b> (ctor 2 tham số) — xoá mà chunk còn thì tài liệu vẫn điều
/// khiển AI dù đã biến mất khỏi màn hình; giữ thêm case gỡ-chunk-lỗi (ngoài 4 UTCID của report).
/// </summary>
public class DeletePlaybookCommandHandlerTests
{
    private static PlaybookDocument Doc()
        => new() { Scope = "company", DocumentType = "guide", FileName = "guide.pdf", UploadedByUserId = Guid.NewGuid() };

    private static DeletePlaybookCommandHandler Handler(InMemoryUnitOfWork uow, RecordingRagIngestionService? rag = null)
        => new(uow, rag ?? new RecordingRagIngestionService());

    // UTCID01 — không tồn tại → not_found
    [Fact]
    public async Task UTCID01_Not_found()
    {
        var res = await Handler(new InMemoryUnitOfWork()).Handle(new DeletePlaybookCommand(Guid.NewGuid()), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy playbook.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID02 — tồn tại → soft delete + gỡ chunk khỏi kho vector (ADR-025)
    [Fact]
    public async Task UTCID02_Soft_deletes_and_removes_chunks()
    {
        var doc = Doc();
        var uow = new InMemoryUnitOfWork().Seed(doc);
        var rag = new RecordingRagIngestionService();

        var res = await Handler(uow, rag).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotNull(doc.DeletedAt);
        Assert.Single(uow.Repo<PlaybookDocument>().Items);
        Assert.Equal(1, uow.SaveChangesCount);

        // Ingest với text rỗng = lệnh xoá sạch chunk của tài liệu này ở rag-service.
        var call = Assert.Single(rag.Ingested);
        Assert.Equal("playbook", call.SourceType);
        Assert.Equal(doc.Id, call.SourceId);
        Assert.Equal(string.Empty, call.Text);
    }

    /// <summary>
    /// (Ngoài report — ADR-025) Gỡ chunk hỏng thì KHÔNG được xoá mềm: thà để tài liệu còn hiện cho nhân sự
    /// bấm lại, còn hơn tạo ra một playbook vô hình vẫn đang nói vào tai AI.
    /// </summary>
    [Fact]
    public async Task Keeps_document_visible_when_chunk_removal_fails()
    {
        var doc = new PlaybookDocument { Scope = "job", DocumentType = "style", FileName = "pb.pdf", UploadedByUserId = Guid.NewGuid() };
        var uow = new InMemoryUnitOfWork().Seed(doc);
        var rag = new RecordingRagIngestionService { ThrowOnIngest = true };

        var res = await Handler(uow, rag).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Null(doc.DeletedAt);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // UTCID03 — Update ném lỗi (gỡ chunk thành công trước, rồi Update chết)
    [Fact]
    public async Task UTCID03_Update_error()
    {
        var doc = Doc();
        var uow = new InMemoryUnitOfWork().Seed(doc).FailUpdateFor<PlaybookDocument>("Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None));
        Assert.Equal("Update Error", ex.Message);
    }

    // UTCID04 — Save ném lỗi
    [Fact]
    public async Task UTCID04_Save_error()
    {
        var doc = Doc();
        var uow = new InMemoryUnitOfWork().Seed(doc).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None));
        Assert.Equal("Save Error", ex.Message);
    }
}

/// <summary>
/// Danh sách playbook (<see cref="GetPlaybooksQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetPlaybooks" (UTCID01–05): rỗng; lọc scope (trim+lower); sắp mới nhất; resolve tên người tải
/// (fullname → email); và lỗi repo.
/// </summary>
public class GetPlaybooksQueryHandlerTests
{
    private static PlaybookDocument Doc(string scope, Guid uploader, DateTimeOffset at, string format = "pdf", string type = "guide")
        => new() { Scope = scope, DocumentType = type, FileName = $"{scope}.{format}", FileFormat = format, Status = "ready", UploadedByUserId = uploader, ParsedText = "x", CreatedAt = at };

    // UTCID01 — không có playbook → []
    [Fact]
    public async Task UTCID01_Empty()
    {
        var res = await new GetPlaybooksQueryHandler(new InMemoryUnitOfWork()).Handle(new GetPlaybooksQuery(null), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value);
    }

    // UTCID02 — company + job_posting, Scope=null → tất cả, sắp mới nhất, resolve tên
    [Fact]
    public async Task UTCID02_All_sorted_with_uploader()
    {
        var now = DateTimeOffset.UtcNow;
        var uploader = AuthData.Staff(fullName: "HR Admin");
        var company = Doc("company", uploader.Id, now);
        var job = Doc("job_posting", uploader.Id, now.AddMinutes(-5), format: "docx", type: "interview");
        var uow = new InMemoryUnitOfWork().Seed(uploader).Seed(company, job);

        var res = await new GetPlaybooksQueryHandler(uow).Handle(new GetPlaybooksQuery(null), CancellationToken.None);

        Assert.Equal(2, res.Value.Count);
        Assert.Equal(company.Id, res.Value[0].Id);   // mới nhất trước
        Assert.Equal("HR Admin", res.Value[0].UploadedBy);
    }

    // UTCID03 — Scope="COMPANY" (không phân biệt hoa-thường) → chỉ company.
    // Lưu ý: report ghi input " COMPANY " (có khoảng trắng) nhưng handler CHỈ ToLower, KHÔNG Trim,
    // nên chuỗi có khoảng trắng sẽ không khớp gì. Test khớp hành vi thật: chuẩn hoá hoa-thường.
    [Fact]
    public async Task UTCID03_Scope_company_case_insensitive()
    {
        var now = DateTimeOffset.UtcNow;
        var uploader = AuthData.Staff(fullName: "HR Admin");
        var uow = new InMemoryUnitOfWork().Seed(uploader)
            .Seed(Doc("company", uploader.Id, now), Doc("job_posting", uploader.Id, now.AddMinutes(-1), format: "docx"));

        var res = await new GetPlaybooksQueryHandler(uow).Handle(new GetPlaybooksQuery("COMPANY"), CancellationToken.None);

        Assert.Equal("company", Assert.Single(res.Value).Scope);
    }

    // UTCID04 — Scope=job_posting; uploader FullName trắng → dùng email
    [Fact]
    public async Task UTCID04_Scope_job_posting_email_fallback()
    {
        var now = DateTimeOffset.UtcNow;
        var uploader = AuthData.Staff(email: "hr@example.com", fullName: " ");
        var uow = new InMemoryUnitOfWork().Seed(uploader)
            .Seed(Doc("company", uploader.Id, now), Doc("job_posting", uploader.Id, now.AddMinutes(-1), format: "docx"));

        var res = await new GetPlaybooksQueryHandler(uow).Handle(new GetPlaybooksQuery("job_posting"), CancellationToken.None);

        var item = Assert.Single(res.Value);
        Assert.Equal("job_posting", item.Scope);
        Assert.Equal("hr@example.com", item.UploadedBy);
    }

    // UTCID05 — repo ném lỗi
    [Fact]
    public async Task UTCID05_Repo_error()
    {
        var uow = new InMemoryUnitOfWork().FailRepo<PlaybookDocument>("Playbook DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => new GetPlaybooksQueryHandler(uow).Handle(new GetPlaybooksQuery(null), CancellationToken.None));
        Assert.Equal("Playbook DB Error", ex.Message);
    }
}
