using ARI.Domain.Constants;
using ARI.Domain.Entities;
using ARI.Infrastructure.Documents;
using UglyToad.PdfPig;
using Xunit;

namespace ARI.Infrastructure.UnitTests.Documents;

/// <summary>
/// PDF phải dựng bằng phông NHÚNG, không phụ thuộc phông của máy chạy. Image .NET Linux không có phông
/// nào — bộ phân giải mặc định của PdfSharpCore làm mọi lệnh xuất PDF chết với
/// "No Fonts installed on this device!" trên production, trong khi máy dev Windows vẫn chạy nhờ Arial.
/// </summary>
public class JdPdfFontTests
{
    private const string Body = "Phát triển hệ thống ứng dụng, tối ưu truy vấn cơ sở dữ liệu";

    [Theory]
    [InlineData("Arial", "Liberation Sans")]
    [InlineData("", "Liberation Sans")]
    [InlineData("Phông không tồn tại", "Liberation Sans")]
    [InlineData("Noto Sans", "Liberation Sans")]
    [InlineData("Times New Roman", "Liberation Serif")]
    [InlineData("Georgia", "Liberation Serif")]
    public async Task Jd_pdf_uses_the_bundled_font_for_any_template_font_name(string family, string expectedFont)
    {
        var pdf = await new JdDocumentRenderer().RenderPdfAsync(Template(family), Document(), logo: null);

        using var read = PdfDocument.Open(pdf);
        var page = read.GetPage(1);
        Assert.Contains(Body.Replace(" ", ""), page.Text.Replace(" ", ""));
        Assert.Contains(page.Letters, l => l.FontName.Contains(expectedFont));
    }

    [Fact]
    public async Task Approval_stamp_renders_vietnamese_text_with_the_bundled_font()
    {
        var stamper = new JdStampService();
        var pdf = await new JdDocumentRenderer().RenderPdfAsync(Template("Arial"), Document(), logo: null);

        var stamped = await stamper.StampApprovalAsync(pdf, "Nguyễn Văn Quân", DateTimeOffset.UtcNow);
        var fromText = await stamper.StampApprovalFromTextAsync("Mô tả công việc", Body, "Trần Thị Ánh", DateTimeOffset.UtcNow);

        using var readStamped = PdfDocument.Open(stamped);
        Assert.Contains("NguyễnVănQuân", readStamped.GetPage(1).Text.Replace(" ", ""));
        using var readText = PdfDocument.Open(fromText);
        var page = readText.GetPage(1);
        Assert.Contains("TrầnThịÁnh", page.Text.Replace(" ", ""));
        Assert.All(page.Letters, l => Assert.Contains("Liberation", l.FontName));
    }

    private static JdTemplate Template(string family) =>
        new() { CompanyName = "Công ty Cổ phần ARISP", FontFamily = family };

    private static JdDocument Document() => new()
    {
        Title = "Lập trình viên .NET cấp cao",
        Location = "Hà Nội",
        SalaryMin = 20_000_000,
        SalaryMax = 35_000_000,
        SalaryCurrency = "VND",
        SectionsJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [JdSectionKeys.Description] = Body,
            [JdSectionKeys.Requirements] = "- Ít nhất 5 năm kinh nghiệm",
        }),
    };
}
