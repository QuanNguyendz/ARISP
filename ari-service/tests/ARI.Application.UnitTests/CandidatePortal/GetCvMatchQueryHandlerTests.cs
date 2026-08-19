using System;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>Truy cập trạng thái nền tĩnh <c>GetCvMatchQueryHandler._matchJobs</c> qua reflection để dàn cảnh
/// nhánh "in-memory state" (processing/failed) và kiểm tra job nền đã đăng ký.</summary>
internal static class MatchJobsAccess
{
    private static readonly FieldInfo DictField =
        typeof(GetCvMatchQueryHandler).GetField("_matchJobs", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly Type StateType =
        typeof(GetCvMatchQueryHandler).GetNestedType("MatchJobState", BindingFlags.NonPublic)!;

    private static IDictionary Dict => (IDictionary)DictField.GetValue(null)!;

    /// <summary>Cùng thuật toán với PortalSupport.ComputeHash (internal): MD5 hex thường.</summary>
    public static string Hash(byte[] bytes) => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    private static string Key(Guid jobId, byte[] bytes) => $"{jobId}:{Hash(bytes)}";

    private static object State(string status, string? message)
    {
        var s = Activator.CreateInstance(StateType)!;
        StateType.GetField("Status")!.SetValue(s, status);
        if (message != null) StateType.GetField("Message")!.SetValue(s, message);
        return s;
    }

    public static void Clear() => Dict.Clear();
    public static void SetProcessing(Guid jobId, byte[] bytes) => Dict[Key(jobId, bytes)] = State("processing", null);
    public static void SetFailed(Guid jobId, byte[] bytes, string message) => Dict[Key(jobId, bytes)] = State("failed", message);
    public static bool Contains(Guid jobId, byte[] bytes) => Dict.Contains(Key(jobId, bytes));
}

/// <summary>Scope factory tối giản — chỉ được gọi trong task nền (đã bọc try/catch); ném để nhánh nền set "failed".</summary>
internal sealed class ThrowingScopeFactory : IServiceScopeFactory
{
    public IServiceScope CreateScope() => throw new NotSupportedException("no background scope in unit test");
}

/// <summary>
/// Phân tích độ phù hợp CV–JD (<see cref="GetCvMatchQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetCvMatch" (UTCID01–16): thiếu tài khoản/CV, không đọc được CV, cache DB (completed/failed),
/// trạng thái nền (processing/failed), khởi chạy job nền, và các lỗi phụ thuộc (lookup/URL/download/read/analysis).
/// </summary>
public class GetCvMatchQueryHandlerTests
{
    private static readonly Guid CandidateId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static CandidateAccount Account(string? profileCvUrl = null, string? profileCvFileName = null)
        => new() { Id = CandidateId, Email = "candidate@example.com", ProfileCvUrl = profileCvUrl, ProfileCvFileName = profileCvFileName };

    private static GetCvMatchQueryHandler Handler(InMemoryUnitOfWork uow, RecordingFileStorage storage)
        => new(uow, storage, new ThrowingScopeFactory());

    private static Task<Result<CvMatchResponse>> Run(InMemoryUnitOfWork uow, RecordingFileStorage storage, Guid jobId)
        => Handler(uow, storage).Handle(new GetCvMatchQuery(jobId, CandidateId), CancellationToken.None);

    private static CvJdAnalysis Cached(Guid jobId, byte[] bytes, string status, string? errorMessage = null) => new()
    {
        Id = Guid.NewGuid(), JobPostingId = jobId, CvHash = MatchJobsAccess.Hash(bytes), Status = status,
        MatchScore = 77, Summary = "ok", SkillsMatched = "[]", SkillsGaps = "[]", RedFlags = "[]",
        ExperienceRelevance = "", OverallRecommendation = "", AiModel = "gemini", RawResponse = "{}", ErrorMessage = errorMessage,
    };

    public GetCvMatchQueryHandlerTests() => MatchJobsAccess.Clear();   // cô lập trạng thái tĩnh giữa các test

    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Run(new InMemoryUnitOfWork(), new RecordingFileStorage(), Guid.NewGuid());
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tài khoản ứng viên.", res.Error);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Theory]
    [InlineData(null)]   // UTCID02 — ProfileCvUrl null
    [InlineData("")]     // UTCID03 — ProfileCvUrl rỗng
    public async Task UTCID02_03_no_cv(string? profileCvUrl)
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl));
        var res = await Run(uow, new RecordingFileStorage(), Guid.NewGuid());
        Assert.True(res.IsSuccess);
        Assert.False(res.Value.HasCv);
        Assert.Equal("none", res.Value.Status);
    }

    [Fact]
    public async Task UTCID04_Storage_returns_null_bytes()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { FileBytes = null };
        var res = await Run(uow, storage, Guid.NewGuid());
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.HasCv);
        Assert.False(res.Value.AiAvailable);
        Assert.Equal("failed", res.Value.Status);
        Assert.Equal("Không đọc được file CV đã lưu.", res.Value.Message);
    }

    [Fact]
    public async Task UTCID05_Storage_returns_empty_bytes()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { FileBytes = Array.Empty<byte>() };
        var res = await Run(uow, storage, Guid.NewGuid());
        Assert.True(res.IsSuccess);
        Assert.Equal("failed", res.Value.Status);
        Assert.Contains("Không đọc được", res.Value.Message);
    }

    [Fact]
    public async Task UTCID06_Cached_completed()
    {
        var jobId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf")).Seed(Cached(jobId, bytes, "completed"));
        var storage = new RecordingFileStorage { FileBytes = bytes };
        var res = await Run(uow, storage, jobId);
        Assert.True(res.IsSuccess);
        Assert.True(res.Value.AiAvailable);
        Assert.Equal("completed", res.Value.Status);
        Assert.NotNull(res.Value.Analysis);
        Assert.Equal(77, res.Value.Analysis!.MatchScore);
    }

    [Fact]
    public async Task UTCID07_Cached_failed_with_error_message()
    {
        var jobId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf")).Seed(Cached(jobId, bytes, "failed", errorMessage: "Phân tích lỗi cụ thể"));
        var res = await Run(uow, new RecordingFileStorage { FileBytes = bytes }, jobId);
        Assert.True(res.IsSuccess);
        Assert.False(res.Value.AiAvailable);
        Assert.Equal("failed", res.Value.Status);
        Assert.Equal("Phân tích lỗi cụ thể", res.Value.Message);
    }

    [Fact]
    public async Task UTCID08_Cached_failed_null_error_message()
    {
        var jobId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf")).Seed(Cached(jobId, bytes, "failed", errorMessage: null));
        var res = await Run(uow, new RecordingFileStorage { FileBytes = bytes }, jobId);
        Assert.True(res.IsSuccess);
        Assert.Equal("CV không hợp lệ.", res.Value.Message);
    }

    [Fact]
    public async Task UTCID09_Inmemory_state_processing()
    {
        var jobId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        MatchJobsAccess.SetProcessing(jobId, bytes);
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var res = await Run(uow, new RecordingFileStorage { FileBytes = bytes }, jobId);
        Assert.True(res.IsSuccess);
        Assert.Equal("processing", res.Value.Status);
    }

    [Fact]
    public async Task UTCID10_Inmemory_state_failed()
    {
        var jobId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        MatchJobsAccess.SetFailed(jobId, bytes, "AI thất bại");
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var res = await Run(uow, new RecordingFileStorage { FileBytes = bytes }, jobId);
        Assert.True(res.IsSuccess);
        Assert.Equal("failed", res.Value.Status);
        Assert.Equal("AI thất bại", res.Value.Message);
    }

    [Fact]
    public async Task UTCID11_No_cache_or_state_starts_background()
    {
        var jobId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf", profileCvFileName: null));
        var res = await Run(uow, new RecordingFileStorage { FileBytes = bytes }, jobId);
        Assert.True(res.IsSuccess);
        Assert.Equal("cv.pdf", res.Value.CvFileName);
        Assert.Equal("processing", res.Value.Status);
        Assert.True(MatchJobsAccess.Contains(jobId, bytes));   // job nền đã đăng ký
    }

    [Fact]
    public async Task UTCID12_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, new RecordingFileStorage(), Guid.NewGuid()));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID13_GetUrl_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { GetUrlThrows = new Exception("URL Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, storage, Guid.NewGuid()));
        Assert.Equal("URL Error", ex.Message);
    }

    [Fact]
    public async Task UTCID14_GetDownloadUrl_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { GetDownloadUrlThrows = new Exception("Download Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, storage, Guid.NewGuid()));
        Assert.Equal("Download Error", ex.Message);
    }

    [Fact]
    public async Task UTCID15_Read_error()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf"));
        var storage = new RecordingFileStorage { ReadThrows = new Exception("Read Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, storage, Guid.NewGuid()));
        Assert.Equal("Read Error", ex.Message);
    }

    [Fact]
    public async Task UTCID16_Analysis_lookup_error()
    {
        var jobId = Guid.NewGuid(); var bytes = new byte[] { 1, 2, 3 };
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf")).FailFindFor<CvJdAnalysis>("Analysis DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Run(uow, new RecordingFileStorage { FileBytes = bytes }, jobId));
        Assert.Equal("Analysis DB Error", ex.Message);
    }
}
