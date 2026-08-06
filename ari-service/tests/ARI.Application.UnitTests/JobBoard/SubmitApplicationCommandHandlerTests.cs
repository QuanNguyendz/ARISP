using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Applications.Commands.SubmitApplication;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.TestSupport;
using Xunit;

namespace ARI.Application.UnitTests.JobBoard;

/// <summary>
/// Nộp hồ sơ từ Job Board — wrapper CQRS (UC-28, <see cref="SubmitApplicationCommandHandler"/>): hash MD5 CV
/// (cache CV-JD), parse text, lưu file rồi ủy quyền <c>IApplicationService</c>; lỗi parse/lưu trả Failure,
/// lỗi ghi DB thì dọn file đã lưu.
/// </summary>
public class SubmitApplicationCommandHandlerTests
{
    private static readonly byte[] CvBytes = Encoding.UTF8.GetBytes("CV-BINARY-CONTENT");

    private static string Md5Hex(byte[] bytes) => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

    private static SubmitApplicationCommand Command(string fileName = "cv.pdf", string ext = ".pdf")
        => new(Guid.NewGuid(), Guid.NewGuid(), "cand@example.io", "Nguyen Van A", "0900000000", CvBytes, fileName, ext);

    private static Task<Result<ApplicationResponse>> Run(
        FakeApplicationService svc, StubDocumentParser parser, RecordingFileStorage storage, SubmitApplicationCommand cmd)
        => new SubmitApplicationCommandHandler(svc, parser, storage).Handle(cmd, CancellationToken.None);

    [Fact]
    public async Task Parses_hashes_saves_and_delegates_to_service()
    {
        var svc = new FakeApplicationService();
        var parser = new StubDocumentParser { Text = "CV nội dung" };
        var storage = new RecordingFileStorage();

        var res = await Run(svc, parser, storage, Command("cv.pdf", ".pdf"));

        Assert.True(res.IsSuccess);
        Assert.Equal("CV nội dung", svc.LastRequest!.CvText);
        Assert.Equal("stored/cv.pdf", svc.LastRequest.CvFileUrl);
        Assert.Equal(Md5Hex(CvBytes), svc.LastRequest.CvFileHash);
        Assert.Equal("job_board", svc.LastSource);
    }

    [Fact]
    public async Task Parse_failure_returns_error_without_saving_or_delegating()
    {
        var svc = new FakeApplicationService();
        var storage = new RecordingFileStorage();

        var res = await Run(svc, new StubDocumentParser { ThrowOnParse = true }, storage, Command());

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể phân tích file CV", res.Error);
        Assert.Empty(storage.Saved);
        Assert.Null(svc.LastRequest);
    }

    [Fact]
    public async Task Storage_failure_returns_server_error()
    {
        var svc = new FakeApplicationService();

        var res = await Run(svc, new StubDocumentParser(), new RecordingFileStorage { ThrowOnSave = true }, Command());

        Assert.True(res.IsFailure);
        Assert.Contains("Không thể lưu file CV", res.Error);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
        Assert.Null(svc.LastRequest);
    }

    [Fact]
    public async Task Db_failure_cleans_up_saved_file()
    {
        var svc = new FakeApplicationService { SubmitResult = Result.Failure<ApplicationResponse>("db down") };
        var storage = new RecordingFileStorage();

        var res = await Run(svc, new StubDocumentParser(), storage, Command("cv.pdf"));

        Assert.True(res.IsFailure);
        Assert.Contains("stored/cv.pdf", storage.Deleted); // file đã lưu được dọn khi ghi DB lỗi
    }

    [Fact]
    public async Task Null_bytes_in_parsed_text_are_stripped()
    {
        var svc = new FakeApplicationService();
        var parser = new StubDocumentParser { Text = "a\0b\0c" };

        await Run(svc, parser, new RecordingFileStorage(), Command());

        Assert.Equal("abc", svc.LastRequest!.CvText);
    }
}
