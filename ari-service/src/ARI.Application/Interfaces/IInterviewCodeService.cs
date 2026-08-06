using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Domain.Entities;

namespace ARI.Application.Interfaces
{
    /// <summary>Interview Code (6 ký tự, one-time-use, TTL 2h — ADR-016): sinh lẻ/batch, validate tại Kiosk.</summary>
    public interface IInterviewCodeService
    {
        Task<Result<InterviewCode>> GenerateCodeAsync(Guid applicationId, int? roundNumber, Guid createdByUserId, CancellationToken ct = default);
        Task<Result<List<InterviewCode>>> GenerateBatchAsync(List<Guid> applicationIds, int? roundNumber, Guid createdByUserId, CancellationToken ct = default);
        Task<Result<KioskSessionResponse>> ValidateCodeAsync(string code, CancellationToken ct = default);
        Task<List<InterviewCodeSummaryDto>> GetCodesByJobAsync(Guid jobPostingId, CancellationToken ct = default);
    }
}
