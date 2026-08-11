using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Commands;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Gửi lời mời phỏng vấn (<see cref="SendInterviewInviteCommandHandler"/>, test-plan B20): handler mỏng
/// delegate sang <see cref="IApplicationService.SendInterviewInviteAsync"/> — chốt việc ráp base URL từ
/// config (ưu tiên <c>Frontend:CandidateBaseUrl</c> → <c>Authentication:AdminFrontendUrl</c> →
/// fallback <c>http://localhost:3000</c>), truyền đúng appId + round, và propagate kết quả nguyên vẹn.
/// </summary>
public class SendInterviewInviteCommandHandlerTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (k, v) in pairs) dict[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task Passes_configured_base_url_app_id_and_round_then_returns_service_result()
    {
        var appId = Guid.NewGuid();
        var svc = new RecordingApplicationService { Result = Result.Success(true) };
        var handler = new SendInterviewInviteCommandHandler(svc, Config(("Frontend:CandidateBaseUrl", "https://cand.example.io")));

        var res = await handler.Handle(new SendInterviewInviteCommand(appId, 2), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value);
        Assert.Equal(appId, svc.LastAppId);
        Assert.Equal("https://cand.example.io", svc.LastBaseUrl);
        Assert.Equal(2, svc.LastRound);
    }

    [Fact]
    public async Task Falls_back_to_admin_frontend_url_when_candidate_url_missing()
    {
        var svc = new RecordingApplicationService { Result = Result.Success(true) };
        var handler = new SendInterviewInviteCommandHandler(svc, Config(("Authentication:AdminFrontendUrl", "https://staff.example.io")));

        await handler.Handle(new SendInterviewInviteCommand(Guid.NewGuid(), 1), CancellationToken.None);

        Assert.Equal("https://staff.example.io", svc.LastBaseUrl);
    }

    [Fact]
    public async Task Falls_back_to_localhost_and_propagates_failure_unchanged()
    {
        var svc = new RecordingApplicationService { Result = Result.Failure<bool>("Không tìm thấy hồ sơ ứng tuyển.") };
        var handler = new SendInterviewInviteCommandHandler(svc, Config()); // không config base URL nào

        var res = await handler.Handle(new SendInterviewInviteCommand(Guid.NewGuid(), 1), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy hồ sơ ứng tuyển.", res.Error);        // propagate nguyên message
        Assert.Equal("http://localhost:3000", svc.LastBaseUrl);           // fallback mặc định
    }

    /// <summary>IApplicationService giả — ghi lại tham số của <c>SendInterviewInviteAsync</c>; phương thức khác không dùng.</summary>
    private sealed class RecordingApplicationService : IApplicationService
    {
        public Guid LastAppId { get; private set; }
        public string? LastBaseUrl { get; private set; }
        public int LastRound { get; private set; }
        public Result<bool> Result { get; set; } = ARI.Application.Common.Result.Success(true);

        public Task<Result<bool>> SendInterviewInviteAsync(Guid applicationId, string frontendBaseUrl, int roundNumber = 1, CancellationToken ct = default, bool sendEmail = true)
        {
            LastAppId = applicationId;
            LastBaseUrl = frontendBaseUrl;
            LastRound = roundNumber;
            return Task.FromResult(Result);
        }

        public Task<Result<ApplicationResponse>> SubmitApplicationAsync(SubmitApplicationRequest request, string source = "invited", CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<ApplicationResponse>>> GetAllApplicationsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<ApplicationResponse>>> GetApplicationsByJobAsync(Guid jobPostingId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<List<ApplicationResponse>>> GetApplicationsForCreatorAsync(Guid creatorUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ApplicationResponse>> GetApplicationByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<ApplicationResponse>> UpdateApplicationStatusAsync(Guid id, string newStatus, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<bool>> AcceptApplicationAsync(Guid applicationId, string frontendBaseUrl, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<bool>> RejectApplicationAsync(Guid applicationId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<bool>> CheckPracticeEligibilityAsync(Guid applicationId, int roundNumber = 1, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
