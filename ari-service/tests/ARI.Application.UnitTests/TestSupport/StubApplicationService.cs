using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;

namespace ARI.Application.UnitTests.TestSupport;

/// <summary>
/// IApplicationService giả có cấu hình cho test các wrapper CQRS mỏng (UpdateApplicationStatus, GetApplicationById):
/// trả kết quả nạp sẵn hoặc ném lỗi, tôn trọng CancellationToken, ghi lại tham số đã nhận. Các phương thức
/// còn lại chưa dùng nên ném NotImplemented.
/// </summary>
public sealed class StubApplicationService : IApplicationService
{
    // --- UpdateApplicationStatus ---
    public Result<ApplicationResponse> UpdateResult { get; set; } = Result.Success(new ApplicationResponse());
    public Exception? UpdateThrows { get; set; }
    public Guid? LastUpdateId { get; private set; }
    public string? LastUpdateStatus { get; private set; }

    // --- GetApplicationById ---
    public Result<ApplicationResponse> GetByIdResult { get; set; } = Result.Success(new ApplicationResponse());
    public Exception? GetByIdThrows { get; set; }

    public Task<Result<ApplicationResponse>> UpdateApplicationStatusAsync(Guid id, string newStatus, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        LastUpdateId = id;
        LastUpdateStatus = newStatus;
        if (UpdateThrows != null) throw UpdateThrows;
        return Task.FromResult(UpdateResult);
    }

    public Task<Result<ApplicationResponse>> GetApplicationByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (GetByIdThrows != null) throw GetByIdThrows;
        return Task.FromResult(GetByIdResult);
    }

    public Task<Result<ApplicationResponse>> SubmitApplicationAsync(SubmitApplicationRequest request, string source = "invited", CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<List<ApplicationResponse>>> GetAllApplicationsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<List<ApplicationResponse>>> GetApplicationsByJobAsync(Guid jobPostingId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<List<ApplicationResponse>>> GetApplicationsForCreatorAsync(Guid creatorUserId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> OpenRoundForSchedulingAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> AcceptApplicationAsync(Guid applicationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> RejectApplicationAsync(Guid applicationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default) => throw new NotImplementedException();
}
