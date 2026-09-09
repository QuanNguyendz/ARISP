using System.Text.Encodings.Web;
using System.Text.Json;

namespace ARI.Application.Common
{
    /// <summary>
    /// Serialize phần <c>metadata</c> của <see cref="ARI.Domain.Entities.AuditLog"/>.
    ///
    /// Dùng <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> vì nhật ký kiểm toán do
    /// NGƯỜI đọc ở màn Super Admin: mặc định của System.Text.Json sẽ biến tên và lý do tiếng Việt
    /// thành chuỗi <c>â...</c> không đọc nổi. An toàn vì chuỗi này chỉ nằm trong DB và hiển
    /// thị dưới dạng văn bản, không nhúng vào HTML.
    ///
    /// (Trước đây mỗi lệnh cần ghi audit lại tự khai một <c>JsonSerializerOptions</c> riêng.)
    /// </summary>
    public static class AuditMetadata
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Serialize(object payload) => JsonSerializer.Serialize(payload, Options);
    }
}
