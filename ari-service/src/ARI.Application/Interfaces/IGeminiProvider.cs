using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;

namespace ARI.Application.Interfaces
{
    public interface IGeminiProvider
    {
        Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(
            string jdText,
            byte[]? cvFileBytes,
            string? cvMimeType,
            string? fallbackCvText,
            CancellationToken ct = default);

        /// <summary>
        /// Đánh giá CV độc lập (không gắn JD): chấm điểm tổng thể, điểm mạnh, gợi ý cải thiện.
        /// </summary>
        Task<Result<CvReviewResultDto>> ReviewCvAsync(
            byte[]? cvFileBytes,
            string? cvMimeType,
            string? fallbackCvText,
            CancellationToken ct = default);

        /// <summary>
        /// Trích xuất thông tin có cấu trúc của Job Posting từ file JD (PDF/DOCX) để auto-fill
        /// form tạo tin tuyển dụng. Người dùng vẫn chỉnh sửa lại được sau khi điền.
        /// </summary>
        Task<Result<JdExtractionResultDto>> ExtractJobFromJdAsync(
            byte[]? jdFileBytes,
            string? jdMimeType,
            string? fallbackJdText,
            CancellationToken ct = default);

        /// <summary>
        /// So sánh thông tin liên hệ và nội dung trong CV bằng AI.
        /// </summary>
        Task<Result<CvContactVerificationResultDto>> VerifyCvContactInfoAsync(
            byte[]? cvFileBytes,
            string? cvMimeType,
            string? fallbackCvText,
            string formName,
            string formPhone,
            string formEmail,
            CancellationToken ct = default);
    }
}
