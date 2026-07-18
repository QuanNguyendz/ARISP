using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Utils;

namespace ARISP.Infrastructure.Documents
{
    /// <summary>
    /// Đóng dấu duyệt trực quan lên file JD bằng PdfSharpCore (chạy tốt trên Linux nhờ
    /// FontResolver mặc định kèm sẵn font Arial — hỗ trợ ký tự tiếng Việt).
    /// PDF gốc: vẽ dấu lên trang đầu. DOCX: render text thành PDF mới rồi đóng dấu.
    /// </summary>
    public class JdStampService : IJdStampService
    {
        private static readonly object _fontLock = new();
        private static bool _fontReady;

        private static void EnsureFontResolver()
        {
            if (_fontReady) return;
            lock (_fontLock)
            {
                if (_fontReady) return;
                GlobalFontSettings.FontResolver ??= new FontResolver();
                _fontReady = true;
            }
        }

        public Task<byte[]> StampApprovalAsync(byte[] pdfBytes, string approverName, DateTimeOffset approvedAt, CancellationToken ct = default)
        {
            if (pdfBytes == null || pdfBytes.Length == 0)
                throw new ArgumentException("PDF rỗng, không thể đóng dấu.", nameof(pdfBytes));

            EnsureFontResolver();

            using var input = new MemoryStream(pdfBytes);
            using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            if (doc.PageCount == 0)
                throw new NotSupportedException("PDF không có trang nào để đóng dấu.");

            var page = doc.Pages[0];
            using (var gfx = XGraphics.FromPdfPage(page))
            {
                DrawStamp(gfx, page.Width.Point, approverName, approvedAt);
            }

            using var output = new MemoryStream();
            doc.Save(output, false);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(output.ToArray());
        }

        public Task<byte[]> StampApprovalFromTextAsync(string title, string bodyText, string approverName, DateTimeOffset approvedAt, CancellationToken ct = default)
        {
            EnsureFontResolver();

            using var doc = new PdfDocument();
            var titleFont = new XFont("Arial", 15, XFontStyle.Bold);
            var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);

            const double margin = 50;
            var lineH = bodyFont.GetHeight() * 1.15;

            PdfPage page = null!;
            XGraphics gfx = null!;
            double pageW = 0, pageH = 0, contentW = 0, y = 0;

            void StartPage(bool first)
            {
                page = doc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                pageW = page.Width.Point;
                pageH = page.Height.Point;
                contentW = pageW - 2 * margin;
                gfx = XGraphics.FromPdfPage(page);
                if (first)
                {
                    gfx.DrawString(
                        string.IsNullOrWhiteSpace(title) ? "Job Description" : title,
                        titleFont, XBrushes.Black,
                        new XRect(margin, margin, contentW - 150, 30), XStringFormats.TopLeft);
                    DrawStamp(gfx, pageW, approverName, approvedAt);
                    y = margin + 120; // chừa chỗ cho tiêu đề + con dấu góc trên phải
                }
                else
                {
                    y = margin;
                }
            }

            StartPage(true);

            var raw = (bodyText ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            foreach (var paragraph in raw.Split('\n'))
            {
                ct.ThrowIfCancellationRequested();
                var lines = WrapParagraph(gfx, bodyFont, paragraph, contentW);
                foreach (var line in lines)
                {
                    if (y + lineH > pageH - margin)
                    {
                        gfx.Dispose();
                        StartPage(false);
                    }
                    if (line.Length > 0)
                        gfx.DrawString(line, bodyFont, XBrushes.Black, margin, y);
                    y += lineH;
                }
            }

            gfx.Dispose();

            using var output = new MemoryStream();
            doc.Save(output, false);
            return Task.FromResult(output.ToArray());
        }

        /// <summary>Vẽ con dấu "ĐÃ DUYỆT" ở góc trên phải của trang (toạ độ điểm/point).</summary>
        private static void DrawStamp(XGraphics gfx, double pageW, string approverName, DateTimeOffset approvedAt)
        {
            const double boxW = 250;
            const double boxH = 92;
            const double pad = 18;

            var x = pageW - boxW - pad;
            if (x < pad) x = pad;
            var y = pad;
            var rect = new XRect(x, y, boxW, boxH);

            var red = XColor.FromArgb(200, 30, 30);
            var fill = XColor.FromArgb(28, 200, 30, 30); // đỏ rất nhạt
            var dark = XColor.FromArgb(40, 40, 40);

            gfx.DrawRoundedRectangle(new XPen(red, 1.6), new XSolidBrush(fill), rect, new XSize(10, 10));

            var hFont = new XFont("Arial", 12, XFontStyle.Bold);
            var bFont = new XFont("Arial", 9, XFontStyle.Regular);
            var sFont = new XFont("Arial", 11, XFontStyle.BoldItalic);

            var tx = x + 12;
            var ty = y + 18;
            gfx.DrawString("ĐÃ DUYỆT — HR LEADER", hFont, new XSolidBrush(red), tx, ty);
            ty += 18;
            gfx.DrawString($"Người duyệt: {approverName}", bFont, new XSolidBrush(dark), tx, ty);
            ty += 15;
            gfx.DrawString($"Ngày: {approvedAt.ToLocalTime():dd/MM/yyyy HH:mm}", bFont, new XSolidBrush(dark), tx, ty);
            ty += 20;
            gfx.DrawString($"Chữ ký: {approverName}", sFont, new XSolidBrush(red), tx, ty);
        }

        /// <summary>Tự xuống dòng một đoạn theo bề rộng tối đa; tách từ quá dài theo ký tự.</summary>
        private static List<string> WrapParagraph(XGraphics gfx, XFont font, string text, double maxW)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) { result.Add(string.Empty); return result; }

            var line = new StringBuilder();
            foreach (var word in text.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (gfx.MeasureString(candidate, font).Width <= maxW)
                {
                    if (line.Length > 0) line.Append(' ');
                    line.Append(word);
                    continue;
                }

                if (line.Length > 0) { result.Add(line.ToString()); line.Clear(); }

                if (gfx.MeasureString(word, font).Width <= maxW)
                {
                    line.Append(word);
                }
                else
                {
                    // Từ dài hơn cả dòng → tách theo ký tự.
                    var chunk = new StringBuilder();
                    foreach (var ch in word)
                    {
                        if (gfx.MeasureString(chunk.ToString() + ch, font).Width <= maxW)
                        {
                            chunk.Append(ch);
                        }
                        else
                        {
                            if (chunk.Length > 0) result.Add(chunk.ToString());
                            chunk.Clear();
                            chunk.Append(ch);
                        }
                    }
                    if (chunk.Length > 0) line.Append(chunk.ToString());
                }
            }

            if (line.Length > 0) result.Add(line.ToString());
            if (result.Count == 0) result.Add(string.Empty);
            return result;
        }
    }
}
