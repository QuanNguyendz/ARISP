using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Domain.Entities;

namespace ARI.Application.Interfaces
{
    /// <summary>
    /// CV-JD Match Analysis (Gemini, ADR-030) — service dùng chung (3 consumer:
    /// CvAnalysis endpoints, portal cv-match background, application auto-analysis).
    /// </summary>
    public interface ICvJdAnalysisService
    {
        Task<Result<CvJdAnalysis>> AnalyzeAndCacheAsync(Guid jobPostingId, Stream cvFileStream, string cvFileName, CancellationToken ct = default);
        Task<Result<CvJdAnalysis>> GetAnalysisByIdAsync(Guid id, CancellationToken ct = default);
        Task<Result<CvJdAnalysis>> GetAnalysisByApplicationIdAsync(Guid applicationId, CancellationToken ct = default);
        Task<bool> CheckCandidateOwnershipAsync(Guid cvAnalysisId, Guid candidateAccountId, CancellationToken ct = default);
        Task ClearAllCacheAsync(CancellationToken ct = default);
    }
}
