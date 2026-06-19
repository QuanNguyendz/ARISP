using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Common;
using ARISP.Application.DTOs;

namespace ARISP.Application.Interfaces
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
    }
}
