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
        /// <summary>Hồ sơ thuộc một tập tin CHO TRƯỚC — phạm vi do <c>JobAccess.ScopedJobIdsAsync</c> tính ở server.</summary>
        Task<Result<List<ApplicationResponse>>> GetApplicationsForJobsAsync(IReadOnlyCollection<Guid> jobPostingIds, CancellationToken ct = default);
        Task<Result<ApplicationResponse>> GetApplicationByIdAsync(Guid id, CancellationToken ct = default);
        Task<Result<ApplicationResponse>> UpdateApplicationStatusAsync(Guid id, string newStatus, CancellationToken ct = default);
        /// <summary>Mở một vòng cho hồ sơ (đánh dấu vòng đang hoạt động + nâng khỏi giai đoạn duyệt CV). Không gửi email.</summary>
        Task<Result<bool>> OpenRoundForSchedulingAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default);
        Task<Result<bool>> AcceptApplicationAsync(Guid applicationId, CancellationToken ct = default);
        /// <summary>
        /// Loại hồ sơ + gửi thư cảm ơn. <paramref name="emailOverride"/>: nội dung do nhân sự sửa
        /// tay ở trình soạn thảo (ADR-061); bỏ trống → dùng mẫu.
        /// </summary>
        Task<Result<bool>> RejectApplicationAsync(
            Guid applicationId, CancellationToken ct = default,
            Emails.EmailOverride? emailOverride = null, Guid? actorUserId = null);
        Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default);
    }
}
