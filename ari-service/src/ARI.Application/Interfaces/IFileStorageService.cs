using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    /// <summary>
    /// Trừu tượng hoá lưu trữ file (CV, tài liệu...). Cho phép thay đổi backend lưu trữ
    /// (đĩa local cho dev, object storage S3-compatible như Cloudflare R2 cho prod) mà không
    /// đụng tới business logic. Theo cùng pattern với IAIProvider / IGeminiProvider.
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Lưu nội dung file và trả về <c>storageKey</c> để lưu vào DB.
        /// Không trả về URL trực tiếp — gọi <see cref="GetUrlAsync"/> khi cần hiển thị.
        /// </summary>
        /// <param name="content">Nội dung file (bytes).</param>
        /// <param name="originalFileName">Tên file gốc — chỉ dùng để lấy phần mở rộng.</param>
        /// <param name="contentType">MIME type (vd application/pdf).</param>
        /// <param name="folder">Nhóm thư mục đích — bắt buộc, để mỗi loại file nằm một nhánh riêng.</param>
        Task<string> SaveAsync(byte[] content, string originalFileName, string contentType, StorageFolder folder, CancellationToken ct = default);

        /// <summary>
        /// Chuyển <c>storageKey</c> thành URL client dùng được.
        /// Local: trả về đường dẫn tương đối (vd "/uploads/x.pdf").
        /// S3/R2: trả về presigned URL tuyệt đối có thời hạn.
        /// </summary>
        Task<string> GetUrlAsync(string storageKey, CancellationToken ct = default);

        /// <summary>
        /// URL tải về (ép trình duyệt download thay vì xem inline), kèm tên file gợi ý.
        /// S3/R2: presigned URL có Content-Disposition=attachment. Local: đường dẫn tương đối.
        /// </summary>
        Task<string> GetDownloadUrlAsync(string storageKey, string downloadFileName, CancellationToken ct = default);

        /// <summary>
        /// Ghi vào ĐÚNG <c>storageKey</c> chỉ định, đè lên nếu đã có.
        ///
        /// Khác <see cref="SaveAsync"/> ở chỗ khoá do nơi gọi quyết định chứ không sinh ngẫu nhiên —
        /// dành cho các bản dựng PHÁI SINH từ một file gốc (bản PDF của một CV .docx). Khoá suy được
        /// từ khoá gốc nên lần sau chỉ cần thử đọc là biết đã dựng hay chưa, không phải thêm cột vào
        /// cơ sở dữ liệu chỉ để nhớ một cái tên.
        /// </summary>
        Task SaveAtAsync(string storageKey, byte[] content, string contentType, CancellationToken ct = default);

        /// <summary>Xoá file theo <c>storageKey</c> (best-effort, không ném lỗi nếu không tồn tại).</summary>
        Task DeleteAsync(string storageKey, CancellationToken ct = default);

        /// <summary>
        /// Đọc toàn bộ nội dung file (bytes) theo <c>storageKey</c> để xử lý phía server
        /// (vd phân tích CV-JD). Trả về <c>null</c> nếu không tìm thấy.
        /// </summary>
        Task<byte[]?> ReadAllBytesAsync(string storageKey, CancellationToken ct = default);
    }
}
