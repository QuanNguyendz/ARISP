using System.Collections.Generic;
using ARI.Application.OnlineTest;
using Xunit;

namespace ARI.Application.UnitTests.OnlineTest;

/// <summary>
/// Kiểm tra ngân hàng câu hỏi khớp ngôn ngữ đã cấu hình cho vòng trắc nghiệm.
///
/// Điểm mấu chốt: nhận diện KHÔNG đối xứng. "Có dấu tiếng Việt" là bằng chứng chắc chắn, còn "không
/// có dấu" thì không chứng minh được là tiếng Anh (câu tiếng Việt không dấu, câu toàn mã nguồn...).
/// Test dưới đây khoá đúng ranh giới đó để không ai siết nhầm thành chặn oan.
/// </summary>
public class OnlineTestLanguageGuardTests
{
    // ---------- Normalize ----------

    [Theory]
    [InlineData("vi", "vi")]
    [InlineData("VI", "vi")]
    [InlineData("vi-VN", "vi")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("  EN  ", "en")]
    public void Normalize_dua_ve_vi_hoac_en(string input, string expected)
        => Assert.Equal(expected, OnlineTestLanguageGuard.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ja")]
    public void Normalize_gia_tri_la_tra_null_de_bo_qua_kiem_tra(string? input)
        => Assert.Null(OnlineTestLanguageGuard.Normalize(input));

    // ---------- Nhận diện chữ tiếng Việt ----------

    [Theory]
    [InlineData("Câu lệnh nào dùng để truy vấn?")]
    [InlineData("Đâu là ngôn ngữ backend?")]
    [InlineData("HTTP 404 nghĩa là gì")]      // chỉ một chữ "ĩ" cũng đủ
    public void Nhan_ra_chu_tieng_viet(string text)
        => Assert.True(OnlineTestLanguageGuard.HasVietnameseChars(text));

    [Theory]
    [InlineData("Which HTTP status means Not Found?")]
    [InlineData("SELECT * FROM users WHERE id = 1")]
    [InlineData("Cau hoi khong dau")]          // tiếng Việt không dấu — KHÔNG nhận ra được
    [InlineData("")]
    [InlineData(null)]
    public void Khong_ket_luan_khi_thieu_dau(string? text)
        => Assert.False(OnlineTestLanguageGuard.HasVietnameseChars(text));

    // ---------- Kiểm theo DÒNG (chỉ áp dụng khi cấu hình tiếng Anh) ----------

    [Fact]
    public void Cau_hinh_tieng_anh_thi_dong_tieng_viet_bi_tu_choi()
    {
        Assert.True(OnlineTestLanguageGuard.RowViolatesLanguage("en", "Đâu là ngôn ngữ backend?", "C#"));
    }

    [Fact]
    public void Cau_hinh_tieng_anh_bat_ca_khi_dau_nam_o_phuong_an()
    {
        // Câu hỏi tiếng Anh nhưng phương án tiếng Việt vẫn là đề lẫn ngôn ngữ.
        Assert.True(OnlineTestLanguageGuard.RowViolatesLanguage("en", "Which one is a backend language?", "Ngôn ngữ C#"));
    }

    [Fact]
    public void Cau_hinh_tieng_anh_va_dong_tieng_anh_thi_qua()
    {
        Assert.False(OnlineTestLanguageGuard.RowViolatesLanguage("en", "Which HTTP status means Not Found?", "404 500"));
    }

    [Theory]
    [InlineData("vi")]
    [InlineData(null)]
    public void Khong_kiem_theo_dong_khi_cau_hinh_khong_phai_tieng_anh(string? language)
    {
        // Cấu hình tiếng Việt mà chặn từng dòng "không có dấu" sẽ đánh trượt oan câu toàn mã nguồn.
        Assert.False(OnlineTestLanguageGuard.RowViolatesLanguage(language, "SELECT * FROM users"));
    }

    // ---------- Kiểm theo CẢ FILE (chỉ áp dụng khi cấu hình tiếng Việt) ----------

    [Fact]
    public void Cau_hinh_tieng_viet_ma_ca_file_khong_co_dau_thi_tu_choi()
    {
        var texts = new List<string>
        {
            "Which HTTP status means Not Found?",
            "What is a primary key?",
            "Which one is a backend language?",
        };

        var error = OnlineTestLanguageGuard.CheckFile("vi", texts);

        Assert.NotNull(error);
        Assert.Contains("Tiếng Việt", error);
    }

    [Fact]
    public void Chi_can_mot_cau_co_dau_la_chap_nhan_ca_file()
    {
        // Ngân hàng thật hay lẫn câu toàn mã nguồn/thuật ngữ — không được vì thế mà trượt.
        var texts = new List<string>
        {
            "SELECT * FROM users WHERE id = 1",
            "HTTP 404",
            "Câu nào đúng về chỉ mục?",
        };

        Assert.Null(OnlineTestLanguageGuard.CheckFile("vi", texts));
    }

    [Fact]
    public void File_qua_it_dong_thi_khong_ket_luan()
    {
        // 1–2 dòng không đủ tín hiệu: có thể là 2 câu hỏi về cú pháp SQL viết cho bài tiếng Việt.
        var texts = new List<string> { "SELECT * FROM users", "HTTP 404" };

        Assert.Null(OnlineTestLanguageGuard.CheckFile("vi", texts));
        Assert.Equal(3, OnlineTestLanguageGuard.MinRowsForVietnameseVerdict);
    }

    [Theory]
    [InlineData("en")]
    [InlineData(null)]
    public void Khong_kiem_ca_file_khi_cau_hinh_khong_phai_tieng_viet(string? language)
    {
        var texts = new List<string> { "What is a primary key?", "HTTP 404", "SELECT 1" };

        Assert.Null(OnlineTestLanguageGuard.CheckFile(language, texts));
    }

    [Fact]
    public void File_rong_khong_bao_loi_ngon_ngu()
    {
        Assert.Null(OnlineTestLanguageGuard.CheckFile("vi", new List<string>()));
    }
}
