using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARISP.Application.Interfaces
{
    /// <summary>
    /// Đóng dấu duyệt (visual stamp) lên file JD PDF sau khi HR Leader phê duyệt.
    /// Không phải chữ ký số mật mã (PKI) — chỉ chèn con dấu hiển thị "ĐÃ DUYỆT" + tên người
    /// duyệt + ngày, để xác nhận trực quan tin/JD đã được HR Leader thông qua.
    /// </summary>
    public interface IJdStampService
    {
        /// <summary>
        /// Nhận bytes của file PDF gốc, vẽ con dấu duyệt và trả về bytes PDF mới.
        /// Ném <see cref="NotSupportedException"/> hoặc lỗi nếu file không phải PDF hợp lệ —
        /// gọi nơi dùng nên bọc try/catch để việc duyệt không bị chặn khi đóng dấu thất bại.
        /// </summary>
        Task<byte[]> StampApprovalAsync(byte[] pdfBytes, string approverName, DateTimeOffset approvedAt, CancellationToken ct = default);

        /// <summary>
        /// Tạo PDF mới từ nội dung text (dùng cho JD gốc là DOCX — không có sẵn PDF để đóng dấu lên):
        /// render tiêu đề + nội dung JD (tự xuống dòng, phân trang) và đóng con dấu duyệt ở trang đầu.
        /// Mất định dạng gốc của DOCX nhưng giữ đầy đủ nội dung + bằng chứng phê duyệt.
        /// </summary>
        Task<byte[]> StampApprovalFromTextAsync(string title, string bodyText, string approverName, DateTimeOffset approvedAt, CancellationToken ct = default);
    }
}
