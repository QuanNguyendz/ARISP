using System.Threading;
using System.Threading.Tasks;
using ARI.Domain.Entities;

namespace ARI.Application.Interfaces
{
    /// <summary>
    /// Dựng file bản mô tả công việc từ mẫu công ty + nội dung đã soạn (ADR-064).
    ///
    /// Nằm ở Infrastructure vì PdfSharpCore chỉ được tham chiếu ở đó — cùng lý do với
    /// <see cref="IJdStampService"/>.
    ///
    /// Hai bản xuất dùng CHUNG một mô hình bố cục (xem <c>JdDocumentRenderer</c>): đầu trang có
    /// logo + thông tin công ty, tiêu đề vị trí, bảng thông tin nhanh, rồi từng mục theo đúng thứ tự
    /// mẫu quy định. Tách hai bộ dựng riêng biệt sẽ trôi khỏi nhau ngay lần sửa thứ hai.
    /// </summary>
    public interface IJdDocumentRenderer
    {
        /// <summary>
        /// Xuất .docx. <paramref name="logo"/> null hoặc hỏng thì bỏ qua phần ảnh và VẪN xuất file —
        /// một file logo lỗi không được phép chặn việc dựng JD.
        /// </summary>
        Task<byte[]> RenderDocxAsync(JdTemplate template, JdDocument document, byte[]? logo, CancellationToken ct = default);

        /// <summary>Xuất .pdf — cùng bố cục với bản .docx.</summary>
        Task<byte[]> RenderPdfAsync(JdTemplate template, JdDocument document, byte[]? logo, CancellationToken ct = default);
    }
}
