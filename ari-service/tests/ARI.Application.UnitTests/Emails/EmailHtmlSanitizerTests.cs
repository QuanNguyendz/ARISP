using ARI.Application.Emails;
using Xunit;

namespace ARI.Application.UnitTests.Emails;

/// <summary>
/// Lọc HTML do nhân sự soạn tay (ADR-061, Phase 4). Nội dung này rời hệ thống tới hộp thư ứng
/// viên dưới tên miền công ty, nên bộ lọc nằm ở SERVER — cổng đặt ở trình duyệt thì một lời gọi
/// API thẳng là vượt qua.
/// </summary>
public class EmailHtmlSanitizerTests
{
    [Fact]
    public void Giu_lai_dinh_dang_co_ban_cua_thu_nghiep_vu()
    {
        var html = "<p>Chào <strong>Nguyễn Văn A</strong>,</p><ul><li>14:30 thứ Hai</li></ul>";

        var clean = EmailHtmlSanitizer.Sanitize(html);

        Assert.Contains("<strong>", clean);
        Assert.Contains("<li>", clean);
        Assert.Contains("Nguyễn Văn A", clean); // không phá tiếng Việt
    }

    [Fact]
    public void Giu_style_noi_tuyen_vi_thu_dien_tu_song_bang_no()
    {
        // Webmail bỏ qua <style> ở <head>, nên thư nghiệp vụ buộc phải dùng style nội tuyến.
        var clean = EmailHtmlSanitizer.Sanitize("<div style='padding: 20px; color: #333;'>Xin chào</div>");

        Assert.Contains("style", clean);
        Assert.Contains("Xin chào", clean);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>Xin chào", "script")]
    [InlineData("<img src='http://x/y.png' onerror='alert(1)'>Xin chào", "onerror")]
    [InlineData("<iframe src='http://evil'></iframe>Xin chào", "iframe")]
    [InlineData("<style>body{display:none}</style>Xin chào", "style>")]
    public void Loai_bo_the_va_thuoc_tinh_nguy_hiem(string html, string forbidden)
    {
        var clean = EmailHtmlSanitizer.Sanitize(html);

        Assert.DoesNotContain(forbidden, clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Xin chào", clean); // phần nội dung thật vẫn còn
    }

    [Fact]
    public void Chan_javascript_trong_href()
    {
        // Vẫn chạy được ở vài webmail cũ.
        var clean = EmailHtmlSanitizer.Sanitize("<a href='javascript:alert(1)'>Bấm vào đây</a>");

        Assert.DoesNotContain("javascript:", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bấm vào đây", clean);
    }

    [Fact]
    public void Cho_phep_lien_ket_http_https_va_mailto()
    {
        var clean = EmailHtmlSanitizer.Sanitize(
            "<a href='https://arisp.io.vn/xac-nhan'>Xác nhận</a> <a href='mailto:hr@corp.io'>HR</a>");

        Assert.Contains("https://arisp.io.vn/xac-nhan", clean);
        Assert.Contains("mailto:hr@corp.io", clean);
    }

    [Fact]
    public void Anh_ngoai_bi_loai_bo()
    {
        // Ảnh trỏ ra ngoài làm thư bị đánh dấu spam và là kênh theo dõi người nhận.
        var clean = EmailHtmlSanitizer.Sanitize("<p>Xin chào</p><img src='https://tracker/pixel.gif'>");

        Assert.DoesNotContain("<img", clean, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tieu_de_la_van_ban_thuan_va_khong_co_xuong_dong()
    {
        // Ký tự xuống dòng trong tiêu đề là đường tiêm header SMTP.
        var subject = EmailHtmlSanitizer.SanitizeSubject("<b>Thư mời</b>\r\nBcc: attacker@evil.com");

        Assert.DoesNotContain("<b>", subject);
        Assert.DoesNotContain("\n", subject);
        Assert.DoesNotContain("\r", subject);
        Assert.Contains("Thư mời", subject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Dau_vao_rong_tra_chuoi_rong(string? html)
    {
        Assert.Equal(string.Empty, EmailHtmlSanitizer.Sanitize(html));
        Assert.Equal(string.Empty, EmailHtmlSanitizer.SanitizeSubject(html));
    }
}
