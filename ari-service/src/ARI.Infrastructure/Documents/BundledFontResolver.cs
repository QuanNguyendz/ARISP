using System;
using System.Collections.Concurrent;
using System.IO;
using PdfSharpCore.Fonts;

namespace ARI.Infrastructure.Documents
{
    /// <summary>
    /// Bộ phân giải phông cho PdfSharpCore dùng phông NHÚNG trong assembly, không đọc phông của hệ điều hành.
    ///
    /// <c>PdfSharpCore.Utils.FontResolver</c> mặc định quét thư mục phông của máy — image
    /// <c>mcr.microsoft.com/dotnet/aspnet:8.0</c> (Debian) không có phông nào nên mọi lệnh xuất PDF chết với
    /// "No Fonts installed on this device!", trong khi DOCX (OpenXML, không đo chữ) vẫn chạy. Trên máy dev
    /// Windows lại chạy được nhờ Arial của hệ thống — đúng kiểu lỗi chỉ lộ ra sau khi deploy.
    ///
    /// Nhúng Liberation Sans/Serif (SIL OFL 1.1, xem <c>Fonts/LICENSE-Liberation.txt</c>): cùng số đo chữ
    /// với Arial/Times New Roman nên bố cục không xê dịch, đủ dấu tiếng Việt, và file PDF ra giống hệt nhau
    /// trên mọi môi trường. Tên phông trong mẫu JD là chữ tự do — họ serif rơi về Liberation Serif, còn lại
    /// về Liberation Sans, nên không tên nào làm hỏng lệnh xuất.
    /// </summary>
    internal sealed class BundledFontResolver : IFontResolver
    {
        private const string Sans = "LiberationSans";
        private const string Serif = "LiberationSerif";
        private const string ResourcePrefix = "ARI.Infrastructure.Documents.Fonts.";

        private static readonly string[] SerifHints =
            { "serif", "times", "georgia", "cambria", "garamond", "palatino", "book antiqua", "tinos" };

        private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.Ordinal);

        public string DefaultFontName => "Liberation Sans";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var style = (isBold, isItalic) switch
            {
                (true, true) => "BoldItalic",
                (true, false) => "Bold",
                (false, true) => "Italic",
                _ => "Regular",
            };
            return new FontResolverInfo($"{FamilyOf(familyName)}-{style}");
        }

        public byte[] GetFont(string faceName) => Cache.GetOrAdd(faceName, Load);

        /// <summary>Họ phông nhúng dùng cho một tên phông bất kỳ trong mẫu.</summary>
        internal static string FamilyOf(string? familyName)
        {
            var name = (familyName ?? string.Empty).Trim().ToLowerInvariant();
            if (name.Contains("sans")) return Sans;
            foreach (var hint in SerifHints)
                if (name.Contains(hint)) return Serif;
            return Sans;
        }

        private static byte[] Load(string faceName)
        {
            var assembly = typeof(BundledFontResolver).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourcePrefix + faceName + ".ttf")
                ?? throw new InvalidOperationException($"Thiếu phông nhúng '{faceName}.ttf' trong ARI.Infrastructure.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
