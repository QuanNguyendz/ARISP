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
    }
}
