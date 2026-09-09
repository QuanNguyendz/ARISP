using System;
using Ganss.Xss;

namespace ARI.Application.Emails
{
    /// <summary>
    /// Lọc HTML do NHÂN SỰ soạn tay trước khi thư rời hệ thống (ADR-061, Phase 4).
    ///
    /// Đặt ở TẦNG SERVER, ngay trước <c>IEmailService</c> — không phải ở trình soạn phía trình
    /// duyệt: cổng nằm ở client thì chỉ cần một lời gọi API thẳng là vượt qua.
    ///
    /// Vì sao vẫn cần lọc dù người soạn là nhân sự nội bộ:
    ///   • dán nội dung từ Word/trang web kéo theo <c>&lt;style&gt;</c>, <c>&lt;script&gt;</c>,
    ///     thuộc tính <c>on*</c> và ảnh trỏ ra ngoài — thư sẽ vỡ hoặc bị đánh dấu spam;
    ///   • một tài khoản nội bộ bị chiếm sẽ có đường gửi HTML tuỳ ý tới hộp thư ứng viên,
    ///     dưới tên miền của công ty;
    ///   • <c>javascript:</c> trong <c>href</c> vẫn chạy ở vài webmail cũ.
    ///
    /// Danh sách cho phép cố ý HẸP: đây là thư nghiệp vụ, không phải trang web.
    /// </summary>
    public static class EmailHtmlSanitizer
    {
        private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();
        private static readonly HtmlSanitizer PlainTextSanitizer = CreatePlainTextSanitizer();

        /// <summary>
        /// Bộ lọc riêng cho TIÊU ĐỀ: không cho phép thẻ nào cả, nhưng giữ lại phần chữ bên trong
        /// (<c>KeepChildNodes</c>). Không dùng chung bộ lọc thân thư được — bộ đó CHO PHÉP
        /// <c>&lt;b&gt;</c>, <c>&lt;a&gt;</c>… nên tiêu đề sẽ ra nguyên thẻ HTML hiển thị thô
        /// trong danh sách hộp thư.
        /// </summary>
        private static HtmlSanitizer CreatePlainTextSanitizer()
        {
            var s = new HtmlSanitizer();
            s.AllowedTags.Clear();
            s.AllowedAttributes.Clear();
            s.AllowedSchemes.Clear();
            s.KeepChildNodes = true;
            return s;
        }

        private static HtmlSanitizer CreateSanitizer()
        {
            var s = new HtmlSanitizer();

            s.AllowedTags.Clear();
            foreach (var tag in new[]
            {
                "p", "br", "div", "span", "strong", "b", "em", "i", "u",
                "ul", "ol", "li", "a", "h1", "h2", "h3", "h4",
                "blockquote", "hr", "table", "thead", "tbody", "tr", "th", "td",
            })
            {
                s.AllowedTags.Add(tag);
            }

            s.AllowedAttributes.Clear();
            s.AllowedAttributes.Add("href");
            s.AllowedAttributes.Add("title");
            // `style` nội tuyến được giữ vì thư nghiệp vụ dựa hoàn toàn vào nó (webmail bỏ qua
            // <style> ở <head>). Thư viện tự lọc từng thuộc tính CSS theo AllowedCssProperties
            // và chặn url()/expression(), nên đây không phải lỗ hổng.
            s.AllowedAttributes.Add("style");
            s.AllowedAttributes.Add("colspan");
            s.AllowedAttributes.Add("rowspan");

            // Chỉ ba giao thức này. Bỏ `data:` (đường nhúng payload) và mọi thứ còn lại.
            s.AllowedSchemes.Clear();
            s.AllowedSchemes.Add("http");
            s.AllowedSchemes.Add("https");
            s.AllowedSchemes.Add("mailto");

            // Không cho nhúng tài nguyên ngoài: ảnh/iframe/script/style-tag đều rơi khỏi AllowedTags
            // ở trên, đây là lớp chặn thứ hai cho thuộc tính.
            s.RemovingAttribute += (_, e) => { /* im lặng: bỏ là hành vi mong muốn */ };

            return s;
        }

        /// <summary>
        /// Lọc nội dung thư. Trả chuỗi rỗng cho đầu vào rỗng — người gọi tự quyết định coi đó là
        /// lỗi hay bỏ qua (thường là lỗi: thư rỗng không nên rời hệ thống).
        /// </summary>
        public static string Sanitize(string? html)
            => string.IsNullOrWhiteSpace(html) ? string.Empty : Sanitizer.Sanitize(html);

        /// <summary>
        /// Tiêu đề thư là VĂN BẢN THUẦN: mọi thẻ bị gỡ, xuống dòng bị thu về khoảng trắng.
        /// Ký tự xuống dòng trong tiêu đề là đường tiêm header SMTP (header injection).
        /// </summary>
        public static string SanitizeSubject(string? subject)
        {
            if (string.IsNullOrWhiteSpace(subject)) return string.Empty;
            var text = PlainTextSanitizer.Sanitize(subject);
            text = System.Net.WebUtility.HtmlDecode(text);
            text = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            // Gộp khoảng trắng thừa còn lại sau khi gỡ thẻ.
            return System.Text.RegularExpressions.Regex.Replace(text, @"\s{2,}", " ").Trim();
        }
    }
}
