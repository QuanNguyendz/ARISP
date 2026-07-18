using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace ARISP.Application.Services
{
    /// <summary>
    /// Minimal language stub: counts Latin/English tokens in JD; majority English → "en", else "vi".
    /// </summary>
    public static class JobDescriptionLanguageDetector
    {
        private static readonly Regex EnglishToken = new(@"^[a-zA-Z]{3,}$", RegexOptions.Compiled);
        private static readonly Regex HtmlTagsRegex = new(@"<.*?>", RegexOptions.Compiled);

        public static string Detect(string jobDescription)
        {
            if (string.IsNullOrWhiteSpace(jobDescription))
                return "vi";

            // Bước 1: Loại bỏ các thẻ HTML để không bị đếm nhầm (ví dụ <ul>, <li>, <p>)
            string cleanText = HtmlTagsRegex.Replace(jobDescription, " ");
            
            // Bước 2: Giải mã các ký tự HTML entity (ví dụ &nbsp;, &amp;)
            cleanText = WebUtility.HtmlDecode(cleanText);

            var tokens = cleanText
                .Split(new[] { ' ', '\n', '\r', '\t', ',', '.', ';', ':', '(', ')', '[', ']', '/', '-', '&' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
                return "vi";

            var englishCount = tokens.Count(t => EnglishToken.IsMatch(t));
            return englishCount > tokens.Length / 4 ? "en" : "vi";
        }
    }
}
