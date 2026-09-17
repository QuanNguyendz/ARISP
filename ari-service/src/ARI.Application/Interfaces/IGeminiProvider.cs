using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;

namespace ARI.Application.Interfaces
{
    /// <summary>Mọi thứ AI cần để chấm một CV theo bộ tiêu chí của tin (ADR-070).</summary>
    /// <param name="JdText">Các trường có cấu trúc của tin + mô tả công việc (và text trích từ file JD DOCX).</param>
    /// <param name="JdPdf">File JD gốc dạng PDF nếu có — gửi nguyên file để model đọc đúng bố cục.</param>
    /// <param name="RubricInstruction">Bảng tiêu chí + mức neo + tài liệu nội bộ + phần cấm dùng.</param>
    /// <param name="CriterionKeys">Đúng và đủ các mã model phải chấm.</param>
    public record CvScoringAiRequest(
        string JdText,
        AiAttachment? JdPdf,
        AiAttachment? CvPdf,
        string? CvText,
        string RubricInstruction,
        IReadOnlyList<string> CriterionKeys);

    public interface IGeminiProvider
    {
        /// <summary>
        /// Chấm CV theo bộ tiêu chí (ADR-060/070): AI chỉ chấm TỪNG tiêu chí kèm bằng chứng; điểm tổng
        /// do backend cộng có trọng số. Không có đường chấm nào khi thiếu bộ tiêu chí.
        /// </summary>
        Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(CvScoringAiRequest request, CancellationToken ct = default);

        /// <summary>
        /// Gợi ý bản nháp bộ tiêu chí chấm CV từ phiếu yêu cầu / JD (ADR-070). Chỉ là bản nháp: Hiring
        /// Manager sửa rồi mới lưu, và bản lưu vẫn đi qua <c>CvRubricEditing.Normalize</c>.
        /// </summary>
        Task<Result<List<CvRubricSuggestionItem>>> SuggestCvRubricAsync(CvRubricSuggestionInput input, CancellationToken ct = default);

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
