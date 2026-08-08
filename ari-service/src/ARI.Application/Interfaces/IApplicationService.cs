using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;

namespace ARI.Application.Interfaces
{
    /// <summary>
    /// Nghiệp vụ hồ sơ ứng tuyển — service dùng chung (consumers: Applications endpoints,
    /// CandidatePortal apply, Jobs GetJobApplications). Handlers CQRS delegate qua interface này.
    /// </summary>
    public interface IApplicationService
    {
        Task<Result<ApplicationResponse>> SubmitApplicationAsync(SubmitApplicationRequest request, string source = "invited", CancellationToken ct = default);
        Task<Result<List<ApplicationResponse>>> GetAllApplicationsAsync(CancellationToken ct = default);
        Task<Result<List<ApplicationResponse>>> GetApplicationsByJobAsync(Guid jobPostingId, CancellationToken ct = default);
        Task<Result<List<ApplicationResponse>>> GetApplicationsForCreatorAsync(Guid creatorUserId, CancellationToken ct = default);
        Task<Result<ApplicationResponse>> GetApplicationByIdAsync(Guid id, CancellationToken ct = default);
        Task<Result<ApplicationResponse>> UpdateApplicationStatusAsync(Guid id, string newStatus, CancellationToken ct = default);
        Task<Result<bool>> SendInterviewInviteAsync(Guid applicationId, string frontendBaseUrl, int roundNumber = 1, CancellationToken ct = default, bool sendEmail = true);
        Task<Result<bool>> AcceptApplicationAsync(Guid applicationId, string frontendBaseUrl, CancellationToken ct = default);
        Task<Result<bool>> RejectApplicationAsync(Guid applicationId, CancellationToken ct = default);
        Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default);
    }
}
