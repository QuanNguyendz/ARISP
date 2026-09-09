using System.Collections.Generic;
using System.Text;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Utils;

namespace ARI.Infrastructure.Documents
{
    /// <summary>
    /// Hai việc mà mọi chỗ dựng PDF bằng PdfSharpCore đều cần: nạp bộ phân giải phông, và tự xuống
    /// dòng theo bề rộng.
    ///
    /// Tách ra khỏi <see cref="JdStampService"/> khi ADR-064 thêm bộ dựng JD thứ hai — chép sang là
    /// có hai bản tự xuống dòng, và lần sửa sau chỉ một bản được sửa.
    /// </summary>
    internal static class PdfText
    {
        private static readonly object FontLock = new();
        private static bool _fontReady;

        /// <summary>
        /// Nạp <c>FontResolver</c> của PdfSharpCore — bắt buộc để chạy trên Linux (container không
        /// có phông hệ thống của Windows). Idempotent, gọi bao nhiêu lần cũng được.
        /// </summary>
        public static void EnsureFontResolver()
        {
            if (_fontReady) return;
            lock (FontLock)
            {
                if (_fontReady) return;
                GlobalFontSettings.FontResolver ??= new FontResolver();
                _fontReady = true;
            }
        }

        /// <summary>Tự xuống dòng một đoạn theo bề rộng tối đa; tách từ quá dài theo ký tự.</summary>
        public static List<string> WrapParagraph(XGraphics gfx, XFont font, string text, double maxW)
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
