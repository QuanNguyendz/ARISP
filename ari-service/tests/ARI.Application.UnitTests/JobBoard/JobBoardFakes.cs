using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>
/// IApplicationService giả cho test wrapper CQRS nộp hồ sơ (UC-28): ghi lại request + source đã nhận
/// (để kiểm tra hash/parse/lưu-file được ráp đúng) và trả kết quả nạp sẵn. Các phương thức khác không dùng.
/// </summary>
internal sealed class FakeApplicationService : IApplicationService
{
    public SubmitApplicationRequest? LastRequest { get; private set; }
    public string? LastSource { get; private set; }
    public Result<ApplicationResponse> SubmitResult { get; set; } =
        Result.Success(new ApplicationResponse { Id = Guid.NewGuid(), Status = "cv_submitted" });

    public Task<Result<ApplicationResponse>> SubmitApplicationAsync(SubmitApplicationRequest request, string source = "invited", CancellationToken ct = default)
    {
        LastRequest = request;
        LastSource = source;
        return Task.FromResult(SubmitResult);
    }

    public Task<Result<List<ApplicationResponse>>> GetAllApplicationsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<List<ApplicationResponse>>> GetApplicationsByJobAsync(Guid jobPostingId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<List<ApplicationResponse>>> GetApplicationsForCreatorAsync(Guid creatorUserId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<ApplicationResponse>> GetApplicationByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<ApplicationResponse>> UpdateApplicationStatusAsync(Guid id, string newStatus, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> OpenRoundForSchedulingAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> AcceptApplicationAsync(Guid applicationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> RejectApplicationAsync(Guid applicationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default) => throw new NotImplementedException();
}

/// <summary>Scope factory stub — chỉ cần để dựng handler có tác vụ nền; không được gọi trong các path test.</summary>
internal sealed class ThrowingScopeFactory : IServiceScopeFactory
{
    public IServiceScope CreateScope() => throw new NotImplementedException();
}
