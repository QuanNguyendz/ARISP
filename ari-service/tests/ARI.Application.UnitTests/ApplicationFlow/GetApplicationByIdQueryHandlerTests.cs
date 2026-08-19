using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Queries;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.ApplicationFlow;

/// <summary>
/// Wrapper CQRS chi tiết hồ sơ (<see cref="GetApplicationByIdQueryHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "GetApplicationById" (UTCID01–07): forward tới service, not-found → gắn mã not_found, resolve storageKey→URL
/// (chỉ khi CvFileUrl có giá trị), và propagate lỗi service / lỗi tạo URL.
/// </summary>
public class GetApplicationByIdQueryHandlerTests
{
    private static readonly Guid AppId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    private static GetApplicationByIdQueryHandler Handler(StubApplicationService svc, RecordingFileStorage storage) => new(svc, storage);

    private static StubApplicationService SvcWith(string? cvFileUrl)
        => new() { GetByIdResult = Result.Success(new ApplicationResponse { Id = AppId, CvFileUrl = cvFileUrl }) };

    // UTCID01 — app có storageKey CV → resolve thành URL
    [Fact]
    public async Task UTCID01_Resolves_cv_url()
    {
        var storage = new RecordingFileStorage { GetUrlResult = "/files/cv/key.pdf" };

        var res = await Handler(SvcWith("cv/key.pdf"), storage).Handle(new GetApplicationByIdQuery(AppId), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("/files/cv/key.pdf", res.Value.CvFileUrl);
    }

    // UTCID02 — CvFileUrl=null → không resolve, giữ null
    [Fact]
    public async Task UTCID02_Null_cv_url()
    {
        var res = await Handler(SvcWith(null), new RecordingFileStorage()).Handle(new GetApplicationByIdQuery(AppId), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Null(res.Value.CvFileUrl);
    }

    // UTCID03 — CvFileUrl="" → không resolve, giữ ""
    [Fact]
    public async Task UTCID03_Empty_cv_url()
    {
        var res = await Handler(SvcWith(""), new RecordingFileStorage()).Handle(new GetApplicationByIdQuery(AppId), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.CvFileUrl);
    }

    // UTCID04 — service trả not-found failure → gắn mã not_found
    [Fact]
    public async Task UTCID04_Not_found()
    {
        var svc = new StubApplicationService { GetByIdResult = Result.Failure<ApplicationResponse>("Application not found.") };

        var res = await Handler(svc, new RecordingFileStorage()).Handle(new GetApplicationByIdQuery(AppId), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("Application not found.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    // UTCID05 — file storage trả URL rỗng → CvFileUrl=""
    [Fact]
    public async Task UTCID05_Storage_returns_empty_url()
    {
        var storage = new RecordingFileStorage { GetUrlResult = "" };

        var res = await Handler(SvcWith("cv/key.pdf"), storage).Handle(new GetApplicationByIdQuery(AppId), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("", res.Value.CvFileUrl);
    }

    // UTCID06 — service ném lỗi → propagate
    [Fact]
    public async Task UTCID06_Service_throws()
    {
        var svc = new StubApplicationService { GetByIdThrows = new Exception("Service Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(svc, new RecordingFileStorage()).Handle(new GetApplicationByIdQuery(AppId), CancellationToken.None));
        Assert.Equal("Service Error", ex.Message);
    }

    // UTCID07 — file storage GetUrlAsync ném lỗi → propagate
    [Fact]
    public async Task UTCID07_Storage_url_throws()
    {
        var storage = new RecordingFileStorage { GetUrlThrows = new Exception("URL Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(SvcWith("cv/key.pdf"), storage).Handle(new GetApplicationByIdQuery(AppId), CancellationToken.None));
        Assert.Equal("URL Error", ex.Message);
    }
}
