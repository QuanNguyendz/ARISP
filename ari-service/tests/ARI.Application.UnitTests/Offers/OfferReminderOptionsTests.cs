using ARI.Application.Options;
using Xunit;

namespace ARI.Application.UnitTests.Offers;

/// <summary>
/// Mốc nhắc phản hồi thư mời nhận việc (ADR-061/062).
///
/// Vì sao có bộ test này: <c>OfferEmail.BuildReminder</c> từng được viết ra rồi **không nơi nào gọi**
/// — ứng viên quá hạn bị âm thầm đánh dấu từ chối mà chưa từng được cảnh báo, trong khi luồng phỏng
/// vấn ngay bên cạnh vẫn nhắc ở mốc 24h/3h. Một tính năng chết lặng lẽ như vậy rất dễ chết lại, nên
/// khoá phần cấu hình lại bằng test.
/// </summary>
public class OfferReminderOptionsTests
{
    [Fact]
    public void Mac_dinh_co_nhac_truoc_khi_het_han()
    {
        var marks = new SchedulingOptions().OfferReminderMarks();

        // Rỗng nghĩa là tắt nhắc — đó chính là hành vi cũ mà ADR-062 đi chữa.
        Assert.NotEmpty(marks);
    }

    [Fact]
    public void Moc_giam_dan_khong_trung_va_bo_gia_tri_rac()
    {
        var opts = new SchedulingOptions { OfferReminderHoursBeforeExpiry = "12, 48, 48, 0, -3, abc" };

        var marks = opts.OfferReminderMarks();

        // Giảm dần để nhánh quét lấy được mốc LỚN NHẤT đã chạm; loại 0, số âm, chữ và giá trị trùng.
        Assert.Equal(new[] { 48, 12 }, marks);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("khong-phai-so")]
    public void Cau_hinh_rong_hoac_rac_thi_tat_nhac_chu_khong_no(string value)
    {
        var opts = new SchedulingOptions { OfferReminderHoursBeforeExpiry = value };

        // Tắt nhắc là lựa chọn hợp lệ; ném lỗi ở đây sẽ làm chết cả tác vụ nền, kéo theo cả nhánh
        // nhắc lịch phỏng vấn và nhánh đóng thư mời quá hạn chạy chung vòng lặp.
        Assert.Empty(opts.OfferReminderMarks());
    }
}
