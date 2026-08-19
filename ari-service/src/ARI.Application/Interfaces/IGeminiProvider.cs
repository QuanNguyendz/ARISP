using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;

namespace ARI.Application.Interfaces
{
    public interface IGeminiProvider
    {
        /// <param name="rubricInstruction">
        /// Bộ tiêu chí chấm điểm của doanh nghiệp + ngữ cảnh playbook liên quan (ADR-060).
        /// Có giá trị thì AI phải chấm TỪNG tiêu chí; điểm tổng do backend cộng có trọng số.
        /// </param>
        Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(
            string jdText,
            byte[]? cvFileBytes,
            string? cvMimeType,
            string? fallbackCvText,
            string? rubricInstruction = null,
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
