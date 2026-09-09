using System.Collections.Generic;
using ARI.Application.Common;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.Common;

/// <summary>
/// Gốc URL frontend — thứ quyết định mọi đường dẫn đặt vào email.
///
/// Vì sao khoá bằng test: đây là loại sai cấu hình **không tự lộ ra**. Thiếu biến thì thư vẫn gửi
/// bình thường, chỉ là nút bấm dẫn về máy lập trình viên; không lỗi, không log, tới khi ứng viên báo
/// link hỏng thì đã muộn. Trước đây 12 chỗ tự đọc cấu hình và chỗ nào cũng có sẵn
/// <c>?? "http://localhost:3000"</c> — sửa một chỗ không cứu được 11 chỗ kia.
/// </summary>
public class FrontendUrlsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] entries)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in entries) dict[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void Doc_dung_gia_tri_da_cau_hinh()
    {
        var config = Config(
            (FrontendUrls.CandidateKey, "https://arisp.io.vn"),
            (FrontendUrls.StaffKey, "https://staff.arisp.io.vn"));

        Assert.Equal("https://arisp.io.vn", FrontendUrls.Candidate(config));
        Assert.Equal("https://staff.arisp.io.vn", FrontendUrls.Staff(config));
    }

    [Fact]
    public void Bo_dau_gach_cuoi_de_ghep_duong_dan_khong_sinh_ra_hai_gach()
    {
        var config = Config((FrontendUrls.CandidateKey, "https://arisp.io.vn/"));

        // Nơi gọi luôn viết `$"{baseUrl}/portal/..."` — giữ dấu gạch cuối là ra `//portal`.
        Assert.Equal("https://arisp.io.vn", FrontendUrls.Candidate(config));
    }

    [Fact]
    public void KHONG_tu_dat_mac_dinh_localhost()
    {
        // Điểm cốt lõi: thiếu cấu hình thì trả RỖNG, không âm thầm thay bằng localhost. Bước kiểm
        // lúc boot của ARI.API mới là nơi chặn — và nó chặn bằng cách không cho app khởi động.
        var empty = Config();

        Assert.Equal(string.Empty, FrontendUrls.Candidate(empty));
        Assert.Equal(string.Empty, FrontendUrls.Staff(empty));
    }

    [Fact]
    public void Chuoi_rong_coi_nhu_chua_cau_hinh()
    {
        // `appsettings.json` để hai khoá này rỗng có chủ đích. Chuỗi rỗng KHÁC null, nên nếu chỉ
        // kiểm `?? ` thì một origin rỗng sẽ lọt xuống tận CORS và email.
        var blank = Config((FrontendUrls.CandidateKey, ""), (FrontendUrls.StaffKey, "   "));

        Assert.Equal(string.Empty, FrontendUrls.Candidate(blank));
        Assert.Equal(string.Empty, FrontendUrls.Staff(blank));
    }

    [Fact]
    public void Cong_ung_vien_thieu_thi_muon_tam_cong_nhan_su()
    {
        // Thà một link đúng máy chủ nhưng sai đường dẫn, còn hơn một link về localhost: cái đầu tự
        // lộ ra khi bấm, cái sau trông vẫn bình thường với người gửi.
        var config = Config((FrontendUrls.StaffKey, "https://staff.arisp.io.vn"));

        Assert.Equal("https://staff.arisp.io.vn", FrontendUrls.Candidate(config));
    }

    [Fact]
    public void Con_doc_duoc_ten_khoa_cu_cua_cong_nhan_su()
    {
        // Vài môi trường vẫn khai `Auth:AdminFrontendUrl`; bỏ qua nó là làm hỏng chính những nơi
        // đang chạy đúng.
        var config = Config((FrontendUrls.LegacyStaffKey, "https://staff.arisp.io.vn"));

        Assert.Equal("https://staff.arisp.io.vn", FrontendUrls.Staff(config));
    }
}
