using System;
using System.Collections.Generic;
using ARI.Application.Scheduling;
using Xunit;

namespace ARI.Application.UnitTests.Scheduling;

/// <summary>
/// Luật kiểm khung giờ rảnh của Hiring Manager (<see cref="HmAvailabilitySupport.Sanitize"/>, ADR-067).
///
/// Khoá bằng test vì đây là bộ luật mà GIAO DIỆN cũng phải nói y hệt: client kiểm trước để báo ngay
/// tại dòng sai, server kiểm lại vì đó mới là chốt chặn thật. Hai bên lệch nhau thì người dùng bấm
/// nút rồi nhận 400 mà không hiểu vì sao — đúng thứ vừa xảy ra khi luật "tối đa 12 tiếng" chỉ có ở
/// một phía.
/// </summary>
public class HmAvailabilityValidationTests
{
    /// <summary>
    /// Chốt <c>now</c> MỘT lần: gọi <c>UtcNow</c> hai lần thì mốc thứ hai trễ vài tick, và khung
    /// "đúng 12 tiếng" hoá ra dài hơn trần một chút — test đỏ vì cách dựng dữ liệu, không phải vì luật.
    /// </summary>
    private static HmAvailabilityWindowInput Window(TimeSpan fromNow, TimeSpan length)
    {
        var start = DateTimeOffset.UtcNow.Add(fromNow);
        return new HmAvailabilityWindowInput(start, start.Add(length), null);
    }

    private static (List<HmAvailabilityWindowInput> windows, string? error) Run(
        params HmAvailabilityWindowInput[] input) => HmAvailabilitySupport.Sanitize(input);

    [Fact]
    public void Khung_gio_hop_le_thi_di_qua()
    {
        var (windows, error) = Run(Window(TimeSpan.FromDays(1), TimeSpan.FromHours(4)));

        Assert.Null(error);
        Assert.Single(windows);
    }

    [Fact]
    public void Gio_bat_dau_o_qua_khu_bi_chan()
    {
        // Yêu cầu của người dùng: khung giờ bắt đầu phải LỚN HƠN thời điểm hiện tại. Trước đây chỉ
        // kiểm giờ kết thúc, nên một khung đã bắt đầu vẫn khai mới được — và ca xếp vào đó có thể
        // rơi vào khoảng đã trôi qua.
        var (_, error) = Run(Window(TimeSpan.FromHours(-1), TimeSpan.FromHours(4)));

        Assert.NotNull(error);
        Assert.Contains("tương lai", error);
    }

    [Fact]
    public void Gio_ket_thuc_khong_sau_gio_bat_dau_bi_chan()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var (_, error) = Run(new HmAvailabilityWindowInput(start, start, null));

        Assert.NotNull(error);
        Assert.Contains("sau giờ bắt đầu", error);
    }

    [Fact]
    public void Khung_dai_hon_tran_bi_chan()
    {
        // "Rảnh từ 10/09 đến 25/09" làm ràng buộc khớp lịch mất hết ý nghĩa — Recruiter xếp được cả
        // ca 3 giờ sáng.
        var (_, error) = Run(Window(TimeSpan.FromDays(1), TimeSpan.FromDays(15)));

        Assert.NotNull(error);
        Assert.Contains($"{HmAvailabilitySupport.MaxWindowHours} tiếng", error);
    }

    [Fact]
    public void Khung_dung_bang_tran_van_hop_le()
    {
        var (windows, error) = Run(
            Window(TimeSpan.FromDays(1), TimeSpan.FromHours(HmAvailabilitySupport.MaxWindowHours)));

        Assert.Null(error);
        Assert.Single(windows);
    }

    [Fact]
    public void Khung_trung_khit_bi_bo_bot()
    {
        // Bấm gửi hai lần không nên sinh ra hai dòng y hệt.
        var w = Window(TimeSpan.FromDays(2), TimeSpan.FromHours(3));
        var (windows, error) = Run(w, w);

        Assert.Null(error);
        Assert.Single(windows);
    }

    [Fact]
    public void Qua_nhieu_khung_bi_chan()
    {
        var many = new List<HmAvailabilityWindowInput>();
        for (var i = 1; i <= HmAvailabilitySupport.MaxWindowsPerRound + 1; i++)
            many.Add(Window(TimeSpan.FromDays(i), TimeSpan.FromHours(2)));

        var (_, error) = HmAvailabilitySupport.Sanitize(many);

        Assert.NotNull(error);
        Assert.Contains("Tối đa", error);
    }

    [Fact]
    public void Danh_sach_rong_khong_phai_loi()
    {
        // Duyệt hồ sơ thứ hai trong cùng đợt không gửi kèm khung nào — chỗ quyết định "đủ lịch chưa"
        // nằm ở handler (đếm cả khung đã khai trước), không phải ở đây.
        var (windows, error) = HmAvailabilitySupport.Sanitize(null);

        Assert.Null(error);
        Assert.Empty(windows);
    }
}
