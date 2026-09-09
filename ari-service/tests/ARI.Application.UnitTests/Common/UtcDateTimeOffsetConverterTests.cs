using System;
using System.Text.Json;
using ARI.Application.Common.Serialization;
using Xunit;

namespace ARI.Application.UnitTests.Common;

/// <summary>
/// Chuẩn hoá mốc thời gian nhận từ client về UTC ngay tại ranh giới JSON.
///
/// Vì sao có bộ test này: Npgsql chỉ ghi được offset 0 vào cột <c>timestamp with time zone</c>. Một
/// ô <c>&lt;input type="date"&gt;</c> gửi lên chuỗi trần <c>"2026-09-19"</c> — không kèm múi giờ — nên
/// ASP.NET hiểu theo giờ local của máy chủ; máy ở UTC+7 thì <c>SaveChanges</c> ném lỗi và cả request
/// thành 500. Quy ước "frontend nhớ gọi toISOString()" đã có ở hai màn khác nhưng chỉ giữ bằng trí
/// nhớ, và ô ngày mới nhất quên một lần là sập.
/// </summary>
public class UtcDateTimeOffsetConverterTests
{
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeOffsetConverter());
        options.Converters.Add(new NullableUtcDateTimeOffsetConverter());
        return options;
    }

    private sealed record Payload(DateTimeOffset Required, DateTimeOffset? Optional);

    [Theory]
    [InlineData("\"2026-09-19T00:00:00+07:00\"")]
    [InlineData("\"2026-09-18T19:00:00-05:00\"")]
    [InlineData("\"2026-09-19T00:00:00Z\"")]
    public void Moi_offset_deu_ve_UTC(string json)
    {
        var value = JsonSerializer.Deserialize<DateTimeOffset>(json, Options());

        // Điều kiện DUY NHẤT mà Npgsql đòi hỏi ở cột `timestamptz`.
        Assert.Equal(TimeSpan.Zero, value.Offset);
    }

    [Fact]
    public void Chuyen_ve_UTC_KHONG_lam_lech_moc_thoi_gian()
    {
        // `ToUniversalTime()` chỉ đổi cách biểu diễn offset, không dời thời điểm — nên dữ liệu cũ và
        // mới vẫn so sánh được với nhau. Nếu chỗ này sai thì mọi hạn nộp hồ sơ lệch đi vài tiếng.
        var expected = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.FromHours(7));

        var value = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-09-19T00:00:00+07:00\"", Options());

        Assert.Equal(expected.UtcDateTime, value.UtcDateTime);
        Assert.Equal(new DateTimeOffset(2026, 9, 18, 17, 0, 0, TimeSpan.Zero), value);
    }

    [Fact]
    public void O_ngay_TUY_CHON_cung_duoc_chuan_hoa()
    {
        // System.Text.Json KHÔNG suy ra bộ chuyển cho `DateTimeOffset?` từ bộ chuyển của kiểu gốc.
        // Thiếu bản nullable thì đúng những ô ngày tuỳ chọn — phần lớn ô ngày trong dự án, gồm cả
        // `ExpectedStartDate` đã gây ra lỗi 500 — vẫn lọt qua với offset lệch.
        var payload = JsonSerializer.Deserialize<Payload>(
            "{\"Required\":\"2026-09-19T00:00:00+07:00\",\"Optional\":\"2026-09-19T00:00:00+07:00\"}",
            Options());

        Assert.Equal(TimeSpan.Zero, payload!.Required.Offset);
        Assert.Equal(TimeSpan.Zero, payload.Optional!.Value.Offset);
    }

    [Fact]
    public void O_ngay_bo_trong_van_la_null()
    {
        var payload = JsonSerializer.Deserialize<Payload>(
            "{\"Required\":\"2026-09-19T00:00:00Z\",\"Optional\":null}", Options());

        Assert.Null(payload!.Optional);
    }

    [Fact]
    public void Ghi_ra_JSON_cung_luon_la_UTC()
    {
        // Giữ đối xứng: client đọc lại đúng thứ server lưu, không phải một offset thứ ba tuỳ máy chủ.
        var json = JsonSerializer.Serialize(
            new Payload(new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.FromHours(7)), null), Options());

        Assert.Contains("2026-09-18T17:00:00", json);
        Assert.DoesNotContain("+07:00", json);
    }
}
