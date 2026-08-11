using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.CvAnalysis.Commands.AnalyzeCv;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CvAnalysis;

/// <summary>
/// Phân tích CV–JD (<see cref="AnalyzeCvCommandHandler"/>, test-plan B27): handler mỏng delegate sang
/// <see cref="ICvJdAnalysisService.AnalyzeAndCacheAsync"/> — truyền đúng (jobId, stream, fileName) và
/// propagate kết quả (Success/Failure) nguyên vẹn, không throw.
/// </summary>
public class AnalyzeCvCommandHandlerTests
{
    [Fact]
    public async Task Delegates_arguments_and_returns_success_result_unchanged()
    {
        var jobId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var analysis = new CvJdAnalysis { JobPostingId = jobId, MatchScore = 88 };
        var svc = new RecordingCvJdAnalysisService { Result = Result.Success(analysis) };

        var res = await new AnalyzeCvCommandHandler(svc)
            .Handle(new AnalyzeCvCommand(jobId, stream, "cv.pdf"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Same(analysis, res.Value);
        Assert.Equal(jobId, svc.LastJobId);
        Assert.Same(stream, svc.LastStream);
        Assert.Equal("cv.pdf", svc.LastFileName);
    }

    [Fact]
    public async Task Propagates_service_failure_without_throwing()
    {
        var svc = new RecordingCvJdAnalysisService { Result = Result.Failure<CvJdAnalysis>("Gemini quá tải, thử lại sau.") };

        var res = await new AnalyzeCvCommandHandler(svc)
            .Handle(new AnalyzeCvCommand(Guid.NewGuid(), new MemoryStream(), "cv.pdf"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Gemini quá tải, thử lại sau.", res.Error);
    }

    /// <summary>ICvJdAnalysisService giả — ghi lại tham số của AnalyzeAndCacheAsync; phương thức khác không dùng.</summary>
    private sealed class RecordingCvJdAnalysisService : ICvJdAnalysisService
    {
        public Guid LastJobId { get; private set; }
        public Stream? LastStream { get; private set; }
        public string? LastFileName { get; private set; }
        public Result<CvJdAnalysis> Result { get; set; } = ARI.Application.Common.Result.Success(new CvJdAnalysis());

        public Task<Result<CvJdAnalysis>> AnalyzeAndCacheAsync(Guid jobPostingId, Stream cvFileStream, string cvFileName, CancellationToken ct = default)
        {
            LastJobId = jobPostingId;
            LastStream = cvFileStream;
            LastFileName = cvFileName;
            return Task.FromResult(Result);
        }

        public Task<Result<CvJdAnalysis>> GetAnalysisByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<CvJdAnalysis>> GetAnalysisByApplicationIdAsync(Guid applicationId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> CheckCandidateOwnershipAsync(Guid cvAnalysisId, Guid candidateAccountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ClearAllCacheAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }
}
