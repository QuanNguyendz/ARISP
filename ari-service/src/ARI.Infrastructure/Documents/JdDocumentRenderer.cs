using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using ARI.Application.JdDocuments;
using ARI.Domain.Entities;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace ARI.Infrastructure.Documents
{
    /// <summary>
    /// Dựng file JD từ mẫu công ty (ADR-064). Cả hai bản xuất đọc CÙNG một
    /// <see cref="JdLayout"/> — nơi đã quyết định "in cái gì, thứ tự nào" — nên ở đây chỉ còn việc
    /// "vẽ ra sao".
    ///
    /// Logo là phần duy nhất có thể hỏng độc lập (file ảnh lỗi, định dạng lạ). Mọi chỗ chạm tới nó
    /// đều bọc try/catch và **bỏ qua ảnh rồi vẫn xuất file**: một logo hỏng không được phép chặn
    /// việc dựng JD, vì người dùng sẽ không hiểu vì sao "tải PDF" lại báo lỗi.
    /// </summary>
    public class JdDocumentRenderer : IJdDocumentRenderer
    {
        private readonly ILogger<JdDocumentRenderer>? _logger;

        public JdDocumentRenderer(ILogger<JdDocumentRenderer>? logger = null) => _logger = logger;

        // =============================================================== DOCX

        public Task<byte[]> RenderDocxAsync(
            JdTemplate template, JdDocument document, byte[]? logo, CancellationToken ct = default)
        {
            var layout = JdLayout.Build(template, document);
            var accent = layout.AccentColor.TrimStart('#');

            using var mem = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new Document(new Body());
                var body = main.Document.Body!;

                // ----- Đầu trang: logo và tên công ty NẰM CÙNG HÀNG -----
                // Dùng bảng 2 cột không viền: OpenXML không có "float", nên đây là cách duy nhất
                // đặt ảnh cạnh chữ mà vẫn giữ được căn dòng khi tên công ty dài.
                body.Append(HeaderRow(main, layout, logo, accent));

                // ----- Tiêu đề văn bản, canh giữa, có đường kẻ dưới -----
                body.Append(Text(layout.DocumentTitle, size: 26, bold: true, font: layout.FontFamily,
                                 spaceBefore: 240, center: true));
                body.Append(HorizontalRule(accent));

                // ----- Bảng thông tin: nhãn canh trái, dấu hai chấm thẳng cột -----
                foreach (var fact in layout.Facts)
                {
                    var p = new Paragraph(new ParagraphProperties(
                        new Tabs(new TabStop { Val = TabStopValues.Left, Position = 1800 }),
                        new SpacingBetweenLines { After = "40" }));
                    p.Append(Run(fact.Label, size: 20, font: layout.FontFamily));
                    p.Append(new Run(new TabChar()));
                    p.Append(Run(": " + fact.Value, size: 20, bold: true, font: layout.FontFamily));
                    body.Append(p);
                }

                // ----- Từng mục: tiêu đề là THANH MÀU ĐẶC chữ trắng -----
                foreach (var section in layout.Sections)
                {
                    ct.ThrowIfCancellationRequested();
                    body.Append(SectionBar(section.Title, accent, layout.FontFamily));

                    foreach (var line in section.Lines)
                        body.Append(Bullet(line, layout.FontFamily));
                }

                if (!string.IsNullOrWhiteSpace(layout.FooterNote))
                {
                    body.Append(HorizontalRule("CCCCCC"));
                    body.Append(Text(layout.FooterNote!, size: 16, color: "888888", font: layout.FontFamily));
                }

                main.Document.Save();
            }

            return Task.FromResult(mem.ToArray());
        }

        // ----- mảnh dựng DOCX -----

        private static Run Run(string text, int size, bool bold = false, string? color = null, string? font = null)
        {
            var props = new RunProperties();
            if (!string.IsNullOrWhiteSpace(font)) props.Append(new RunFonts { Ascii = font, HighAnsi = font });
            props.Append(new FontSize { Val = size.ToString() });          // nửa-point
            if (bold) props.Append(new Bold());
            if (!string.IsNullOrWhiteSpace(color)) props.Append(new Color { Val = color });

            return new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        }

        private static Paragraph Text(
            string text, int size, bool bold = false, string? color = null, string? font = null,
            int spaceBefore = 0, bool center = false)
        {
            var props = new ParagraphProperties(new SpacingBetweenLines
            {
                Before = spaceBefore.ToString(),
                After = "60",
            });
            if (center) props.Append(new Justification { Val = JustificationValues.Center });

            return new Paragraph(props, Run(text, size, bold, color, font));
        }

        /// <summary>
        /// Tiêu đề mục dạng thanh màu đặc, chữ trắng in hoa — dấu hiệu nhận dạng rõ nhất của khuôn JD
        /// doanh nghiệp. Trong OpenXML là một đoạn có <c>Shading</c> tô nền.
        /// </summary>
        private static Paragraph SectionBar(string title, string accentHex, string? font) =>
            new(new ParagraphProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accentHex },
                    new SpacingBetweenLines { Before = "260", After = "120" },
                    new Indentation { Left = "80", Right = "80" }),
                Run(title.ToUpperInvariant(), size: 21, bold: true, color: "FFFFFF", font: font));

        /// <summary>
        /// Hàng đầu trang: logo bên trái, tên + thông tin liên hệ công ty bên phải. Bảng 2 cột KHÔNG
        /// VIỀN — OpenXML không có khái niệm "float", nên bảng là cách duy nhất giữ hai khối cạnh
        /// nhau mà chữ vẫn xuống dòng đúng khi tên công ty dài.
        /// </summary>
        private Table HeaderRow(MainDocumentPart main, JdLayout layout, byte[]? logo, string accentHex)
        {
            var table = new Table(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                // Viền None ở cả 6 cạnh: thiếu một cạnh là Word tự vẽ lại đường kẻ mặc định.
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            var logoCell = new TableCell(new TableCellProperties(
                new TableCellWidth { Width = "1600", Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));

            var logoRun = TryBuildLogoRun(main, logo);
            logoCell.Append(logoRun != null ? new Paragraph(logoRun) : new Paragraph());

            var infoCell = new TableCell(new TableCellProperties(
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));

            infoCell.Append(Text(layout.CompanyName, size: 22, bold: true, color: accentHex, font: layout.FontFamily));
            foreach (var line in new[] { layout.CompanyAddress, layout.CompanyWebsite, layout.CompanyEmail }
                         .Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                infoCell.Append(Text(line!, size: 16, color: "666666", font: layout.FontFamily));
            }

            table.Append(new TableRow(logoCell, infoCell));
            return table;
        }

        private static Paragraph Bullet(string text, string? font) =>
            new(new ParagraphProperties(
                    new Indentation { Left = "360", Hanging = "180" },
                    new SpacingBetweenLines { After = "40" }),
                Run("• " + text, size: 20, font: font));

        /// <summary>Đường kẻ ngang — đoạn rỗng có viền dưới, cách làm chuẩn trong OpenXML.</summary>
        private static Paragraph HorizontalRule(string color) =>
            new(new ParagraphProperties(
                new ParagraphBorders(new BottomBorder
                {
                    Val = BorderValues.Single,
                    Size = 6,
                    Color = color,
                }),
                new SpacingBetweenLines { Before = "120", After = "120" }));

        /// <summary>
        /// Chèn logo vào .docx. Trả <c>null</c> nếu không có ảnh hoặc ảnh hỏng — nơi gọi bỏ qua và
        /// vẫn xuất file.
        /// </summary>
        private Run? TryBuildLogoRun(MainDocumentPart main, byte[]? logo)
        {
            if (logo is not { Length: > 0 }) return null;

            try
            {
                var part = main.AddImagePart(DetectImagePartType(logo));
                using (var stream = new MemoryStream(logo)) part.FeedData(stream);
                var relId = main.GetIdOfPart(part);

                // Khổ cố định 140x40pt (EMU: 1pt = 12700). Không đọc kích thước thật của ảnh: làm
                // vậy phải nạp thư viện ảnh, mà khung cố định cho bố cục ổn định hơn.
                const long cx = 140L * 12700, cy = 40L * 12700;

                var drawing = new Drawing(
                    new DW.Inline(
                        new DW.Extent { Cx = cx, Cy = cy },
                        new DW.DocProperties { Id = 1U, Name = "Logo" },
                        new A.Graphic(new A.GraphicFrameLocks { NoChangeAspect = true },
                            new A.GraphicData(
                                new PIC.Picture(
                                    new PIC.NonVisualPictureProperties(
                                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo" },
                                        new PIC.NonVisualPictureDrawingProperties()),
                                    new PIC.BlipFill(
                                        new A.Blip { Embed = relId },
                                        new A.Stretch(new A.FillRectangle())),
                                    new PIC.ShapeProperties(
                                        new A.Transform2D(
                                            new A.Offset { X = 0, Y = 0 },
                                            new A.Extents { Cx = cx, Cy = cy }),
                                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                    { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

                return new Run(drawing);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Không chèn được logo vào file JD .docx — xuất file không kèm logo.");
                return null;
            }
        }

        private static PartTypeInfo DetectImagePartType(byte[] bytes) =>
            // PNG bắt đầu bằng 0x89 'P' 'N' 'G'. Còn lại coi là JPEG — tầng trên chỉ nhận PNG/JPG.
            bytes.Length > 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                ? ImagePartType.Png
                : ImagePartType.Jpeg;

        // =============================================================== PDF

        public Task<byte[]> RenderPdfAsync(
            JdTemplate template, JdDocument document, byte[]? logo, CancellationToken ct = default)
        {
            var layout = JdLayout.Build(template, document);
            var (r, g, b) = JdLayout.HexToRgb(layout.AccentColor);
            var accent = XColor.FromArgb(r, g, b);

            PdfText.EnsureFontResolver();

            using var doc = new PdfDocument();

            // PdfSharpCore ném lỗi nếu phông không phân giải được. Người dùng gõ tên phông lạ vào
            // mẫu là chuyện thường, nên thử trước và rơi về Arial — chứ không để cả file chết.
            var fontName = SafeFontName(layout.FontFamily);

            var h1 = new XFont(fontName, 14, XFontStyle.Bold);
            var h2 = new XFont(fontName, 10, XFontStyle.Bold);
            var bodyFont = new XFont(fontName, 10, XFontStyle.Regular);
            var boldBody = new XFont(fontName, 10, XFontStyle.Bold);
            var smallFont = new XFont(fontName, 8, XFontStyle.Regular);

            const double margin = 50;
            const double barH = 17;          // chiều cao thanh tiêu đề mục
            var lineH = bodyFont.GetHeight() * 1.25;

            PdfPage page = null!;
            XGraphics gfx = null!;
            double pageH = 0, contentW = 0, y = 0;

            void StartPage()
            {
                page = doc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                pageH = page.Height.Point;
                contentW = page.Width.Point - 2 * margin;
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
            }

            void EnsureRoom(double needed)
            {
                if (y + needed <= pageH - margin) return;
                gfx.Dispose();
                StartPage();
            }

            void Write(string text, XFont font, XBrush brush, double indent = 0)
            {
                foreach (var line in PdfText.WrapParagraph(gfx, font, text, contentW - indent))
                {
                    EnsureRoom(lineH);
                    if (line.Length > 0) gfx.DrawString(line, font, brush, margin + indent, y);
                    y += lineH;
                }
            }

            StartPage();

            // ----- Đầu trang: logo bên trái, tên công ty ngay bên phải trên CÙNG hàng -----
            const double logoW = 108, logoH = 30;
            var textLeft = margin;
            var headerTop = y;

            if (logo is { Length: > 0 })
            {
                try
                {
                    using var img = XImage.FromStream(() => new MemoryStream(logo));
                    gfx.DrawImage(img, margin, y, logoW, logoH);
                    textLeft = margin + logoW + 12;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Không chèn được logo vào file JD .pdf — xuất file không kèm logo.");
                }
            }

            gfx.DrawString(layout.CompanyName, h2, new XSolidBrush(accent), textLeft, headerTop + 12);
            var contactLines = new[] { layout.CompanyAddress, layout.CompanyWebsite, layout.CompanyEmail }
                .Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            var cy = headerTop + 24;
            foreach (var line in contactLines)
            {
                gfx.DrawString(line!, smallFont, XBrushes.Gray, textLeft, cy);
                cy += 11;
            }

            y = Math.Max(headerTop + logoH, cy) + 18;

            // ----- Tiêu đề văn bản, CANH GIỮA + đường kẻ dưới -----
            var titleSize = gfx.MeasureString(layout.DocumentTitle, h1);
            gfx.DrawString(layout.DocumentTitle, h1, XBrushes.Black,
                margin + (contentW - titleSize.Width) / 2, y + h1.GetHeight());
            y += h1.GetHeight() + 8;
            gfx.DrawLine(new XPen(accent, 1.4), margin, y, margin + contentW, y);
            y += 16;

            // ----- Bảng thông tin: nhãn canh trái, dấu hai chấm THẲNG CỘT -----
            const double labelW = 90;
            foreach (var fact in layout.Facts)
            {
                EnsureRoom(lineH);
                gfx.DrawString(fact.Label, bodyFont, XBrushes.Black, margin, y);
                gfx.DrawString(": " + fact.Value, boldBody, XBrushes.Black, margin + labelW, y);
                y += lineH;
            }

            // ----- Từng mục: tiêu đề là THANH MÀU ĐẶC chữ trắng -----
            foreach (var section in layout.Sections)
            {
                ct.ThrowIfCancellationRequested();

                y += 14;
                // Thanh tiêu đề + ít nhất một dòng nội dung phải nằm cùng trang — tiêu đề mục đứng
                // một mình cuối trang là lỗi bố cục thấy ngay.
                EnsureRoom(barH + lineH);

                gfx.DrawRectangle(new XSolidBrush(accent), margin, y - 2, contentW, barH);
                gfx.DrawString(section.Title.ToUpperInvariant(), h2, XBrushes.White, margin + 8, y + barH - 7);
                y += barH + 8;

                foreach (var line in section.Lines)
                    Write("• " + line, bodyFont, XBrushes.Black, indent: 14);
            }

            if (!string.IsNullOrWhiteSpace(layout.FooterNote))
            {
                y += 14;
                EnsureRoom(lineH * 2);
                gfx.DrawLine(new XPen(XColors.LightGray, 0.8), margin, y, margin + contentW, y);
                y += 12;
                Write(layout.FooterNote!, smallFont, XBrushes.Gray);
            }

            gfx.Dispose();

            using var output = new MemoryStream();
            doc.Save(output, false);
            return Task.FromResult(output.ToArray());
        }

        /// <summary>Tên phông dùng được, hoặc Arial nếu phông trong mẫu không phân giải nổi.</summary>
        private string SafeFontName(string requested)
        {
            try
            {
                _ = new XFont(requested, 10, XFontStyle.Regular);
                return requested;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Phông '{Font}' trong mẫu JD không dùng được — rơi về Arial.", requested);
                return "Arial";
            }
        }
    }
}
