using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ARI.Application.JdDocuments;
using ARI.Domain.Constants;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JdDocuments;

/// <summary>
/// Bố cục bản mô tả công việc (ADR-064). Đây là nơi quyết định "in cái gì, thứ tự nào" — tách khỏi
/// renderer nên test được mà không phải chạm tới OpenXML hay PdfSharpCore.
/// </summary>
public class JdLayoutTests
{
    private static JdTemplate Template(IEnumerable<JdTemplateSection>? sections = null) => new()
    {
        CompanyName = "Eastern Sun",
        CompanyAddress = "Hà Nội",
        AccentColor = "#123456",
        FontFamily = "Arial",
        SectionsJson = JsonSerializer.Serialize(
            sections ?? JdTemplateSection.Defaults(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)),
    };

    private static JdDocument Document(Dictionary<string, string>? sections = null) => new()
    {
        Title = "Backend Developer",
        Department = "Engineering",
        Vacancies = 2,
        SalaryMin = 20_000_000,
        SalaryMax = 30_000_000,
        SalaryCurrency = "VND",
        SectionsJson = JsonSerializer.Serialize(
            sections ?? new Dictionary<string, string> { [JdSectionKeys.Description] = "Viết API" },
            new JsonSerializerOptions(JsonSerializerDefaults.Web)),
    };

    [Fact]
    public void Muc_theo_dung_thu_tu_cua_mau_va_bo_muc_bi_tat()
    {
        var sections = new List<JdTemplateSection>
        {
            new() { Key = JdSectionKeys.Requirements, Title = "Yêu cầu", Enabled = true },
            new() { Key = JdSectionKeys.Description,  Title = "Mô tả",   Enabled = true },
            new() { Key = JdSectionKeys.Benefits,     Title = "Quyền lợi", Enabled = false },
        };
        var content = new Dictionary<string, string>
        {
            [JdSectionKeys.Description] = "Viết API",
            [JdSectionKeys.Requirements] = "Biết C#",
            [JdSectionKeys.Benefits] = "Thưởng tháng 13",
        };

        var layout = JdLayout.Build(Template(sections), Document(content));

        // Đúng thứ tự MẪU quy định, không phải thứ tự người soạn gõ.
        Assert.Equal(new[] { "Yêu cầu", "Mô tả" }, layout.Sections.Select(s => s.Title).ToArray());
    }

    [Fact]
    public void Muc_bat_nhung_chua_viet_gi_thi_khong_in_tieu_de_trong()
    {
        var layout = JdLayout.Build(
            Template(), Document(new Dictionary<string, string> { [JdSectionKeys.Description] = "Viết API" }));

        // Mẫu mặc định bật 5 mục, nhưng chỉ một mục có nội dung.
        Assert.Single(layout.Sections);
        Assert.Equal("Mô tả công việc", layout.Sections[0].Title);
    }

    [Fact]
    public void Noi_dung_nhieu_dong_tach_thanh_tung_gach_dau_dong_va_bo_dau_liet_ke_thua()
    {
        var layout = JdLayout.Build(Template(), Document(new Dictionary<string, string>
        {
            [JdSectionKeys.Description] = "- Viết API\n\n* Tối ưu truy vấn\n   \n• Review code",
        }));

        Assert.Equal(
            new[] { "Viết API", "Tối ưu truy vấn", "Review code" },
            layout.Sections[0].Lines.ToArray());
    }

    [Fact]
    public void Truong_trong_bi_bo_khoi_bang_thong_tin_nhanh()
    {
        var doc = Document();
        doc.Location = null;
        doc.EmploymentType = "   ";

        var layout = JdLayout.Build(Template(), doc);

        Assert.DoesNotContain(layout.Facts, f => f.Label == "Địa chỉ");
        Assert.DoesNotContain(layout.Facts, f => f.Label == "Thời gian");
        Assert.Contains(layout.Facts, f => f.Label == "Bộ phận" && f.Value == "Engineering");
        Assert.Contains(layout.Facts, f => f.Label == "Số lượng" && f.Value == "2");
    }

    [Fact]
    public void Ten_vi_tri_la_DONG_DAU_cua_bang_thong_tin_chu_khong_phai_tieu_de_rieng()
    {
        // Bố cục bám khuôn JD doanh nghiệp: tiêu đề canh giữa là tên LOẠI văn bản
        // ("THÔNG TIN TUYỂN DỤNG"), còn tên vị trí nằm trong bảng thông tin ngay dưới.
        var template = Template();
        template.DocumentTitle = "THÔNG TIN TUYỂN DỤNG";

        var layout = JdLayout.Build(template, Document());

        Assert.Equal("THÔNG TIN TUYỂN DỤNG", layout.DocumentTitle);
        Assert.Equal("Vị trí", layout.Facts[0].Label);
        Assert.Equal("Backend Developer", layout.Facts[0].Value);
    }

    [Fact]
    public void Truong_phan_loai_in_ra_NHAN_nguoi_doc_duoc_chu_khong_phai_khoa_may()
    {
        // File JD là văn bản gửi ra ngoài công ty — in thẳng khoá máy vào đó thì ứng viên đọc được
        // "Thời gian: full_time, Cấp bậc: middle".
        var doc = Document();
        doc.EmploymentType = "full_time";
        doc.ExperienceLevel = "middle";
        doc.WorkMode = "onsite";

        var layout = JdLayout.Build(Template(), doc);

        Assert.Contains(layout.Facts, f => f.Label == "Thời gian" && f.Value == "Toàn thời gian");
        Assert.Contains(layout.Facts, f => f.Label == "Cấp bậc" && f.Value == "Middle");
        Assert.Contains(layout.Facts, f => f.Label == "Hình thức làm việc" && f.Value == "On-site");
    }

    [Fact]
    public void Khoa_la_thi_in_nguyen_gia_tri_chu_khong_bo_trong()
    {
        // Dữ liệu cũ hoặc giá trị nhập tay vẫn phải hiện ra — biến mất âm thầm còn tệ hơn hiển thị
        // một chuỗi lạ, vì người soạn sẽ không biết là mình cần sửa.
        var doc = Document();
        doc.EmploymentType = "thoi_vu";

        var layout = JdLayout.Build(Template(), doc);

        Assert.Contains(layout.Facts, f => f.Label == "Thời gian" && f.Value == "thoi_vu");
    }

    [Fact]
    public void Tieu_de_van_ban_de_trong_thi_roi_ve_mac_dinh()
    {
        var template = Template();
        template.DocumentTitle = "   ";

        Assert.Equal("THÔNG TIN TUYỂN DỤNG", JdLayout.Build(template, Document()).DocumentTitle);
    }

    [Fact]
    public void Cau_hinh_mau_hong_thi_roi_ve_mac_dinh_chu_khong_nem_loi()
    {
        // Cấu hình hỏng KHÔNG được làm chết việc dựng JD — người dùng sẽ không hiểu vì sao
        // "tải PDF" lại báo lỗi.
        var template = Template();
        template.SectionsJson = "{ đây không phải JSON";

        var layout = JdLayout.Build(template, Document());

        Assert.NotEmpty(layout.Sections);
    }

    [Theory]
    [InlineData(null, "#4F46E5")]
    [InlineData("", "#4F46E5")]
    [InlineData("khong-phai-mau", "#4F46E5")]
    [InlineData("#12345", "#4F46E5")]      // thiếu một ký tự
    [InlineData("123456", "#123456")]      // thiếu dấu # thì tự thêm
    [InlineData("#abcdef", "#ABCDEF")]
    public void Mau_rac_roi_ve_mac_dinh_thay_vi_nem_loi(string? input, string expected)
    {
        Assert.Equal(expected, JdLayout.NormalizeHex(input));
    }

    // `int?` chứ không phải `decimal?`: InlineData chở số nguyên, mà xUnit không tự đổi
    // int → decimal? nên tham số khai decimal? sẽ ném ArgumentException lúc gọi.
    [Theory]
    [InlineData(null, null, "")]
    [InlineData(20_000_000, null, "20.000.000 VND")]
    [InlineData(null, 30_000_000, "30.000.000 VND")]
    [InlineData(20_000_000, 30_000_000, "20.000.000 – 30.000.000 VND")]
    public void Dai_luong_dinh_dang_theo_du_lieu_co_that(int? min, int? max, string expected)
    {
        Assert.Equal(expected, JdLayout.FormatSalary((decimal?)min, (decimal?)max, "VND"));
    }

    [Fact]
    public void Dai_luong_dung_dau_cham_du_may_chu_khong_co_culture_vi_VN()
    {
        // Container Linux có thể chạy globalization-invariant; khi đó CultureInfo("vi-VN") âm thầm
        // rơi về invariant và lương in ra "20,000,000" — file JD gửi ra ngoài với dấu sai.
        Assert.Equal("20.000.000 VND", JdLayout.FormatSalary(20_000_000m, null, "VND"));
    }

    [Fact]
    public void Han_nop_in_theo_gio_Viet_Nam()
    {
        // Container chạy UTC nên ToLocalTime() là lệnh rỗng và in lệch một ngày — đúng lỗi đã phải
        // vá ở thư mời nhận việc.
        var doc = Document();
        doc.ApplicationDeadline = new DateTimeOffset(2026, 9, 4, 18, 0, 0, TimeSpan.Zero); // 05/09 giờ VN

        var layout = JdLayout.Build(Template(), doc);

        Assert.Contains(layout.Facts, f => f.Label == "Hạn nộp hồ sơ" && f.Value == "05/09/2026");
    }
}
