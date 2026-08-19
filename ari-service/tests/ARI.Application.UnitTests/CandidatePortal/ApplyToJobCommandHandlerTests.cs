using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.CandidatePortal;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.UnitTests.ApplicationFlow;
using ARI.Application.UnitTests.JobBoard;
using ARI.Application.UnitTests.TestSupport;
using ARI.Domain.Entities;
using Xunit;

namespace ARI.Application.UnitTests.CandidatePortal;

/// <summary>
/// Nộp hồ sơ qua Job Board (<see cref="ApplyToJobCommandHandler"/>) — test-plan Report5 Unit v1.2,
/// tab "ApplyToJob" (UTCID01–16): chặn trùng, chọn nguồn CV (đính kèm/hồ sơ), MIME theo đuôi, parse best-effort,
/// lưu bản sao immutable + bù trừ xoá khi service lỗi, ráp request (source 'job_board', trim), và các nhánh lỗi.
/// </summary>
public class ApplyToJobCommandHandlerTests
{
    private static readonly Guid CandidateId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static CandidateAccount Account(string? profileCvUrl = null)
        => new() { Id = CandidateId, Email = "candidate@example.com", ProfileCvUrl = profileCvUrl };

    private static ApplyToJobCommand Cmd(Guid jobId, byte[]? bytes, string? fileName,
        string name = "Candidate User", string phone = "0901234567", string notice = "30 days")
        => new(jobId, CandidateId, name, phone, "Cover letter", notice, bytes, fileName);

    private static ApplyToJobCommandHandler Handler(InMemoryUnitOfWork uow, RecordingFileStorage storage, FakeApplicationService app, StubDocumentParser? parser = null)
        => new(uow, storage, parser ?? new StubDocumentParser(), app);

    [Fact]
    public async Task UTCID01_Unknown_candidate()
    {
        var res = await Handler(new InMemoryUnitOfWork(), new RecordingFileStorage(), new FakeApplicationService())
            .Handle(Cmd(Guid.NewGuid(), new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tài khoản ứng viên.", res.Error);
        Assert.Equal(CommonErrorCodes.Unauthorized, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID02_Unknown_job()
    {
        var uow = new InMemoryUnitOfWork().Seed(Account());
        var res = await Handler(uow, new RecordingFileStorage(), new FakeApplicationService())
            .Handle(Cmd(Guid.NewGuid(), new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("Không tìm thấy tin tuyển dụng.", res.Error);
        Assert.Equal(CommonErrorCodes.NotFound, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID03_Existing_non_withdrawn_short_circuits()
    {
        var job = ApplicationData.Job();
        var existing = ApplicationData.Application(job.Id, CandidateId, status: "cv_submitted");
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job).Seed(existing);
        var storage = new RecordingFileStorage(); var app = new FakeApplicationService();

        var res = await Handler(uow, storage, app).Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.AlreadyApplied);
        Assert.Equal(existing.Id, res.Value.ExistingApplicationId);
        Assert.Null(res.Value.Application);
        Assert.Empty(storage.Saved);
        Assert.Null(app.LastRequest);
    }

    [Fact]
    public async Task UTCID04_Only_withdrawn_allows_new_submission()
    {
        var job = ApplicationData.Job();
        var withdrawn = ApplicationData.Application(job.Id, CandidateId, status: "withdrawn");
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job).Seed(withdrawn);
        var app = new FakeApplicationService();

        var res = await Handler(uow, new RecordingFileStorage(), app).Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.AlreadyApplied);
        Assert.NotNull(res.Value.Application);
        Assert.NotNull(app.LastRequest);
    }

    [Fact]
    public async Task UTCID05_Attached_pdf_submitted()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job);
        var app = new FakeApplicationService();
        var res = await Handler(uow, new RecordingFileStorage(), app).Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.False(res.Value.AlreadyApplied);
        Assert.Null(res.Value.ExistingApplicationId);
        Assert.NotNull(res.Value.Application);
    }

    [Fact]
    public async Task UTCID06_Profile_cv_copied_and_submitted()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf")).Seed(job);
        var storage = new RecordingFileStorage { FileBytes = new byte[] { 9, 9 } };
        var app = new FakeApplicationService();

        var res = await Handler(uow, storage, app).Handle(Cmd(job.Id, null, null), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Single(storage.Saved);          // bản sao CV được lưu
        Assert.NotNull(app.LastRequest);
    }

    [Fact]
    public async Task UTCID07_No_attachment_no_profile_cv()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: null)).Seed(job);
        var res = await Handler(uow, new RecordingFileStorage(), new FakeApplicationService()).Handle(Cmd(job.Id, null, null), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("no_cv", res.ErrorCode);
    }

    [Fact]
    public async Task UTCID08_Profile_cv_reads_empty()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf")).Seed(job);
        var storage = new RecordingFileStorage { FileBytes = null };
        var res = await Handler(uow, storage, new FakeApplicationService()).Handle(Cmd(job.Id, null, null), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("cv_unreadable", res.ErrorCode);
    }

    [Fact]
    public async Task UTCID09_Unknown_ext_uses_octet_stream_mime()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job);
        var storage = new RecordingFileStorage(); var app = new FakeApplicationService();
        await Handler(uow, storage, app).Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.txt"), CancellationToken.None);
        Assert.Equal("application/octet-stream", Assert.Single(storage.Saved).ContentType);
    }

    [Fact]
    public async Task UTCID10_Parser_throws_submits_with_empty_cvtext()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job);
        var app = new FakeApplicationService();
        var res = await Handler(uow, new RecordingFileStorage(), app, new StubDocumentParser { ThrowOnParse = true })
            .Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsSuccess);
        Assert.Equal("", app.LastRequest!.CvText);
    }

    [Fact]
    public async Task UTCID11_Save_error_is_server_error()
    {
        // Fake ném thông điệp cố định ("storage down"); ta khẳng định tiền tố VN + mã lỗi (báo cáo ghi "Storage Error" là placeholder).
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job);
        var storage = new RecordingFileStorage { ThrowOnSave = true };
        var res = await Handler(uow, storage, new FakeApplicationService()).Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.StartsWith("Không thể lưu CV cho hồ sơ ứng tuyển:", res.Error);
        Assert.Equal(CommonErrorCodes.ServerError, res.ErrorCode);
    }

    [Fact]
    public async Task UTCID12_Service_failure_deletes_stored_cv()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job);
        var storage = new RecordingFileStorage();
        var app = new FakeApplicationService { SubmitResult = Result.Failure<ApplicationResponse>("Submit Error") };

        var res = await Handler(uow, storage, app).Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Contains("Submit Error", res.Error);
        Assert.Single(storage.Saved);
        Assert.Contains("cv/cv.pdf", storage.Deleted);
        Assert.NotNull(app.LastRequest);
    }

    [Fact]
    public async Task UTCID13_Success_trims_inputs_and_uses_job_board_source()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job);
        var app = new FakeApplicationService();

        var res = await Handler(uow, new RecordingFileStorage(), app)
            .Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.pdf", name: "  Candidate User  ", phone: "  0901234567  ", notice: "  30 days  "), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.AlreadyApplied);
        Assert.NotNull(res.Value.Application);
        Assert.Equal("job_board", app.LastSource);
        Assert.Equal("Candidate User", app.LastRequest!.CandidateName);
        Assert.Equal("0901234567", app.LastRequest.CandidatePhone);
        Assert.Equal("30 days", app.LastRequest.NoticePeriod);
    }

    [Fact]
    public async Task UTCID14_Candidate_lookup_error()
    {
        var uow = new InMemoryUnitOfWork().FailGetByIdFor<CandidateAccount>("Candidate DB Error");
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new RecordingFileStorage(), new FakeApplicationService())
            .Handle(Cmd(Guid.NewGuid(), new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None));
        Assert.Equal("Candidate DB Error", ex.Message);
    }

    [Fact]
    public async Task UTCID15_Profile_cv_read_error()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account(profileCvUrl: "stored/cv.pdf")).Seed(job);
        var storage = new RecordingFileStorage { ReadThrows = new Exception("Read Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, storage, new FakeApplicationService())
            .Handle(Cmd(job.Id, null, "cv.pdf"), CancellationToken.None));
        Assert.Equal("Read Error", ex.Message);
    }

    [Fact]
    public async Task UTCID16_Service_throws_propagates()
    {
        var job = ApplicationData.Job();
        var uow = new InMemoryUnitOfWork().Seed(Account()).Seed(job);
        var app = new FakeApplicationService { SubmitThrows = new Exception("Service Error") };
        var ex = await Assert.ThrowsAsync<Exception>(() => Handler(uow, new RecordingFileStorage(), app)
            .Handle(Cmd(job.Id, new byte[] { 1, 2, 3 }, "cv.pdf"), CancellationToken.None));
        Assert.Equal("Service Error", ex.Message);
    }
}
