using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.JdTemplates;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JdTemplates;

/// <summary>Dựng đầu vào hợp lệ + serialize mục JD cho test mẫu JD của công ty (ADR-064).</summary>
internal static class JdTemplateFixture
{
    public static List<JdTemplateSection> ValidSections() => JdTemplateSection.Defaults();

    public static UpdateJdTemplateInput Input(string company = "ARISP JSC", List<JdTemplateSection>? sections = null)
        => new(company, "Hà Nội", "https://arisp.io", "hr@arisp.io", "#2563EB", "Arial", null, sections ?? ValidSections());

    public static string SerializeSections(IEnumerable<JdTemplateSection> sections) =>
        System.Text.Json.JsonSerializer.Serialize(sections,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
}

/// <summary>
/// Đọc mẫu JD (<see cref="GetJdTemplateQueryHandler"/>, ADR-064): chưa cấu hình thì trả bộ MẶC ĐỊNH trong bộ nhớ
/// để renderer luôn dựng được; đã cấu hình thì trả kèm URL logo tính ở server.
/// </summary>
public class GetJdTemplateQueryHandlerTests
{
    [Fact]
    public async Task UTCID01_Returns_defaults_when_unconfigured()
    {
        var res = await new GetJdTemplateQueryHandler(new InMemoryUnitOfWork(), new RecordingFileStorage())
            .Handle(new GetJdTemplateQuery(), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.NotEmpty(res.Value.Sections);
        Assert.Null(res.Value.Id);
    }

    [Fact]
    public async Task UTCID02_Returns_stored_template_with_logo_url()
    {
        var tmpl = new JdTemplate
        {
            Id = Guid.NewGuid(), CompanyName = "ARISP JSC", LogoStorageKey = "branding/logo.png",
            AccentColor = "#2563EB", FontFamily = "Arial",
            SectionsJson = JdTemplateFixture.SerializeSections(JdTemplateFixture.ValidSections()), UpdatedAt = DateTimeOffset.UtcNow,
        };
        var uow = new InMemoryUnitOfWork().Seed(tmpl);

        var res = await new GetJdTemplateQueryHandler(uow, new RecordingFileStorage())
            .Handle(new GetJdTemplateQuery(), CancellationToken.None);

        Assert.Equal("ARISP JSC", res.Value.CompanyName);
        Assert.Equal("/files/branding/logo.png", res.Value.LogoUrl);
    }
}

/// <summary>
/// Cấu hình mẫu JD (<see cref="UpdateJdTemplateCommandHandler"/>, ADR-064): tên công ty bắt buộc; danh sách mục hợp lệ
/// (khoá cố định, có ít nhất một mục bật); tạo mới nếu chưa có, sửa tại chỗ nếu đã có.
/// </summary>
public class UpdateJdTemplateCommandHandlerTests
{
    [Fact]
    public async Task UTCID01_Blank_company_rejected()
    {
        var res = await new UpdateJdTemplateCommandHandler(new InMemoryUnitOfWork())
            .Handle(new UpdateJdTemplateCommand(JdTemplateFixture.Input(company: "   "), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Tên công ty", res.Error);
    }

    [Fact]
    public async Task UTCID02_Empty_sections_rejected()
    {
        var res = await new UpdateJdTemplateCommandHandler(new InMemoryUnitOfWork())
            .Handle(new UpdateJdTemplateCommand(JdTemplateFixture.Input(sections: new List<JdTemplateSection>()), Guid.NewGuid()),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("ít nhất một mục", res.Error);
    }

    [Fact]
    public async Task UTCID03_Unknown_section_key_rejected()
    {
        var bad = new List<JdTemplateSection> { new() { Key = "bogus", Title = "Lung tung", Enabled = true } };

        var res = await new UpdateJdTemplateCommandHandler(new InMemoryUnitOfWork())
            .Handle(new UpdateJdTemplateCommand(JdTemplateFixture.Input(sections: bad), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("không hợp lệ", res.Error);
    }

    [Fact]
    public async Task UTCID04_Creates_template_when_none()
    {
        var uow = new InMemoryUnitOfWork();

        var res = await new UpdateJdTemplateCommandHandler(uow)
            .Handle(new UpdateJdTemplateCommand(JdTemplateFixture.Input(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        var saved = Assert.Single(uow.Repo<JdTemplate>().Items);
        Assert.Equal("ARISP JSC", saved.CompanyName);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UTCID05_Edits_existing_template()
    {
        var tmpl = new JdTemplate { Id = Guid.NewGuid(), CompanyName = "Old Co", UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var uow = new InMemoryUnitOfWork().Seed(tmpl);

        var res = await new UpdateJdTemplateCommandHandler(uow)
            .Handle(new UpdateJdTemplateCommand(JdTemplateFixture.Input(company: "New Co"), Guid.NewGuid()), CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Single(uow.Repo<JdTemplate>().Items);   // không tạo bản mới
        Assert.Equal("New Co", tmpl.CompanyName);
    }
}

/// <summary>
/// Tải logo công ty (<see cref="UploadCompanyLogoCommandHandler"/>, ADR-064): chặn rỗng / quá 2MB / không phải
/// PNG-JPG (hai bộ dựng DOCX+PDF chỉ đọc được PNG/JPEG); hợp lệ thì lưu file + gắn LogoStorageKey vào mẫu.
/// </summary>
public class UploadCompanyLogoCommandHandlerTests
{
    [Fact]
    public async Task UTCID01_Empty_rejected()
    {
        var res = await new UploadCompanyLogoCommandHandler(new InMemoryUnitOfWork(), new RecordingFileStorage())
            .Handle(new UploadCompanyLogoCommand(Array.Empty<byte>(), "logo.png", "image/png", Guid.NewGuid()),
                CancellationToken.None);

        Assert.True(res.IsFailure);
    }

    [Fact]
    public async Task UTCID02_Wrong_type_rejected()
    {
        var res = await new UploadCompanyLogoCommandHandler(new InMemoryUnitOfWork(), new RecordingFileStorage())
            .Handle(new UploadCompanyLogoCommand(new byte[] { 1, 2, 3 }, "logo.svg", "image/svg+xml", Guid.NewGuid()),
                CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("PNG hoặc JPG", res.Error);
    }

    [Fact]
    public async Task UTCID03_Png_saved_and_persisted()
    {
        var uow = new InMemoryUnitOfWork();
        var storage = new RecordingFileStorage();

        var res = await new UploadCompanyLogoCommandHandler(uow, storage)
            .Handle(new UploadCompanyLogoCommand(new byte[] { 1, 2, 3 }, "logo.png", "image/png", Guid.NewGuid()),
                CancellationToken.None);

        Assert.True(res.IsSuccess, res.Error);
        Assert.Single(storage.Saved);
        var tmpl = Assert.Single(uow.Repo<JdTemplate>().Items);
        Assert.False(string.IsNullOrEmpty(tmpl.LogoStorageKey));
    }
}
