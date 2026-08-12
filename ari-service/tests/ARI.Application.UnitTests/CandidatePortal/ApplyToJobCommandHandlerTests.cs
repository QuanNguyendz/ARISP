using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using ARI.Application.UnitTests.ApplicationFlow;
using ARI.Application.UnitTests.JobBoard;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Nộp hồ sơ qua Job Board (<see cref="ApplyToJobCommandHandler"/>, test-plan B10): chặn ứng tuyển trùng,
/// nguồn CV (đính kèm) → hash MD5 + parse text + lưu bản sao immutable, ráp <see cref="SubmitApplicationRequest"/>
/// (source 'job_board', trim tên/điện thoại) và bù trừ xoá file khi service tạo hồ sơ thất bại.
/// </summary>
public class ApplyToJobCommandHandlerTests
{
    private static CandidateAccount Account(string? cvUrl = null) =>
        new() { Email = "cand@example.io", ProfileCvUrl = cvUrl };

    private static ApplyToJobCommand Cmd(
        Guid jobId, Guid candId, byte[]? bytes = null, string? fileName = null,
        string name = "  Nguyen Van A  ", string phone = "  0900000000  ") =>
        new(jobId, candId, name, phone, "Thư xin việc", "  30 ngày  ", bytes, fileName);

    private static ApplyToJobCommandHandler Handler(
        InMemoryUnitOfWork uow, RecordingFileStorage storage, FakeApplicationService app, StubDocumentParser? parser = null) =>
        new(uow, storage, parser ?? new StubDocumentParser(), app);

    [Fact]
    public async Task Duplicate_application_short_circuits_without_saving_cv_or_calling_service()
    {
        var acc = Account();
        var job = ApplicationData.Job();
        var existing = ApplicationData.Application(job.Id, acc.Id, status: "cv_submitted");
        var uow = new InMemoryUnitOfWork().Seed(acc).Seed(job).Seed(existing);
        var storage = new RecordingFileStorage();
        var app = new FakeApplicationService();

        var res = await Handler(uow, storage, app)
            .Handle(Cmd(job.Id, acc.Id, bytes: new byte[] { 1, 2, 3 }, fileName: "cv.pdf"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.AlreadyApplied);
        Assert.Equal(existing.Id, res.Value.ExistingApplicationId);
        Assert.Null(res.Value.Application);
        Assert.Empty(storage.Saved);          // không lưu CV mới
        Assert.Null(app.LastRequest);         // không gọi SubmitApplication
    }

    [Fact]
    public async Task Attached_cv_is_hashed_saved_and_forwarded_as_job_board_source()
    {
        var candId = Guid.NewGuid();
        var acc = Account();
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(acc).Seed(job);
        var storage = new RecordingFileStorage();
        var app = new FakeApplicationService();
        var parser = new StubDocumentParser { Text = "Nội dung CV đã parse" };
        var bytes = Encoding.UTF8.GetBytes("PDF BYTES");
        var expectedHash = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

        var res = await Handler(uow, storage, app, parser)
            .Handle(Cmd(job.Id, acc.Id, bytes: bytes, fileName: "myresume.pdf"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.AlreadyApplied);
        Assert.NotNull(res.Value.Application);
        Assert.Equal("cv_submitted", res.Value.Application!.Status);

        // File CV: lưu đúng 1 bản với MIME theo đuôi .pdf, vào thư mục cv/.
        var saved = Assert.Single(storage.Saved);
        Assert.Equal("myresume.pdf", saved.FileName);
        Assert.Equal("application/pdf", saved.ContentType);
        Assert.Equal(StorageFolder.Cv, saved.Folder);

        // Request chuyển cho service: source + trim + hash + CV text + URL bản đã lưu.
        Assert.Equal("job_board", app.LastSource);
        var req = app.LastRequest!;
        Assert.Equal(job.Id, req.JobPostingId);
        Assert.Equal(acc.Id, req.CandidateAccountId);
        Assert.Equal("cand@example.io", req.CandidateEmail);
        Assert.Equal("Nguyen Van A", req.CandidateName);   // trim
        Assert.Equal("0900000000", req.CandidatePhone);    // trim
        Assert.Equal("30 ngày", req.NoticePeriod);         // trim
        Assert.Equal("cv/myresume.pdf", req.CvFileUrl);
        Assert.Equal("Nội dung CV đã parse", req.CvText);
        Assert.Equal(expectedHash, req.CvFileHash);
    }

    [Fact]
    public async Task No_attachment_and_no_profile_cv_fails_with_no_cv_code()
    {
        var acc = Account(cvUrl: null);       // hồ sơ không có CV
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(acc).Seed(job);
        var storage = new RecordingFileStorage();
        var app = new FakeApplicationService();

        var res = await Handler(uow, storage, app)
            .Handle(Cmd(job.Id, acc.Id, bytes: null, fileName: null), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("no_cv", res.ErrorCode);
        Assert.Empty(storage.Saved);
        Assert.Null(app.LastRequest);
    }

    [Fact]
    public async Task Saved_cv_is_deleted_when_submit_service_fails()
    {
        var acc = Account();
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(acc).Seed(job);
        var storage = new RecordingFileStorage();
        var app = new FakeApplicationService
        {
            SubmitResult = Result.Failure<ApplicationResponse>("Tin tuyển dụng đã đóng.")
        };

        var res = await Handler(uow, storage, app)
            .Handle(Cmd(job.Id, acc.Id, bytes: new byte[] { 9 }, fileName: "cv.pdf"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Tin tuyển dụng đã đóng", res.Error);   // lỗi service propagate
        Assert.Single(storage.Saved);                          // đã lưu CV
        Assert.Contains("cv/cv.pdf", storage.Deleted);     // rồi bù trừ xoá đi
        Assert.NotNull(app.LastRequest);                       // service ĐÃ được gọi
    }
}
