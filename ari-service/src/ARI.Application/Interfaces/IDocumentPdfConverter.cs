using System.Threading;
using System.Threading.Tasks;

namespace ARI.Application.Interfaces
{
    /// <summary>
    /// Chuyển tài liệu Word sang PDF để DỰNG BẢN XEM TRƯỚC.
    ///
    /// <b>Vì sao cần.</b> Bản xem trước phía trình duyệt (<c>docx-preview</c>) đọc được nội dung nhưng
    /// không bố trí được khung nổi, hộp văn bản và chia cột — đúng những thứ mẫu CV hiện đại dùng để
    /// làm dải màu bên lề. Kết quả là khối nền đè lên chữ. Tài liệu do hệ thống tự sinh (file JD) thì
    /// hiện đẹp, vì nó chỉ dùng đoạn văn, bảng và ảnh chảy theo dòng — nên vấn đề không nằm ở trình
    /// xem mà ở loại tài liệu. PDF thì trình duyệt tự dựng, trung thực tuyệt đối.
    ///
    /// <b>Luôn là tuỳ chọn, không bao giờ bắt buộc.</b> Bộ chuyển đổi dựa vào LibreOffice — một tiến
    /// trình ngoài có thể không được cài (máy lập trình viên) hoặc gọi hỏng. Mọi thất bại trả
    /// <c>null</c> để nơi gọi lùi về bản xem trước cũ: xem hơi lệch vẫn hơn không xem được gì.
    /// </summary>
    public interface IDocumentPdfConverter
    {
        /// <summary>Có dùng được ở môi trường đang chạy không (đã tìm thấy LibreOffice).</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Trả về PDF, hoặc <c>null</c> nếu không chuyển được vì bất kỳ lý do gì.
        /// </summary>
        /// <param name="fileName">Tên file gốc — chỉ dùng để lấy phần mở rộng cho tiến trình ngoài.</param>
        Task<byte[]?> ToPdfAsync(byte[] content, string fileName, CancellationToken ct = default);
    }
}
