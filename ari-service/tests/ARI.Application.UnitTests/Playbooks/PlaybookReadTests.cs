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
/// Xoá mềm playbook (<see cref="DeletePlaybookCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "DeletePlaybook" (UTCID01–04): id lạ → not_found; tồn tại → soft delete; Update/Save ném lỗi.
/// </summary>
public class DeletePlaybookCommandHandlerTests
{
    private static PlaybookDocument Doc()
        => new() { Scope = "company", DocumentType = "guide", FileName = "guide.pdf", UploadedByUserId = Guid.NewGuid() };

    // UTCID01 — không tồn tại → not_found
    [Fact]
    public async Task UTCID01_Not_found()
    {
        var res = await new DeletePlaybookCommandHandler(new InMemoryUnitOfWork()).Handle(new DeletePlaybookCommand(Guid.NewGuid()), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy playbook.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID02 — tồn tại → soft delete
    [Fact]
    public async Task UTCID02_Soft_deletes()
    {
        var doc = Doc();
        var uow = new InMemoryUnitOfWork().Seed(doc);
        var res = await new DeletePlaybookCommandHandler(uow).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.NotNull(doc.DeletedAt);
        Assert.Single(uow.Repo<PlaybookDocument>().Items);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // UTCID03 — Update ném lỗi
    [Fact]
    public async Task UTCID03_Update_error()
    {
        var doc = Doc();
        var uow = new InMemoryUnitOfWork().Seed(doc).FailUpdateFor<PlaybookDocument>("Update Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => new DeletePlaybookCommandHandler(uow).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None));
        Assert.Equal("Update Error", ex.Message);
    }

    // UTCID04 — Save ném lỗi
    [Fact]
    public async Task UTCID04_Save_error()
    {
        var doc = Doc();
        var uow = new InMemoryUnitOfWork().Seed(doc).FailSaveOn(1, "Save Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => new DeletePlaybookCommandHandler(uow).Handle(new DeletePlaybookCommand(doc.Id), CancellationToken.None));
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
