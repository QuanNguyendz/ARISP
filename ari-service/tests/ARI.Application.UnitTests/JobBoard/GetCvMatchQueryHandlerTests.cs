using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>
/// AI CV Suggestions (UC-27, <see cref="GetCvMatchQueryHandler"/>) — các nhánh xác định (không chạy nền):
/// tài khoản không tồn tại, chưa có CV, file không đọc được, và tái dùng kết quả cache completed/failed.
/// </summary>
public class GetCvMatchQueryHandlerTests
{
    private static readonly byte[] CvBytes = Encoding.UTF8.GetBytes("CV-CONTENT");

    private static string Md5Hex(byte[] bytes) => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

    private static Task<Result<CvMatchResponse>> Run(InMemoryUnitOfWork uow, RecordingFileStorage storage, Guid jobId, Guid candidateId)
        => new GetCvMatchQueryHandler(uow, storage, new ThrowingScopeFactory())
            .Handle(new GetCvMatchQuery(jobId, candidateId), CancellationToken.None);

    [Fact]
    public async Task Unknown_candidate_is_unauthorized()
    {
        var res = await Run(new InMemoryUnitOfWork(), new RecordingFileStorage(), Guid.NewGuid(), Guid.NewGuid());

        Assert.True(res.IsFailure);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Fact]
    public async Task No_cv_in_profile_returns_none()
    {
        var candidateId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobBoardData.Candidate(candidateId, cvUrl: null));

        var res = await Run(uow, new RecordingFileStorage(), Guid.NewGuid(), candidateId);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.HasCv);
        Assert.Equal("none", res.Value.Status);
    }

    [Fact]
    public async Task Unreadable_cv_returns_failed()
    {
        var candidateId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork().Seed(JobBoardData.Candidate(candidateId, cvUrl: "cv/profile.pdf"));
        var storage = new RecordingFileStorage { FileBytes = null }; // đọc file trả null

        var res = await Run(uow, storage, Guid.NewGuid(), candidateId);

        Assert.True(res.IsSuccess);
        Assert.Equal("failed", res.Value.Status);
        Assert.False(res.Value.AiAvailable);
    }

    [Fact]
    public async Task Cached_completed_analysis_is_reused()
    {
        var candidateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(JobBoardData.Candidate(candidateId, cvUrl: "cv/profile.pdf"))
            .Seed(JobBoardData.Analysis(jobId, Md5Hex(CvBytes), status: "completed", score: 88));
        var storage = new RecordingFileStorage { FileBytes = CvBytes };

        var res = await Run(uow, storage, jobId, candidateId);

        Assert.Equal("completed", res.Value.Status);
        Assert.True(res.Value.AiAvailable);
        Assert.Equal(88, res.Value.Analysis!.MatchScore);
    }

    [Fact]
    public async Task Cached_failed_analysis_returns_failed()
    {
        var candidateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork()
            .Seed(JobBoardData.Candidate(candidateId, cvUrl: "cv/profile.pdf"))
            .Seed(JobBoardData.Analysis(jobId, Md5Hex(CvBytes), status: "failed"));
        var storage = new RecordingFileStorage { FileBytes = CvBytes };

        var res = await Run(uow, storage, jobId, candidateId);

        Assert.Equal("failed", res.Value.Status);
        Assert.False(res.Value.AiAvailable);
        Assert.False(string.IsNullOrEmpty(res.Value.Message));
    }
}
