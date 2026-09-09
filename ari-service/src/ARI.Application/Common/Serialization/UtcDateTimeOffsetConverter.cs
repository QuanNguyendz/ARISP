using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARI.Application.Common.Serialization
{
    /// <summary>
    /// Chuẩn hoá mọi <see cref="DateTimeOffset"/> nhận từ client về UTC ngay tại ranh giới JSON.
    ///
    /// <b>Vì sao cần.</b> Npgsql chỉ ghi được offset 0 vào cột <c>timestamp with time zone</c>; giá
    /// trị lệch múi giờ làm <c>SaveChanges</c> ném <see cref="ArgumentException"/> và cả request
    /// thành 500. Mà ô <c>&lt;input type="date"&gt;</c> gửi lên chuỗi trần <c>"2026-09-19"</c> — không
    /// kèm múi giờ — nên ASP.NET hiểu theo giờ <b>local của máy chủ</b>: máy ở UTC+7 thì ra
    /// <c>+07:00</c> và vỡ ngay khi lưu.
    ///
    /// <b>Vì sao đặt ở đây chứ không sửa từng biểu mẫu.</b> Quy ước "frontend tự gọi
    /// <c>toISOString()</c> trước khi gửi" đã tồn tại ở hai màn (hạn nộp hồ sơ, thư mời) nhưng chỉ
    /// được giữ bằng trí nhớ — ô ngày mới nhất trên phiếu yêu cầu tuyển dụng quên một lần là sập
    /// production. Chốt ở ranh giới thì không biểu mẫu nào quên được nữa.
    ///
    /// <b>Không đổi ý nghĩa dữ liệu.</b> <c>ToUniversalTime()</c> giữ nguyên MỐC thời gian, chỉ đổi
    /// cách biểu diễn offset — và Postgres <c>timestamptz</c> vốn lưu mốc chứ không lưu offset.
    /// Chỗ hiển thị đã tự đổi sang <c>+07:00</c> khi in ra (xem <c>JdLayout</c>).
    /// </summary>
    public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetDateTimeOffset().ToUniversalTime();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToUniversalTime());
    }

    /// <summary>
    /// Bản cho <c>DateTimeOffset?</c>. System.Text.Json KHÔNG tự suy ra bộ chuyển cho kiểu nullable
    /// từ bộ chuyển của kiểu gốc, nên thiếu lớp này thì đúng những ô ngày *tuỳ chọn* — vốn là phần
    /// lớn các ô ngày trong dự án — vẫn lọt qua với offset lệch.
    /// </summary>
    public sealed class NullableUtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTimeOffset().ToUniversalTime();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        {
            if (value is { } v) writer.WriteStringValue(v.ToUniversalTime());
            else writer.WriteNullValue();
        }
    }
}
